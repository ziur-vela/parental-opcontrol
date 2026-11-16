// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpControl.Parental.Contracts;
using OpControl.Parental.Contracts.Games;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using OpControl.Parental.Server.Services.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

public sealed class Job23ServerTests
{
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
        public int Calls { get; private set; }
        public List<LunaChatRequest> Requests { get; } = [];
        public Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct)
        {
            Calls++;
            Requests.Add(request);
            return Task.FromResult(new LunaChatResult("Look at level 3: it needs 4 more.", 10, 5));
        }
    }

    private sealed class FakeModerator : ILunaModerator
    {
        public Queue<LunaModerationResult> Script { get; } = new();
        public Task<LunaModerationResult> ModerateAsync(string text, CancellationToken ct) =>
            Task.FromResult(Script.Count > 0 ? Script.Dequeue() : Safe());
        public static LunaModerationResult Safe() => new(false, [], new Dictionary<string, double>());
        public static LunaModerationResult Categories(params string[] categories) =>
            new(true, categories, categories.ToDictionary(c => c, _ => 1.0));
    }

    private sealed class FakeZdr : ILunaZdrCatalog
    {
        public Task<string?> MissingModelAsync(string chatModel, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private sealed record Harness(ParentalDbContext Db, Family Family, Child Child, Device Device,
        FakeProviders Providers, LunaGameService Games, LunaParentService ParentSvc) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static async Task<Harness> NewHarnessAsync(bool gamesEnabled = true, bool enabled = true)
    {
        var db = new ParentalDbContext(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job23-" + Guid.NewGuid()).Options);
        var protection = new EphemeralDataProtectionProvider();
        var providers = new FakeProviders(protection);
        var family = new Family { Name = "Demo" };
        var child = new Child { FamilyId = family.Id, Name = "Ana Demo" };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "PC", TimeZoneId = "UTC" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(device);
        db.LunaFamilySettings.Add(new LunaFamilySettings
        {
            FamilyId = family.Id,
            Setup = LunaSetup.A,
            ChatProvider = LunaChatProviders.OpenRouter,
            ChatModel = "test/chat-model",
            ChatKeyProtected = providers.Protect("sk-test-1234"),
            LastTestUtc = DateTime.UtcNow,
            LastTestOk = true,
            HelplineText = ""
        });
        db.ChildLunaSettings.Add(new ChildLunaSettings
        {
            FamilyId = family.Id,
            ChildId = child.Id,
            Enabled = enabled,
            DailyMessageLimit = 1,
            GamesEnabled = gamesEnabled
        });
        await db.SaveChangesAsync();

        var alerts = new AlertService(db, new DashboardEventBus(), protection,
            new EmailSender(db, protection, new EmailRenderer(
                    Microsoft.Extensions.Options.Options.Create(new EmailOptions()),
                    new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
                Microsoft.Extensions.Options.Options.Create(new EmailOptions()), NullLogger<EmailSender>.Instance),
            new EmailRenderer(Microsoft.Extensions.Options.Options.Create(new EmailOptions()),
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
            NullLogger<AlertService>.Instance);
        var settings = new LunaSettingsService(db, providers, new FakeZdr(), NullLogger<LunaSettingsService>.Instance, alerts);
        var usage = new LunaUsageStore(db);
        var games = new LunaGameService(db, providers, settings, usage, alerts, NullLogger<LunaGameService>.Instance);
        var parentSvc = new LunaParentService(db, settings, usage);
        return new Harness(db, family, child, device, providers, games, parentSvc);
    }

    private static LunaTokenContext Context(Harness h) =>
        new(Guid.NewGuid(), h.Family.Id, h.Child.Id, h.Device.Id, DateTime.UtcNow.AddMinutes(15));

    [Fact]
    public async Task Result_is_recorded_and_appears_in_the_weekly_summary()
    {
        await using var h = await NewHarnessAsync();
        var result = await h.Games.RecordResultAsync(Context(h),
            new LunaGameResultRequest(LunaGameCatalog.MoonTower, LunaGameModes.Make10, "hard", "win", 37, 300));

        Assert.Equal("win", result.Result);
        Assert.Equal(37, result.Score);
        var summary = await h.Games.SummaryAsync(h.Family.Id, h.Child.Id);
        Assert.Equal(1, summary.Played);
        Assert.Equal(1, summary.Wins);
        Assert.Equal(5, summary.Minutes);
        Assert.Single(await h.Games.RecentAsync(h.Family.Id, h.Child.Id));
    }

    [Fact]
    public async Task Games_switch_defaults_on_and_can_be_turned_off()
    {
        await using var h = await NewHarnessAsync();
        var child = await h.ParentSvc.GetChildAsync(h.Family.Id, h.Child.Id);
        Assert.True(child.GamesEnabled);

        var saved = await h.ParentSvc.SaveChildAsync(h.Family.Id, Guid.NewGuid(), h.Child.Id,
            new ChildLunaRequest(true, 60, null, false, false, GamesEnabled: false));
        Assert.False(saved.GamesEnabled);

        var row = await h.Db.ChildLunaSettings.FirstAsync(s => s.ChildId == h.Child.Id);
        Assert.False(row.GamesEnabled);
    }

    [Fact]
    public async Task Ask_Luna_counts_tokens_but_not_the_daily_message_limit()
    {
        await using var h = await NewHarnessAsync();
        var ctx = Context(h);
        var today = LunaUsageStore.LocalDate(DateTime.UtcNow, TimeZoneInfo.Utc);
        h.Db.LunaUsageDays.Add(new LunaUsageDay
        {
            FamilyId = h.Family.Id, ChildId = h.Child.Id, Date = today, Messages = 1
        });
        await h.Db.SaveChangesAsync();

        var reply = await h.Games.AskAsync(ctx, new LunaGameAskRequest("How do I make level 3?", "moon-tower",
            "Level 3 has 4 + 2."));

        Assert.False(reply.Refused);
        Assert.Contains("level 3", reply.Text);
        Assert.Equal(1, h.Providers.Chat.Calls);
        var request = h.Providers.Chat.Requests[0];
        Assert.Contains("Moon Tower", request.SystemPrompt);
        Assert.Contains("Current game state", request.SystemPrompt);
        var usage = await h.Db.LunaUsageDays.FirstAsync(u => u.ChildId == h.Child.Id);
        Assert.Equal(1, usage.Messages);
        Assert.Equal(10, usage.PromptTokens);
        Assert.Equal(5, usage.CompletionTokens);
    }

    [Fact]
    public async Task Blocked_game_question_never_reaches_the_provider()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Moderator.Script.Enqueue(FakeModerator.Categories("S12"));

        var reply = await h.Games.AskAsync(Context(h),
            new LunaGameAskRequest("Something unsafe", "moon-tower", ""));

        Assert.True(reply.Refused);
        Assert.Equal(0, h.Providers.Chat.Calls);
    }

    [Fact]
    public async Task Results_are_rejected_when_games_are_off()
    {
        await using var h = await NewHarnessAsync(gamesEnabled: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Games.RecordResultAsync(Context(h),
            new LunaGameResultRequest(LunaGameCatalog.MoonTower, LunaGameModes.Make10, "easy", "draw", 0, 60)));
    }

    [Fact]
    public async Task Invalid_game_or_mode_is_rejected()
    {
        await using var h = await NewHarnessAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Games.RecordResultAsync(Context(h),
            new LunaGameResultRequest("not-a-game", LunaGameModes.Make10, "easy", "win", 0, 60)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Games.RecordResultAsync(Context(h),
            new LunaGameResultRequest(LunaGameCatalog.MoonTower, LunaGameModes.Fractions, "easy", "win", 0, 60)));
    }
}
