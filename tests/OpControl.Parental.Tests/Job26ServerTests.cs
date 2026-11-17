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

public sealed class Job26ServerTests
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
        public Task<LunaChatResult> CompleteAsync(LunaProviderConfig config, LunaChatRequest request, CancellationToken ct) =>
            Task.FromResult(new LunaChatResult("Look at level 3: it needs 4 more.", 10, 5));
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

    private static async Task<Harness> NewHarnessAsync()
    {
        var db = new ParentalDbContext(new DbContextOptionsBuilder<ParentalDbContext>()
            .UseInMemoryDatabase("job26-" + Guid.NewGuid()).Options);
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
            Enabled = true,
            DailyMessageLimit = 1,
            GamesEnabled = true
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
    public async Task Ask_luna_saves_one_games_chat_per_session_for_parent_review()
    {
        await using var h = await NewHarnessAsync();
        var ctx = Context(h);

        var first = await h.Games.AskAsync(ctx, new LunaGameAskRequest("How do I make level 3?", "moon-tower",
            "Level 3 has 4 + 2."));
        Assert.False(first.Refused);
        Assert.NotNull(first.ChatId);
        var second = await h.Games.AskAsync(ctx, new LunaGameAskRequest("And then?", "moon-tower",
            "Level 3 has 4 + 2.", first.ChatId));
        Assert.False(second.Refused);
        Assert.Equal(first.ChatId, second.ChatId);

        var chat = Assert.Single(await h.Db.LunaChats.ToListAsync());
        Assert.Equal(LunaSubjects.Games, chat.Subject);
        Assert.Equal("Moon Tower questions", chat.Title);

        var list = await h.ParentSvc.ListChatsAsync(h.Family.Id, h.Child.Id);
        var row = Assert.Single(list);
        Assert.Equal(chat.Id, row.Id);
        Assert.Equal("Luna.Subject.Games", LunaUi.Subject(row.Subject).NameKey);

        var review = await h.ParentSvc.GetChatAsync(h.Family.Id, h.Child.Id, chat.Id);
        Assert.Equal(4, review.Messages.Count);
        Assert.Equal("How do I make level 3?", review.Messages[0].Text);
        Assert.Equal("Look at level 3: it needs 4 more.", review.Messages[1].ShownText);
        Assert.Equal("And then?", review.Messages[2].Text);

        var usage = await h.Db.LunaUsageDays.FirstAsync(u => u.ChildId == h.Child.Id);
        Assert.Equal(0, usage.Messages);
    }

    [Fact]
    public async Task Blocked_ask_is_saved_with_the_blocked_flag_and_visible_to_parents()
    {
        await using var h = await NewHarnessAsync();
        h.Providers.Moderator.Script.Enqueue(FakeModerator.Categories("S12"));

        var reply = await h.Games.AskAsync(Context(h),
            new LunaGameAskRequest("Something unsafe", "moon-tower", ""));
        Assert.True(reply.Refused);
        Assert.NotNull(reply.ChatId);

        var chat = Assert.Single(await h.Db.LunaChats.ToListAsync());
        var review = await h.ParentSvc.GetChatAsync(h.Family.Id, h.Child.Id, chat.Id);
        Assert.Equal(2, review.Messages.Count);
        Assert.Equal("Blocked", review.Messages[0].Flag);
        Assert.Equal("Something unsafe", review.Messages[0].Text);
        Assert.Equal(chat.Id, reply.ChatId);
        Assert.Equal("Moon Tower questions", chat.Title);
    }
}
