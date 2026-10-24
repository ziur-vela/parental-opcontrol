// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Server.Data;

namespace OpControl.Parental.Server.Services.Luna;

/// <summary>
/// Development-only demo data for the Job 17 preview. It is intentionally safe to
/// run only after <see cref="LunaFakeMode.EnsureSeedAllowed"/> has passed.
/// </summary>
public static class LunaDemoSeeder
{
    public const string DemoEmail = "parent@demo.local";
    public const string DemoPassword = "LunaDemo!2026";
    public const string OpsEmail = "preview-ops@demo.local";
    public const string PausedEmail = "paused@demo.local";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParentalDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ParentUser>>();
        var providers = scope.ServiceProvider.GetRequiredService<ILunaProviderFactory>();

        if (await db.Families.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var family = new Family { Name = "Luna Demo Family", DefaultLanguage = "en", CreatedUtc = now };
        var ana = new Child { FamilyId = family.Id, Name = "Ana", DateOfBirth = new DateOnly(2016, 3, 12), CreatedUtc = now };
        var leo = new Child { FamilyId = family.Id, Name = "Leo", DateOfBirth = new DateOnly(2018, 7, 5), CreatedUtc = now };
        var anaDevice = new Device
        {
            FamilyId = family.Id, ChildId = ana.Id, Name = "Ana laptop", TimeZoneId = "UTC",
            Platform = DevicePlatform.Windows, EnrolledUtc = now, LastSeenUtc = now, ClientVersion = "0.16.1"
        };
        var leoDevice = new Device
        {
            FamilyId = family.Id, ChildId = leo.Id, Name = "Leo tablet", TimeZoneId = "UTC",
            Platform = DevicePlatform.Windows, EnrolledUtc = now, LastSeenUtc = now, ClientVersion = "0.16.1"
        };
        db.Families.Add(family);
        db.Children.AddRange(ana, leo);
        db.Devices.AddRange(anaDevice, leoDevice);

        db.LunaFamilySettings.Add(new LunaFamilySettings
        {
            FamilyId = family.Id,
            Setup = LunaSetup.A,
            ChatProvider = LunaChatProviders.OpenRouter,
            ChatModel = "anthropic/claude-haiku-5.5",
            ChatKeyProtected = providers.Protect("sk-or-demo-hk55"),
            LastTestUtc = now.AddMinutes(-12),
            LastTestOk = true,
            LastTestError = null,
            MonthlyTokenCap = 2_000_000,
            HelplineText = "Please talk to a grown-up you trust right now.",
            UpdatedUtc = now
        });
        db.ChildLunaSettings.AddRange(
            new ChildLunaSettings
            {
                FamilyId = family.Id, ChildId = ana.Id, Enabled = true, EnabledUtc = now.AddDays(-5),
                DailyMessageLimit = 60,
                ExtraInstructions = "Ana is in 5th grade. Ana is working on fractions and times tables. Answer in Spanish unless Ana writes in English.",
                Theme = LunaThemes.Night, UpdatedUtc = now
            },
            new ChildLunaSettings
            {
                FamilyId = family.Id, ChildId = leo.Id, Enabled = false, EnabledUtc = null,
                DailyMessageLimit = 30, ExtraInstructions = "Leo is in 2nd grade. Keep answers very short and use simple words.",
                Theme = LunaThemes.Gray, UpdatedUtc = now
            });
        db.LunaLearnerProfiles.AddRange(
            new LunaLearnerProfile
            {
                FamilyId = family.Id, ChildId = ana.Id,
                Text = "5th grade. Practising times tables (7s and 8s are still slow) and adding fractions with different denominators. Loves history, especially Ancient Egypt. Often asks what new words mean. Likes short examples.",
                UpdatedUtc = now.AddDays(-1), UpdatedBy = "luna"
            },
            new LunaLearnerProfile
            {
                FamilyId = family.Id, ChildId = leo.Id,
                Text = "2nd grade. Working on addition and subtraction within 100. Likes space and dinosaurs. Prefers very short answers.",
                UpdatedUtc = now.AddDays(-2), UpdatedBy = "luna"
            });

        db.LunaUsageDays.AddRange(
            new LunaUsageDay
            {
                FamilyId = family.Id, ChildId = ana.Id, Date = today, Messages = 60,
                PromptTokens = 96_000, CompletionTokens = 19_000, UpdatedUtc = now
            },
            new LunaUsageDay
            {
                FamilyId = family.Id, ChildId = ana.Id, Date = today.AddDays(-2), Messages = 41,
                PromptTokens = 62_000, CompletionTokens = 12_000, UpdatedUtc = now
            });

        var request = new LunaMoreRequest
        {
            FamilyId = family.Id, ChildId = ana.Id, CreatedUtc = now.AddHours(-2),
            Status = LunaMoreRequestStatus.Pending
        };
        db.LunaMoreRequests.Add(request);

        var titles = new (string Subject, string Title, int MinutesAgo, bool Hidden)[]
        {
            (LunaSubjects.History, "Ancient Egypt", 18, false),
            (LunaSubjects.Science, "How volcanoes erupt", 75, false),
            (LunaSubjects.Math, "Times tables", 4, false),
            (LunaSubjects.Dictionary, "What does \"enormous\" mean?", 240, true),
            (LunaSubjects.Languages, "Spanish practice", 420, false),
            (LunaSubjects.Curious, "Why is the sky blue?", 1_500, false),
            (LunaSubjects.Curious, "Feeling worried", 1_800, false)
        };

        var chats = new Dictionary<string, LunaChat>();
        foreach (var (subject, title, minutes, hidden) in titles)
        {
            var chat = new LunaChat
            {
                FamilyId = family.Id, ChildId = ana.Id, DeviceId = anaDevice.Id, Subject = subject,
                Title = title, HiddenByChild = hidden, CreatedUtc = now.AddMinutes(-minutes - 10),
                LastMessageUtc = now.AddMinutes(-minutes), UpdatedUtc = now.AddMinutes(-minutes)
            };
            chats[title] = chat;
            db.LunaChats.Add(chat);
        }

        AddCleanExchange(db, chats["Times tables"], now.AddMinutes(-5), "Can you help me with 7 times 8?", "What do you think 7 × 8 is? Try counting in sevens: 7, 14, 21…");
        AddCleanExchange(db, chats["Spanish practice"], now.AddMinutes(-420), "How do I say \"my favourite animal is a dog\" in Spanish?", "Try: \"Mi animal favorito es un perro\". Which word means \"favourite\"?");
        AddCleanExchange(db, chats["Why is the sky blue?"], now.AddMinutes(-1_500), "Why is the sky blue?", "Sunlight has many colours. Air scatters the blue light more than the red light, so blue reaches your eyes from all over the sky. What colour do you think sunsets are, and why?");
        AddCleanExchange(db, chats["What does \"enormous\" mean?"], now.AddMinutes(-240), "What does \"enormous\" mean?", "Meaning: very, very big.\nExample: An enormous elephant stood by the river.\nSpanish: enorme\nEnglish: enormous");

        // Blocked child message + the fixed kind reply the child saw.
        AddMessage(db, chats["Ancient Egypt"], LunaChatRoles.Child, now.AddMinutes(-20),
            "Tell me a really gross bloody story about mummies", null, LunaMessageFlag.Blocked, "S12",
            """{"S12":0.97,"violence/graphic":0.91}""");
        AddMessage(db, chats["Ancient Egypt"], LunaChatRoles.Luna, now.AddMinutes(-20),
            "That's not something I can help with. Want to get back to learning? Ask me about anything for school!", null);
        AddMessage(db, chats["Ancient Egypt"], LunaChatRoles.Child, now.AddMinutes(-19),
            "OK! How did they make mummies?", null);
        AddMessage(db, chats["Ancient Egypt"], LunaChatRoles.Luna, now.AddMinutes(-18),
            "Great question! What do you think they needed to do first to keep a body from going bad? Here's a hint: it has to do with water.", null);

        // Luna's answer was blocked: the original is kept for parents, the child saw the fallback.
        AddMessage(db, chats["How volcanoes erupt"], LunaChatRoles.Child, now.AddMinutes(-77),
            "How do volcanoes erupt?", null);
        AddMessage(db, chats["How volcanoes erupt"], LunaChatRoles.Luna, now.AddMinutes(-75),
            "Here is a very unsafe answer about lava.", "Let me think about that differently. Can you ask me another way?",
            LunaMessageFlag.ReplyBlocked, "S12", """{"S12":0.88}""");

        // Urgent item, already reviewed.
        AddMessage(db, chats["Feeling worried"], LunaChatRoles.Child, now.AddMinutes(-1_805),
            "Sometimes I feel like I want to disappear.", null, LunaMessageFlag.Urgent, "S11",
            """{"S11":0.72,"self-harm/intent":0.58,"sexual/minors":0.02,"violence":0.04,"hate":0.01,"harassment":0.02,"illicit":0.01,"self-harm":0.41}""",
            reviewedUtc: now.AddMinutes(-1_790), markedFine: false);
        AddMessage(db, chats["Feeling worried"], LunaChatRoles.Luna, now.AddMinutes(-1_800),
            "Thank you for telling me. This is important. Please talk to a grown-up you trust right now, like your mom, your dad or your teacher.\n\nPlease talk to a grown-up you trust right now.",
            null);

        await db.SaveChangesAsync(ct);

        var owner = new ParentUser
        {
            FamilyId = family.Id,
            DisplayName = "Parent",
            UserName = DemoEmail,
            Email = DemoEmail,
            EmailConfirmed = true,
            RoleInFamily = FamilyRole.Owner,
            Permissions = ParentPermissions.All,
            PreferredLanguage = "en",
            CreatedUtc = now
        };
        var ownerResult = await users.CreateAsync(owner, DemoPassword);
        if (!ownerResult.Succeeded)
            throw new InvalidOperationException(AppText.Get("Luna.Error.DemoSeedFailed",
                string.Join("; ", ownerResult.Errors.Select(e => e.Description))));

        // A separate server admin closes first-run setup without adding extra nav items to the demo owner.
        var opsFamily = new Family { Name = "Preview Operations", DefaultLanguage = "en", CreatedUtc = now };
        db.Families.Add(opsFamily);
        var ops = new ParentUser
        {
            FamilyId = opsFamily.Id,
            DisplayName = "Preview Ops",
            UserName = OpsEmail,
            Email = OpsEmail,
            EmailConfirmed = true,
            RoleInFamily = FamilyRole.Owner,
            Permissions = ParentPermissions.All,
            IsServerAdmin = true,
            CreatedUtc = now
        };
        var opsResult = await users.CreateAsync(ops, DemoPassword);
        if (!opsResult.Succeeded)
            throw new InvalidOperationException(AppText.Get("Luna.Error.DemoSeedFailed",
                string.Join("; ", opsResult.Errors.Select(e => e.Description))));

        // A second family with a broken Luna setup so the Paused moon can be reviewed without breaking the main demo.
        var pausedFamily = new Family { Name = "Luna Paused Demo", DefaultLanguage = "en", CreatedUtc = now };
        var pausedChild = new Child
        {
            FamilyId = pausedFamily.Id, Name = "Ana", DateOfBirth = new DateOnly(2016, 3, 12), CreatedUtc = now
        };
        db.Families.Add(pausedFamily);
        db.Children.Add(pausedChild);
        db.LunaFamilySettings.Add(new LunaFamilySettings
        {
            FamilyId = pausedFamily.Id,
            Setup = LunaSetup.A,
            ChatProvider = LunaChatProviders.OpenRouter,
            ChatModel = "anthropic/claude-haiku-5.5",
            ChatKeyProtected = providers.Protect("sk-or-demo-paused"),
            LastTestUtc = now.AddMinutes(-3),
            LastTestOk = false,
            LastTestError = AppText.Get("Luna.Error.BadKey", "en"),
            MonthlyTokenCap = 2_000_000,
            HelplineText = "Please talk to a grown-up you trust right now.",
            UpdatedUtc = now
        });
        db.ChildLunaSettings.Add(new ChildLunaSettings
        {
            FamilyId = pausedFamily.Id, ChildId = pausedChild.Id, Enabled = true, EnabledUtc = now.AddDays(-4),
            DailyMessageLimit = 60, Theme = LunaThemes.Night, UpdatedUtc = now
        });
        db.Alerts.Add(new Alert
        {
            FamilyId = pausedFamily.Id, ChildId = pausedChild.Id, Type = "luna-setup-broken",
            Title = AppText.Get("Alert.Luna.SetupBroken.Title", "en"),
            Detail = AppText.Get("Alert.Luna.SetupBroken.Detail", "en"),
            TitleKey = "Alert.Luna.SetupBroken.Title", DetailKey = "Alert.Luna.SetupBroken.Detail",
            ArgsJson = "[]", CreatedUtc = now.AddMinutes(-3)
        });
        var pausedOwner = new ParentUser
        {
            FamilyId = pausedFamily.Id,
            DisplayName = "Parent",
            UserName = PausedEmail,
            Email = PausedEmail,
            EmailConfirmed = true,
            RoleInFamily = FamilyRole.Owner,
            Permissions = ParentPermissions.All,
            PreferredLanguage = "en",
            CreatedUtc = now
        };
        var pausedResult = await users.CreateAsync(pausedOwner, DemoPassword);
        if (!pausedResult.Succeeded)
            throw new InvalidOperationException(AppText.Get("Luna.Error.DemoSeedFailed",
                string.Join("; ", pausedResult.Errors.Select(e => e.Description))));

        await db.SaveChangesAsync(ct);
    }

    private static void AddCleanExchange(ParentalDbContext db, LunaChat chat, DateTime at, string child, string luna)
    {
        AddMessage(db, chat, LunaChatRoles.Child, at, child, null);
        AddMessage(db, chat, LunaChatRoles.Luna, at.AddSeconds(3), luna, null);
    }

    private static void AddMessage(ParentalDbContext db, LunaChat chat, string role, DateTime at, string text,
        string? shown, LunaMessageFlag flag = LunaMessageFlag.None, string? categories = null, string? scores = null,
        DateTime? reviewedUtc = null, bool markedFine = false)
    {
        db.LunaMessages.Add(new LunaMessage
        {
            FamilyId = chat.FamilyId,
            ChildId = chat.ChildId,
            ChatId = chat.Id,
            Role = role,
            Text = text,
            ShownText = shown ?? text,
            CreatedUtc = at,
            Flag = flag,
            FlagCategories = categories,
            FlagScores = scores,
            ReviewedUtc = reviewedUtc,
            MarkedFine = markedFine,
            PromptTokens = 0,
            CompletionTokens = 0
        });
    }
}
