// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia.Headless;
using Avalonia.VisualTree;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Hubs;
using OpControl.Parental.Server.Services;
using OpControl.Parental.Server.Services.Luna;
using OpControl.Parental.Server.Services.Luna.Providers;
using OpControl.Parental.Server.Services.Push;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job21WebSearchTests
{
    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job21-" + Guid.NewGuid())
            .Options);

    private sealed class CaptureHandler(List<string> bodies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "choices":[{"message":{"role":"assistant","content":"Grounded answer",
                    "annotations":[
                      {"type":"url_citation","url_citation":{"url":"https://www.example.org/page","title":"Example page"}},
                      {"type":"url_citation","url_citation":{"url":"https://example.org/page","title":"Duplicate"}}
                    ],
                    "tool_calls":[{"function":{"arguments":"{\"query\":\"fractions explained\"}"}}]}}],
                  "usage":{"prompt_tokens":12,"completion_tokens":8,
                    "server_tool_use_details":{"web_search_requests":2}}
                }
                """, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StubFactory(List<string> bodies) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new CaptureHandler(bodies));
    }

    [Fact]
    public async Task Payload_uses_exa_caps_exclusions_and_keeps_zdr()
    {
        var bodies = new List<string>();
        var config = new LunaProviderConfig(LunaChatProviders.OpenRouter, "test/model", "sk-test");
        var options = new LunaWebSearchOptions("exa", 5, 10, ["bad.example", "worse.example"]);
        var result = await new OpenAiCompatibleChatProvider(new StubFactory(bodies)).CompleteAsync(config,
            new LunaChatRequest("system", [new LunaChatTurn("user", "hi")], 100, WebSearch: options), default);

        using var json = JsonDocument.Parse(bodies.Single());
        var root = json.RootElement;
        Assert.True(root.GetProperty("provider").GetProperty("zdr").GetBoolean());
        var tool = root.GetProperty("tools")[0];
        Assert.Equal("openrouter:web_search", tool.GetProperty("type").GetString());
        var parameters = tool.GetProperty("parameters");
        Assert.Equal("exa", parameters.GetProperty("engine").GetString());
        Assert.Equal(5, parameters.GetProperty("max_results").GetInt32());
        Assert.Equal(10, parameters.GetProperty("max_total_results").GetInt32());
        Assert.Equal(["bad.example", "worse.example"],
            parameters.GetProperty("excluded_domains").EnumerateArray().Select(x => x.GetString()).ToArray());

        Assert.Equal(2, result.WebSearchRequests);
        Assert.Equal("fractions explained", Assert.Single(result.Queries!));
        Assert.Equal(2, result.Sources!.Count);
        Assert.Equal("https://www.example.org/page", result.Sources[0].Url);
        Assert.Equal("Example page", result.Sources[0].Title);
    }

    [Fact]
    public async Task Setup_b_rejects_the_web_search_switch()
    {
        await using var h = await HarnessAsync(setup: LunaSetup.B);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Parent.SaveChildAsync(h.Family.Id, h.ParentUser.Id, h.Child.Id,
                new ChildLunaRequest(true, 60, null, true, WebSearchEnabled: true), default));
        Assert.Contains("Setup A", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Blocked_cited_source_is_retried_without_it()
    {
        await using var h = await HarnessAsync();
        await EnableWebSearchAsync(h.Db);
        await AddCategoryDomainAsync(h.Db, "adult", "bad.example");
        h.Providers.Chat.Handler = request => request.WebSearch is null
            ? new LunaChatResult("unused", 1, 1)
            : request.SystemPrompt.Contains("not allowed to see", StringComparison.Ordinal)
                ? new LunaChatResult("Safe retry answer", 2, 2, [], 1)
                : new LunaChatResult("Unsafe sourced answer", 3, 3,
                    [new LunaChatSource("https://bad.example/story", "Bad source")], 1);
        var chat = await h.Chat.CreateChatAsync(Context(h), LunaSubjects.Science);

        var result = await h.Chat.SendAsync(Context(h), chat.Id, "What happened today?", default);

        Assert.Equal("Safe retry answer", result.LunaMessage!.Text);
        Assert.Empty(result.LunaMessage.Sources!);
        var saved = await h.Db.LunaMessages.SingleAsync(m => m.Role == LunaChatRoles.Luna);
        Assert.Equal("What happened today?", saved.WebQuery);
        Assert.Equal(2, saved.WebSearchRequests);
    }

    [Fact]
    public async Task Blocked_source_that_survives_the_retry_uses_the_fallback_and_alerts()
    {
        await using var h = await HarnessAsync();
        await EnableWebSearchAsync(h.Db);
        await AddCategoryDomainAsync(h.Db, "gambling", "bad.example");
        h.Providers.Chat.Handler = _ => new LunaChatResult("Sourced answer", 2, 2,
            [new LunaChatSource("https://bad.example/story", "Bad source")], 1);
        var chat = await h.Chat.CreateChatAsync(Context(h), LunaSubjects.Curious);

        var result = await h.Chat.SendAsync(Context(h), chat.Id, "Tell me about that site", default);

        Assert.Equal(AppText.Get("Luna.Child.ReplyFallback"), result.LunaMessage!.Text);
        var saved = await h.Db.LunaMessages.SingleAsync(m => m.Role == LunaChatRoles.Luna);
        Assert.Equal(LunaMessageFlag.ReplyBlocked, saved.Flag);
        Assert.Equal("Sourced answer", saved.Text);
        Assert.Contains("bad.example", saved.FlagCategories);
        Assert.True(await h.Db.Alerts.AnyAsync(a => a.Type == "luna-flagged"));
    }

    [Fact]
    public async Task Excluded_domains_include_child_rules_and_stop_at_the_engine_limit()
    {
        await using var db = NewDb();
        var family = new Family { Name = "Demo" };
        var child = new Child { FamilyId = family.Id, Name = "Ana" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Categories.Add(new Category { Key = "adult", Name = "Adult" });
        db.Categories.Add(new Category { Key = "gambling", Name = "Gambling" });
        db.Categories.Add(new Category { Key = "ai", Name = "Public AI chats" });
        for (var i = 0; i < 800; i++)
            db.CategoryDomains.Add(new CategoryDomain { CategoryKey = "adult", Domain = $"adult{i}.example" });
        for (var i = 0; i < 800; i++)
            db.CategoryDomains.Add(new CategoryDomain { CategoryKey = "gambling", Domain = $"bet{i}.example" });
        db.WebsiteRules.Add(new WebsiteRule
        {
            FamilyId = family.Id, ChildId = child.Id, Domain = "blocked.example", IsBlocked = true
        });
        await db.SaveChangesAsync();

        var guard = new LunaWebSearchGuard(db, new MemoryCache(new MemoryCacheOptions()));
        var result = await guard.BuildAsync(family.Id, child.Id, default);

        Assert.Equal(LunaWebSearchGuard.MaxExcludedDomains, result.ExcludedDomains.Count);
        Assert.Equal("blocked.example", result.ExcludedDomains[0]);
        Assert.Contains("adult0.example", result.ExcludedDomains);
        Assert.Contains("bet0.example", result.ExcludedDomains);
        Assert.True(LunaWebSearchGuard.IsBlocked("https://sub.adult0.example/x", result.BlockedDomains));
        Assert.False(LunaWebSearchGuard.IsBlocked("https://example.org/x", result.BlockedDomains));
    }

    [Fact]
    public async Task Switch_off_sends_no_web_search_tool()
    {
        await using var h = await HarnessAsync();
        var chat = await h.Chat.CreateChatAsync(Context(h), LunaSubjects.Math);
        await h.Chat.SendAsync(Context(h), chat.Id, "Hello", default);
        Assert.All(h.Providers.Chat.Requests, request => Assert.Null(request.WebSearch));
    }


    [Fact]
    public void Luna_and_My_OpControl_inputs_wrap_and_grow_to_six_lines()
    {
        var root = FindRoot();
        var luna = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        var kid = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "MyOpControlWindow.cs"));
        foreach (var source in new[] { luna, kid })
        {
            Assert.Contains("AcceptsReturn = true", source, StringComparison.Ordinal);
            Assert.Contains("TextWrapping = TextWrapping.Wrap", source, StringComparison.Ordinal);
            Assert.Contains("MinHeight = 46", source, StringComparison.Ordinal);
            Assert.Contains("MaxHeight = 150", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Source_label_is_translated()
    {
        var root = FindRoot();
        var en = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "en.json"));
        var es = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Localization", "es-MX.json"));
        Assert.Contains("\"Luna.Chat.SourcesTitle\"", en, StringComparison.Ordinal);
        Assert.Contains("Where I found this", en, StringComparison.Ordinal);
        Assert.Contains("Dónde lo encontré", es, StringComparison.Ordinal);
    }


    [Fact]
    public void Luna_reply_shows_sources_and_the_input_grows_to_six_lines()
    {
        using var session = Avalonia.Headless.HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        session.Dispatch(() =>
        {
            OpControl.Parental.Localization.Loc.SetLanguage(OpControl.Parental.Localization.Loc.English);
            var api = new OpControl.Parental.Luna.LunaApiClient();
            var view = new OpControl.Parental.Luna.LunaAppView(api, "night");
            view.MarkInitializedForTest();
            var window = new Avalonia.Controls.Window
            {
                Width = 960, Height = 760, WindowDecorations = Avalonia.Controls.WindowDecorations.None,
                Content = view
            };
            window.Show();
            UiHarness.Pump(250);
            var chat = new LunaChatDto(Guid.NewGuid(), "science", "Mount Everest", false, false,
                DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow);
            var luna = new LunaChildMessageDto(Guid.NewGuid(), chat.Id, LunaChatRoles.Luna,
                "Mount Everest is about 8,849 meters tall.", DateTime.UtcNow.AddMinutes(-4), "none",
                [new LunaSourceDto("https://en.wikipedia.org/wiki/Mount_Everest", "Mount Everest - Wikipedia", "en.wikipedia.org")]);
            view.OpenChatForTest(chat, [luna]);
            UiHarness.Pump(600);

            var label = view.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>()
                .First(t => t.Text == "Where I found this");
            Assert.NotNull(label);
            var link = view.GetVisualDescendants().OfType<Avalonia.Controls.Button>()
                .First(b => b.Content is Avalonia.Controls.TextBlock tb && tb.Text.Contains("Mount Everest"));
            Assert.NotNull(link);

            if (view.InputForTest is { } input)
            {
                input.Text = "Line one\nLine two\nLine three\nLine four\nLine five\nLine six";
                UiHarness.Pump(200);
                Assert.True(input.Bounds.Height > 90, $"input height {input.Bounds.Height}");
                Assert.True(input.Bounds.Height <= input.MaxHeight + 0.5);
            }

            window.Close();
            api.Dispose();
        }, CancellationToken.None);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    private static async Task EnableWebSearchAsync(ParentalDbContext db)
    {
        var settings = await db.ChildLunaSettings.SingleAsync();
        settings.WebSearchEnabled = true;
        await db.SaveChangesAsync();
    }

    private static async Task AddCategoryDomainAsync(ParentalDbContext db, string key, string domain)
    {
        if (!await db.Categories.AnyAsync(c => c.Key == key))
            db.Categories.Add(new Category { Key = key, Name = key });
        db.CategoryDomains.Add(new CategoryDomain { CategoryKey = key, Domain = domain });
        await db.SaveChangesAsync();
    }

    private sealed record Harness(
        ParentalDbContext Db, Family Family, Child Child, ParentUser ParentUser, Device Device,
        LunaChatService Chat, LunaParentService Parent, FakeProviders Providers) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakeProviders : ILunaProviderFactory
    {
        private readonly IDataProtector _protector;
        public FakeChat Chat { get; } = new();
        public FakeModerator Moderator { get; } = new();

        public FakeProviders(IDataProtectionProvider protection) =>
            _protector = protection.CreateProtector(LunaProviderFactory.KeyProtector);

        public ILunaChatProvider GetChat(string providerId) => Chat;
        public ILunaModerator GetModerator(LunaFamilySettings settings) => Moderator;
        public string? ChatKey(LunaFamilySettings settings) => Unprotect(settings.ChatKeyProtected);
        public string? ModerationKey(LunaFamilySettings settings) => Unprotect(settings.ModerationKeyProtected);
        public string? Protect(string? value) => string.IsNullOrWhiteSpace(value) ? null : _protector.Protect(value.Trim());
        private string? Unprotect(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try { return _protector.Unprotect(value); } catch { return null; }
        }
    }

    private sealed class FakeChat : ILunaChatProvider
    {
        public string ProviderId => "fake";
        public List<LunaChatRequest> Requests { get; } = [];
        public Func<LunaChatRequest, LunaChatResult>? Handler { get; set; }

        public Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            if (request.SystemPrompt == LunaPrompts.TitlePrompt)
                return Task.FromResult(new LunaChatResult("Title", 1, 1));
            if (request.SystemPrompt.StartsWith(LunaPrompts.SummaryPrompt, StringComparison.Ordinal))
                return Task.FromResult(new LunaChatResult("Summary", 1, 1));
            if (request.SystemPrompt.StartsWith(LunaPrompts.ProfilePrompt, StringComparison.Ordinal))
                return Task.FromResult(new LunaChatResult("Profile", 1, 1));
            return Task.FromResult(Handler?.Invoke(request) ?? new LunaChatResult("A safe answer", 10, 5));
        }
    }

    private sealed class FakeModerator : ILunaModerator
    {
        public Task<LunaModerationResult> ModerateAsync(string text, CancellationToken ct) =>
            Task.FromResult(new LunaModerationResult(false, [], new Dictionary<string, double>()));
    }

    private static async Task<Harness> HarnessAsync(LunaSetup setup = LunaSetup.A)
    {
        var db = NewDb();
        var protection = new EphemeralDataProtectionProvider();
        var providers = new FakeProviders(protection);
        var family = new Family { Name = "Demo" };
        var child = new Child { FamilyId = family.Id, Name = "Ana Demo" };
        var parent = new ParentUser
        {
            FamilyId = family.Id, DisplayName = "Parent", RoleInFamily = FamilyRole.Owner,
            UserName = "parent@example.test", Email = "parent@example.test"
        };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "PC", TimeZoneId = "UTC" };
        db.AddRange(family, child, parent, device);
        db.LunaFamilySettings.Add(new LunaFamilySettings
        {
            FamilyId = family.Id,
            Setup = setup,
            ChatProvider = setup == LunaSetup.A ? LunaChatProviders.OpenRouter : LunaChatProviders.OpenAi,
            ChatModel = "test/model",
            ChatKeyProtected = providers.Protect("sk-test-1234"),
            ModerationKeyProtected = providers.Protect("sk-mod-5678"),
            LastTestUtc = DateTime.UtcNow,
            LastTestOk = true,
            HelplineText = ""
        });
        db.ChildLunaSettings.Add(new ChildLunaSettings
        {
            FamilyId = family.Id, ChildId = child.Id, Enabled = true, DailyMessageLimit = 60
        });
        await db.SaveChangesAsync();

        var alerts = new AlertService(db, new DashboardEventBus(), protection,
            new EmailSender(db, protection,
                new EmailRenderer(Microsoft.Extensions.Options.Options.Create(new EmailOptions()),
                    new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
                Microsoft.Extensions.Options.Options.Create(new EmailOptions()),
                NullLogger<EmailSender>.Instance),
            new EmailRenderer(Microsoft.Extensions.Options.Options.Create(new EmailOptions()),
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            NullLogger<AlertService>.Instance);
        var settings = new LunaSettingsService(db, providers, new FakeZdr(), NullLogger<LunaSettingsService>.Instance, alerts);
        var usage = new LunaUsageStore(db);
        var chat = new LunaChatService(db, providers, settings, usage,
            new LunaWebSearchGuard(db, new MemoryCache(new MemoryCacheOptions())),
            new LockService(db, new NullHub(), new DashboardEventBus(), new RuleService(db, alerts),
                new UsageService(db, alerts), alerts, NullLogger<LockService>.Instance),
            alerts, NullLogger<LunaChatService>.Instance);
        var parentSvc = new LunaParentService(db, settings, usage);
        return new Harness(db, family, child, parent, device, chat, parentSvc, providers);
    }

    private static LunaTokenContext Context(Harness h) =>
        new(Guid.NewGuid(), h.Family.Id, h.Child.Id, h.Device.Id, DateTime.UtcNow.AddMinutes(15));

    private sealed class FakeZdr : ILunaZdrCatalog
    {
        public Task<string?> MissingModelAsync(string chatModel, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private sealed class NullHub : Microsoft.AspNetCore.SignalR.IHubContext<ParentalHub>
    {
        public Microsoft.AspNetCore.SignalR.IHubClients Clients => NullClients.Instance;
        public Microsoft.AspNetCore.SignalR.IGroupManager Groups => NullGroups.Instance;
    }

    private sealed class NullClients : Microsoft.AspNetCore.SignalR.IHubClients
    {
        public static readonly NullClients Instance = new();
        public Microsoft.AspNetCore.SignalR.IClientProxy All => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy AllExcept(IReadOnlyList<string> excluded) => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy Client(string id) => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy Clients(IReadOnlyList<string> ids) => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy Group(string name) => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy GroupExcept(string name, IReadOnlyList<string> excluded) => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy Groups(IReadOnlyList<string> names) => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy User(string id) => NullProxy.Instance;
        public Microsoft.AspNetCore.SignalR.IClientProxy Users(IReadOnlyList<string> ids) => NullProxy.Instance;
    }

    private sealed class NullProxy : Microsoft.AspNetCore.SignalR.IClientProxy
    {
        public static readonly NullProxy Instance = new();
        public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NullGroups : Microsoft.AspNetCore.SignalR.IGroupManager
    {
        public static readonly NullGroups Instance = new();
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default) => Task.CompletedTask;
    }
}
