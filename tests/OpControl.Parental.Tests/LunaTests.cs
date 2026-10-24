// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Hubs;
using OpControl.Parental.Server.Services;
using OpControl.Parental.Server.Services.Luna;
using OpControl.Parental.Server.Services.Luna.Providers;
using OpControl.Parental.Server.Services.Push;
using Xunit;

namespace OpControl.Parental.Tests;

public class LunaTests
{
    private static ParentalDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("luna-" + Guid.NewGuid())
            .Options);

    private static EmailRenderer NewRenderer() =>
        new(Options.Create(new EmailOptions { LogoUrl = "https://example.test/logo.png" }),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PublicBaseUrl"] = "https://example.test"
            }).Build());

    private static EmailSender NewEmailSender(ParentalDbContext db, IDataProtectionProvider protection) =>
        new(db, protection, NewRenderer(), Options.Create(new EmailOptions()), NullLogger<EmailSender>.Instance);

    private static AlertService NewAlerts(ParentalDbContext db, IDataProtectionProvider protection) =>
        new(db, new DashboardEventBus(), protection, NewEmailSender(db, protection), NewRenderer(),
            NullLogger<AlertService>.Instance);

    private static LockService NewLocks(ParentalDbContext db, AlertService alerts) =>
        new(db, new NullHubContext(), new DashboardEventBus(), new RuleService(db, alerts),
            new UsageService(db, alerts), alerts, NullLogger<LockService>.Instance);

    private sealed class FakeProviders : ILunaProviderFactory
    {
        private readonly IDataProtector _protector;
        public FakeChatProvider Chat { get; } = new();
        public FakeModerator Moderator { get; } = new();

        public FakeProviders(IDataProtectionProvider protection) =>
            _protector = protection.CreateProtector(LunaProviderFactory.KeyProtector);

        public ILunaChatProvider GetChat(string providerId) => Chat;
        public ILunaModerator GetModerator(LunaFamilySettings settings) => Moderator;
        public string? ChatKey(LunaFamilySettings settings) => Unprotect(settings.ChatKeyProtected);
        public string? ModerationKey(LunaFamilySettings settings) =>
            Unprotect(settings.ModerationKeyProtected)
            ?? (settings.ChatProvider == LunaChatProviders.OpenAi ? ChatKey(settings) : null);
        public string? Protect(string? value) => string.IsNullOrWhiteSpace(value) ? null : _protector.Protect(value.Trim());
        private string? Unprotect(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try { return _protector.Unprotect(value); } catch { return null; }
        }
    }

    private sealed class FakeChatProvider : ILunaChatProvider
    {
        public string ProviderId => "fake";
        public int Calls { get; private set; }
        public List<LunaChatRequest> Requests { get; } = [];
        public Queue<LunaChatResult> Replies { get; } = new();
        public Func<LunaChatRequest, LunaChatResult>? Handler { get; set; }

        public Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct)
        {
            Calls++;
            Requests.Add(request);
            if (request.SystemPrompt == LunaPrompts.TitlePrompt)
                return Task.FromResult(new LunaChatResult("Test chat title", 3, 2));
            if (request.SystemPrompt.StartsWith(LunaPrompts.SummaryPrompt, StringComparison.Ordinal))
                return Task.FromResult(new LunaChatResult("Summary of the chat", 5, 4));
            if (request.SystemPrompt.StartsWith(LunaPrompts.ProfilePrompt, StringComparison.Ordinal))
                return Task.FromResult(new LunaChatResult("Learner profile", 5, 4));
            if (Handler is not null) return Task.FromResult(Handler(request));
            if (Replies.Count > 0) return Task.FromResult(Replies.Dequeue());
            return Task.FromResult(new LunaChatResult("A friendly tutoring reply.", 10, 5));
        }
    }

    private sealed class FakeModerator : ILunaModerator
    {
        public int Calls { get; private set; }
        public Queue<Func<string, LunaModerationResult>> Script { get; } = new();
        public Exception? Throw { get; set; }

        public Task<LunaModerationResult> ModerateAsync(string text, CancellationToken ct)
        {
            Calls++;
            if (Throw is not null) throw Throw;
            return Task.FromResult(Script.Count > 0 ? Script.Dequeue()(text) : Safe());
        }

        public static LunaModerationResult Safe() => new(false, [], new Dictionary<string, double>());
        public static LunaModerationResult Categories(params string[] categories) =>
            new(true, categories, categories.ToDictionary(c => c, _ => 1.0));
    }

    private sealed class FakeZdr : ILunaZdrCatalog
    {
        public string? Missing { get; set; }
        public Task<string?> MissingModelAsync(string chatModel, CancellationToken ct) => Task.FromResult(Missing);
    }

    private sealed record Harness(
        ParentalDbContext Db,
        Family Family,
        Child Child,
        ParentUser Parent,
        Device Device,
        FakeProviders Providers,
        FakeZdr Zdr,
        LunaSettingsService Settings,
        LunaChatService Chat,
        LunaParentService ParentSvc,
        LunaTokenService Tokens) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static async Task<Harness> NewHarnessAsync(
        bool enabled = true, bool lastTestOk = true, int dailyLimit = 60, LunaSetup setup = LunaSetup.B,
        DateOnly? dateOfBirth = null, string chatModel = "test/chat-model")
    {
        var db = NewDb();
        var protection = new EphemeralDataProtectionProvider();
        var providers = new FakeProviders(protection);
        var zdr = new FakeZdr();
        var family = new Family { Name = "Demo" };
        var child = new Child { FamilyId = family.Id, Name = "Ana Demo", DateOfBirth = dateOfBirth };
        var parent = new ParentUser
        {
            FamilyId = family.Id,
            DisplayName = "Demo Parent",
            RoleInFamily = FamilyRole.Owner,
            UserName = "parent@example.test",
            Email = "parent@example.test"
        };
        var device = new Device { FamilyId = family.Id, ChildId = child.Id, Name = "Demo PC", TimeZoneId = "UTC" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Users.Add(parent);
        db.Devices.Add(device);
        db.LunaFamilySettings.Add(new LunaFamilySettings
        {
            FamilyId = family.Id,
            Setup = setup,
            ChatProvider = setup == LunaSetup.B ? LunaChatProviders.OpenRouter : LunaChatProviders.OpenAi,
            ChatModel = chatModel,
            ChatKeyProtected = providers.Protect("sk-test-chat-1234"),
            ModerationKeyProtected = providers.Protect("sk-test-moderation-5678"),
            LastTestUtc = DateTime.UtcNow,
            LastTestOk = lastTestOk,
            HelplineText = ""
        });
        db.ChildLunaSettings.Add(new ChildLunaSettings
        {
            FamilyId = family.Id,
            ChildId = child.Id,
            Enabled = enabled,
            DailyMessageLimit = dailyLimit,
            UnderThirteenAcknowledgedUtc = null
        });
        await db.SaveChangesAsync();

        var alerts = NewAlerts(db, protection);
        var settings = new LunaSettingsService(db, providers, zdr, NullLogger<LunaSettingsService>.Instance);
        var usage = new LunaUsageStore(db);
        var chat = new LunaChatService(db, providers, settings, usage,
            NewLocks(db, alerts), alerts, NullLogger<LunaChatService>.Instance);
        var parentSvc = new LunaParentService(db, settings, usage);
        return new Harness(db, family, child, parent, device, providers, zdr, settings, chat, parentSvc,
            new LunaTokenService(db));
    }

    private static LunaTokenContext Context(Harness h) =>
        new(Guid.NewGuid(), h.Family.Id, h.Child.Id, h.Device.Id, DateTime.UtcNow.AddMinutes(15));

    private static async Task<(LunaChatDto Chat, LunaTokenContext Ctx)> NewChatAsync(Harness h)
    {
        var ctx = Context(h);
        var chat = await h.Chat.CreateChatAsync(ctx, LunaSubjects.Math);
        return (chat, ctx);
    }

    [Fact]
    public void OpenAi_thresholds_let_school_topics_pass_and_catch_urgent()
    {
        var war = LunaSafety.ClassifyOpenAi(new Dictionary<string, double>
        {
            ["violence"] = 0.60,
            ["violence/graphic"] = 0.40
        });
        Assert.False(war.Blocked);
        Assert.False(war.Urgent);

        var sexual = LunaSafety.ClassifyOpenAi(new Dictionary<string, double> { ["sexual"] = 0.60 });
        Assert.True(sexual.Blocked);
        Assert.False(sexual.Urgent);

        var selfHarm = LunaSafety.ClassifyOpenAi(new Dictionary<string, double> { ["self-harm/intent"] = 0.31 });
        Assert.True(selfHarm.Blocked);
        Assert.True(selfHarm.Urgent);

        var minors = LunaSafety.ClassifyOpenAi(new Dictionary<string, double> { ["sexual/minors"] = 0.26 });
        Assert.True(minors.Urgent);
    }

    [Fact]
    public void Llama_guard_categories_map_to_block_and_urgent()
    {
        var selfHarm = LunaSafety.ClassifyLlamaGuard("unsafe\nS11");
        Assert.True(selfHarm.Urgent);
        Assert.True(selfHarm.Blocked);

        var sexual = LunaSafety.ClassifyLlamaGuard("unsafe\nS12");
        Assert.True(sexual.Blocked);
        Assert.False(sexual.Urgent);

        var privacy = LunaSafety.ClassifyLlamaGuard("unsafe\nS7");
        Assert.False(privacy.Blocked);

        Assert.False(LunaSafety.ClassifyLlamaGuard("safe").Blocked);
    }

    [Fact]
    public void Concern_marker_is_stripped_and_reported()
    {
        var (text, kinds) = LunaSafety.StripConcernMarkers("Let's talk to an adult.\n[[LUNA_CONCERN:self-harm]]");
        Assert.Equal("Let's talk to an adult.", text);
        Assert.Contains("self-harm", kinds);
        Assert.DoesNotContain("LUNA_CONCERN", text);
    }

    [Fact]
    public async Task Blocked_input_never_calls_chat_and_later_prompts_use_the_placeholder()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Categories("S12"));
        var (chat, ctx) = await NewChatAsync(h);

        var blocked = await h.Chat.SendAsync(ctx, chat.Id, "Something unsafe", default);
        Assert.Equal(0, h.Providers.Chat.Calls);
        Assert.Equal("Blocked", blocked.ChildMessage.Flag);
        Assert.Contains("not something I can help with", blocked.LunaMessage!.Text);
        var stored = await h.Db.LunaMessages.FirstAsync(m => m.Role == LunaChatRoles.Child && m.Flag == LunaMessageFlag.Blocked);
        Assert.Equal("S12", stored.FlagCategories);

        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        var safe = await h.Chat.SendAsync(ctx, chat.Id, "Help me with fractions", default);
        Assert.NotNull(safe.LunaMessage);
        var lastRequest = h.Providers.Chat.Requests.Last(r => r.SystemPrompt.Contains("Core rules version"));
        Assert.Contains(lastRequest.Messages, m => m.Content == LunaRemovePlaceholder.Text);
        Assert.DoesNotContain(lastRequest.Messages, m => m.Content.Contains("Something unsafe"));
    }

    [Fact]
    public async Task Moderation_failure_fails_closed_and_never_calls_chat()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Moderator.Throw = new LunaProviderException("Luna.Error.ModerationTimeout");
        var (chat, ctx) = await NewChatAsync(h);

        var result = await h.Chat.SendAsync(ctx, chat.Id, "Hello", default);
        Assert.Equal(0, h.Providers.Chat.Calls);
        Assert.True(result.Urgent is false);
        Assert.Contains("can't answer", result.Notice);
        Assert.NotNull(result.LunaMessage);
    }

    [Fact]
    public async Task Reply_blocked_retries_once_then_uses_the_fallback()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Chat.Replies.Enqueue(new LunaChatResult("Bad first reply", 10, 5));
        h.Providers.Chat.Replies.Enqueue(new LunaChatResult("Bad second reply", 10, 5));
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());               // input
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Categories("S12"));    // first output
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Categories("S12"));    // retry output
        var (chat, ctx) = await NewChatAsync(h);

        var result = await h.Chat.SendAsync(ctx, chat.Id, "Tell me a story", default);
        Assert.Equal("ReplyBlocked", result.LunaMessage!.Flag);
        Assert.Contains("think about that differently", result.LunaMessage.Text);
        Assert.Equal(2, h.Providers.Chat.Requests.Count(r => r.SystemPrompt.Contains("Core rules version")));
        var parentOnly = await h.Db.LunaMessages.FirstAsync(m => m.Role == LunaChatRoles.Luna);
        Assert.Equal("Bad first reply", parentOnly.Text);
        Assert.DoesNotContain("Bad first reply", result.LunaMessage.Text);
        Assert.Contains(await h.Db.Alerts.ToListAsync(), a => a.Type == "luna-flagged");
    }

    [Fact]
    public async Task Reply_retry_can_succeed()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Chat.Replies.Enqueue(new LunaChatResult("Bad first reply", 10, 5));
        h.Providers.Chat.Replies.Enqueue(new LunaChatResult("A safe second reply.", 10, 5));
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Categories("S12"));
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        var (chat, ctx) = await NewChatAsync(h);

        var result = await h.Chat.SendAsync(ctx, chat.Id, "Tell me a story", default);
        Assert.Equal("None", result.LunaMessage!.Flag);
        Assert.Equal("A safe second reply.", result.LunaMessage.Text);
        Assert.DoesNotContain(await h.Db.Alerts.ToListAsync(), a => a.Type == "luna-flagged");
    }

    [Fact]
    public async Task Urgent_classifier_returns_the_fixed_message_and_urgent_alert()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Categories("S11"));
        var (chat, ctx) = await NewChatAsync(h);

        var result = await h.Chat.SendAsync(ctx, chat.Id, "I want to hurt myself", default);
        Assert.Equal(0, h.Providers.Chat.Calls);
        Assert.True(result.Urgent);
        Assert.Equal("Urgent", result.ChildMessage.Flag);
        Assert.Contains("Thank you for telling me", result.LunaMessage!.Text);
        Assert.Contains("grown-up", result.LunaMessage.Text);
        Assert.DoesNotContain("parents", result.LunaMessage.Text);
        var alert = await h.Db.Alerts.SingleAsync(a => a.Type == "luna-urgent");
        Assert.NotNull(alert.ArgsJson);
    }

    [Fact]
    public async Task Urgent_marker_is_stripped_from_what_the_child_sees()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        h.Providers.Chat.Replies.Enqueue(new LunaChatResult(
            "Please talk to a trusted adult.\n[[LUNA_CONCERN:grooming]]", 10, 5));
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        var (chat, ctx) = await NewChatAsync(h);

        var result = await h.Chat.SendAsync(ctx, chat.Id, "Someone online asked me for photos", default);
        Assert.True(result.Urgent);
        Assert.DoesNotContain("LUNA_CONCERN", result.LunaMessage!.Text);
        Assert.DoesNotContain("grooming", result.LunaMessage.Text);
        var stored = await h.Db.LunaMessages.SingleAsync(m => m.Role == LunaChatRoles.Luna);
        Assert.DoesNotContain("LUNA_CONCERN", stored.Text);
        Assert.DoesNotContain("LUNA_CONCERN", stored.ShownText);
        Assert.Contains("marker:grooming", stored.FlagCategories);
    }

    [Fact]
    public async Task Three_blocks_start_a_fifteen_minute_cooldown_with_one_alert()
    {
        await using var h = await NewHarnessAsync();
        var (chat, ctx) = await NewChatAsync(h);
        for (var i = 0; i < 3; i++)
        {
            h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Categories("S12"));
            await h.Chat.SendAsync(ctx, chat.Id, "blocked " + i, default);
        }
        var usage = await h.Db.LunaUsageDays.FirstAsync();
        Assert.NotNull(usage.CooldownUntilUtc);
        Assert.True(usage.CooldownUntilUtc > DateTime.UtcNow.AddMinutes(10));

        var callsBefore = h.Providers.Chat.Calls;
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        var during = await h.Chat.SendAsync(ctx, chat.Id, "Let's learn", default);
        Assert.True(during.Cooldown);
        Assert.Contains("break", during.Notice);
        Assert.Equal(callsBefore, h.Providers.Chat.Calls);
        Assert.Equal(1, await h.Db.Alerts.CountAsync(a => a.Type == "luna-flagged"
            && a.DetailKey == "Alert.Luna.Cooldown.Detail"));
    }

    [Fact]
    public async Task Daily_limit_warning_and_limit_reached()
    {
        await using var h = await NewHarnessAsync(dailyLimit: 6);
        var (chat, ctx) = await NewChatAsync(h);
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        var first = await h.Chat.SendAsync(ctx, chat.Id, "One", default);
        Assert.Equal(5, first.MessagesLeftToday);
        Assert.Contains("5 messages left", first.Warning);

        for (var i = 0; i < 5; i++)
        {
            h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
            await h.Chat.SendAsync(ctx, chat.Id, "More " + i, default);
        }
        var callsBefore = h.Providers.Chat.Calls;
        var limited = await h.Chat.SendAsync(ctx, chat.Id, "One too many", default);
        Assert.True(limited.LimitReached);
        Assert.Contains("reached our chat limit", limited.Notice);
        var again = await h.Chat.SendAsync(ctx, chat.Id, "Still trying", default);
        Assert.True(again.LimitReached);
        Assert.Equal(callsBefore, h.Providers.Chat.Calls);
    }

    [Fact]
    public async Task Extra_grant_works_for_today_and_closes_the_request()
    {
        await using var h = await NewHarnessAsync(dailyLimit: 1);
        var (chat, ctx) = await NewChatAsync(h);
        h.Providers.Moderator.Script.Enqueue(_ => FakeModerator.Safe());
        await h.Chat.SendAsync(ctx, chat.Id, "Only one", default);
        var request = await h.Chat.CreateMoreRequestAsync(ctx, default);
        Assert.Equal("Pending", request.Status);
        Assert.Contains(await h.Db.Alerts.ToListAsync(), a => a.Type == "luna-more-request");

        var left = await h.ParentSvc.GrantExtraAsync(h.Family.Id, h.Child.Id, h.Parent.Id, 10, default);
        Assert.Equal(10, left);
        var stored = await h.Db.LunaMoreRequests.SingleAsync();
        Assert.Equal(LunaMoreRequestStatus.Granted, stored.Status);
        Assert.Equal(10, stored.GrantedAmount);

        var pending = await h.Chat.CreateMoreRequestAsync(ctx, default);
        await h.ParentSvc.DismissRequestAsync(h.Family.Id, h.Child.Id, pending.Id, h.Parent.Id, default);
        Assert.Equal(LunaMoreRequestStatus.Dismissed, (await h.Db.LunaMoreRequests.SingleAsync(r => r.Id == pending.Id)).Status);
    }

    [Fact]
    public async Task Hidden_chats_disappear_for_the_child_but_stay_for_parents()
    {
        await using var h = await NewHarnessAsync();
        var (chat, ctx) = await NewChatAsync(h);
        await h.Chat.UpdateChatAsync(ctx, chat.Id, new LunaUpdateChatRequest("My fractions", null), default);
        await h.Chat.UpdateChatAsync(ctx, chat.Id, new LunaUpdateChatRequest(null, true), default);

        Assert.Empty(await h.Chat.ListChatsAsync(ctx, default));
        var parentChats = await h.ParentSvc.ListChatsAsync(h.Family.Id, h.Child.Id, default);
        var parentChat = Assert.Single(parentChats);
        Assert.True(parentChat.HiddenByChild);
        Assert.True(parentChat.TitleSetByChild);
        Assert.Equal("My fractions", parentChat.Title);
    }

    [Fact]
    public async Task Keys_are_encrypted_never_returned_and_only_the_last_four_show()
    {
        await using var h = await NewHarnessAsync();
        var dto = await h.Settings.SaveAsync(h.Family.Id, new LunaSettingsRequest(
            "B", "openrouter", "test/chat-model", "sk-new-secret-1234", "sk-mod-5678", 100000, "Help text"), default);
        Assert.True(dto.HasChatKey);
        Assert.Equal("1234", dto.ChatKeyLast4);
        Assert.Equal("1234", dto.ModerationKeyLast4);
        var stored = await h.Db.LunaFamilySettings.SingleAsync();
        Assert.NotEqual("sk-new-secret-1234", stored.ChatKeyProtected);
        Assert.DoesNotContain("sk-new-secret-1234", stored.ChatKeyProtected!);
        Assert.Equal("sk-new-secret-1234", h.Providers.ChatKey(stored));
        Assert.False(dto.LastTestOk ?? false);
    }

    [Fact]
    public async Task Setup_validation_rules()
    {
        await using var h = await NewHarnessAsync();
        var settings = await h.Db.LunaFamilySettings.SingleAsync();
        settings.ModerationKeyProtected = null;
        await h.Db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => h.Settings.SaveAsync(h.Family.Id,
            new LunaSettingsRequest("A", "anthropic", "model", "chat-key", "", null, null), default));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => h.Settings.SaveAsync(h.Family.Id,
            new LunaSettingsRequest("B", "openai", "model", "chat-key", "", null, null), default));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => h.Settings.SaveAsync(h.Family.Id,
            new LunaSettingsRequest("B", "openrouter", "", "chat-key", "", null, null), default));
    }

    [Fact]
    public async Task Setup_test_checks_zdr_and_stores_the_result()
    {
        await using var h = await NewHarnessAsync();
        h.Zdr.Missing = "test/chat-model";
        var failed = await h.Settings.TestAsync(h.Family.Id, default);
        Assert.False(failed.Ok);
        Assert.Contains("zero-data-retention", failed.Error);
        Assert.False((await h.Db.LunaFamilySettings.SingleAsync()).LastTestOk);

        h.Zdr.Missing = null;
        var ok = await h.Settings.TestAsync(h.Family.Id, default);
        Assert.True(ok.Ok);
        var stored = await h.Db.LunaFamilySettings.SingleAsync();
        Assert.True(stored.LastTestOk);
        Assert.NotNull(stored.LastTestUtc);
    }

    [Fact]
    public async Task Enabling_requires_a_valid_setup_and_the_under_thirteen_acknowledgement()
    {
        await using var h = await NewHarnessAsync(enabled: false, lastTestOk: false, setup: LunaSetup.A, dateOfBirth: null);
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            h.ParentSvc.SaveChildAsync(h.Family.Id, h.Parent.Id, h.Child.Id,
                new ChildLunaRequest(true, 60, null, false), default));
        Assert.Contains("setup", ex.Message, StringComparison.OrdinalIgnoreCase);

        var family = await h.Db.LunaFamilySettings.SingleAsync();
        family.LastTestOk = true;
        await h.Db.SaveChangesAsync();

        var under = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            h.ParentSvc.SaveChildAsync(h.Family.Id, h.Parent.Id, h.Child.Id,
                new ChildLunaRequest(true, 60, null, false), default));
        Assert.Contains("under 13", under.Message, StringComparison.OrdinalIgnoreCase);

        var enabled = await h.ParentSvc.SaveChildAsync(h.Family.Id, h.Parent.Id, h.Child.Id,
            new ChildLunaRequest(true, 60, "Be kind", true), default);
        Assert.True(enabled.Enabled);
        Assert.NotNull(enabled.UnderThirteenAcknowledgedUtc);
    }

    [Fact]
    public async Task Tokens_are_scoped_in_both_directions_and_expire()
    {
        await using var h = await NewHarnessAsync();
        var deviceToken = "device-secret-token";
        h.Device.CredentialHash = SecretToken.Hash(deviceToken);
        h.Device.CredentialExpiresUtc = DateTime.UtcNow.AddDays(1);
        await h.Db.SaveChangesAsync();

        var (lunaToken, _) = await h.Tokens.IssueAsync(h.Device, default);
        Assert.NotNull(await h.Tokens.ValidateAsync(lunaToken, default));
        Assert.Null(await new DeviceAuthService(h.Db).ValidateAsync(lunaToken, default));
        Assert.Null(await h.Tokens.ValidateAsync(deviceToken, default));
        Assert.NotNull(await new DeviceAuthService(h.Db).ValidateAsync(deviceToken, default));

        var row = await h.Db.LunaTokens.SingleAsync();
        row.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
        await h.Db.SaveChangesAsync();
        Assert.Null(await h.Tokens.ValidateAsync(lunaToken, default));
    }

    [Fact]
    public async Task Token_issue_is_forbidden_when_disabled_or_the_setup_is_not_valid()
    {
        await using var h = await NewHarnessAsync(enabled: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Tokens.IssueAsync(h.Device, default));

        await using var h2 = await NewHarnessAsync(lastTestOk: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h2.Tokens.IssueAsync(h2.Device, default));
    }

    [Fact]
    public void Push_rules_cover_luna_types_and_permissions()
    {
        Assert.Equal("luna", PushService.Category("luna-flagged"));
        Assert.Equal("luna", PushService.Category("luna-urgent"));
        Assert.Equal("luna", PushService.Category("luna-more-request"));
        Assert.True(PushService.Wants(new PushPreference(), "luna"));
        var parent = new ParentUser { RoleInFamily = FamilyRole.Parent, Permissions = ParentPermissions.Luna };
        Assert.True(PushService.Allowed(parent, "luna"));
        var reports = new ParentUser { RoleInFamily = FamilyRole.Parent, Permissions = ParentPermissions.ViewReports };
        Assert.False(PushService.Allowed(reports, "luna"));
        Assert.True(ParentPermissions.All.HasFlag(ParentPermissions.Luna));
    }

    [Fact]
    public async Task Monthly_cap_pauses_luna_without_calling_the_provider()
    {
        await using var h = await NewHarnessAsync();
        var family = await h.Db.LunaFamilySettings.SingleAsync();
        family.MonthlyTokenCap = 10;
        h.Db.LunaUsageDays.Add(new LunaUsageDay
        {
            FamilyId = h.Family.Id,
            ChildId = h.Child.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            PromptTokens = 8,
            CompletionTokens = 5
        });
        await h.Db.SaveChangesAsync();
        var (chat, ctx) = await NewChatAsync(h);

        var result = await h.Chat.SendAsync(ctx, chat.Id, "Hello", default);
        Assert.Contains("month", result.Notice, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, h.Providers.Chat.Calls);
    }

    [Fact]
    public async Task OpenRouter_chat_and_llama_guard_requests_both_send_zdr()
    {
        var factory = new StubHttpFactory();
        var config = new LunaProviderConfig(LunaChatProviders.OpenRouter, "test/chat-model", "sk-test");
        await new OpenAiCompatibleChatProvider(factory).CompleteAsync(config,
            new LunaChatRequest("system", [new LunaChatTurn("user", "hi")], 10), default);
        await new LlamaGuardModerator(factory, "sk-test").ModerateAsync("hello", default);

        Assert.Equal(2, factory.Bodies.Count);
        foreach (var body in factory.Bodies)
        {
            using var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.GetProperty("provider").GetProperty("zdr").GetBoolean());
        }
    }

    [Fact]
    public void Provider_errors_map_to_clear_messages()
    {
        Assert.Equal("Luna.Error.BadKey", LunaProviderException.FromStatus(401, "").MessageKey);
        Assert.Equal("Luna.Error.NoCredit", LunaProviderException.FromStatus(429, "insufficient_quota").MessageKey);
        Assert.Equal("Luna.Error.RateLimited", LunaProviderException.FromStatus(429, "slow down").MessageKey);
        Assert.Equal("Luna.Error.ModelNotFound", LunaProviderException.FromStatus(404, "").MessageKey);
        Assert.Equal("Luna.Error.ProviderDown", LunaProviderException.FromStatus(503, "").MessageKey);
    }

    [Fact]
    public void Localization_has_every_luna_child_and_alert_key_in_both_languages()
    {
        var root = FindRoot();
        var en = ReadResx(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.resx"));
        var es = ReadResx(Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", "AppStrings.es-MX.resx"));
        string[] keys =
        [
            "Luna.Child.Blocked", "Luna.Child.ReplyFallback", "Luna.Child.Urgent", "Luna.Child.Limit",
            "Luna.Child.FiveLeft", "Luna.Child.Cooldown", "Luna.Child.Resting", "Luna.Child.Refusal",
            "Luna.Child.MonthlyCap", "Alert.Luna.Flagged.Title", "Alert.Luna.Urgent.Title",
            "Alert.Luna.MoreRequest.Detail", "Push.Luna.Urgent.Body", "Team.Perm.Luna"
        ];
        foreach (var key in keys)
        {
            Assert.True(en.ContainsKey(key), "missing en " + key);
            Assert.True(es.ContainsKey(key), "missing es " + key);
            Assert.False(string.IsNullOrWhiteSpace(en[key]));
            Assert.False(string.IsNullOrWhiteSpace(es[key]));
            Assert.NotEqual(en[key], es[key]);
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("root not found");
    }

    private static Dictionary<string, string> ReadResx(string path)
    {
        var document = System.Xml.Linq.XDocument.Load(path);
        return document.Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => e.Element("value")?.Value ?? "");
    }

    private sealed class StubHttpFactory : IHttpClientFactory
    {
        public List<string> Bodies { get; } = [];
        public HttpClient CreateClient(string name) => new(new CaptureHandler(Bodies));
    }

    private sealed class CaptureHandler(List<string> bodies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            bodies.Add(body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"safe\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1}}",
                    Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class NullHubContext : IHubContext<ParentalHub>
    {
        public IHubClients Clients { get; } = new NullClients();
        public IGroupManager Groups { get; } = new NullGroups();
    }

    private sealed class NullClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NullProxy();
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NullProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
