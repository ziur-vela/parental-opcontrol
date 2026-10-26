// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using OpControl.Parental.Contracts;
using OpControl.Parental.Luna;
using OpControl.Parental.Server.Data;
using OpControl.Parental.Server.Services;
using Xunit;

namespace OpControl.Parental.Tests;

public class Job19DSmallFixTests
{
    [Fact]
    public void Blocked_and_reply_blocked_results_do_not_keep_the_above_input_notice()
    {
        var chatId = Guid.NewGuid();
        var child = Message(chatId, LunaChatRoles.Child, "blocked question");
        var luna = Message(chatId, LunaChatRoles.Luna, "That's not something I can help with.");
        var blocked = new LunaMessageResponse(chatId, child, luna, 55, false, false, false,
            "That's not something I can help with.", null);
        Assert.Null(LunaAppView.NoticeAfterSend(blocked));

        var replyBlocked = new LunaMessageResponse(chatId, child, luna, 55, false, false, false, null, null);
        Assert.Null(LunaAppView.NoticeAfterSend(replyBlocked));
    }

    [Fact]
    public void Luna_send_uses_the_notice_guard_and_clears_it_on_the_next_send()
    {
        var root = FindRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        Assert.Contains("_notice = NoticeAfterSend(response);", source, StringComparison.Ordinal);
        Assert.Contains("_notice = null;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Real_error_and_limit_notices_still_show()
    {
        var chatId = Guid.NewGuid();
        var child = Message(chatId, LunaChatRoles.Child, "question");
        var error = new LunaMessageResponse(chatId, child, null, 55, false, false, false,
            "Luna can't answer right now", null);
        Assert.Equal("Luna can't answer right now", LunaAppView.NoticeAfterSend(error));

        var limit = new LunaMessageResponse(chatId, child, null, 0, true, false, false,
            "Luna is resting until tomorrow", null);
        Assert.Equal("Luna is resting until tomorrow", LunaAppView.NoticeAfterSend(limit));
    }

    [Fact]
    public void Luna_dashboard_times_use_the_family_time_zone()
    {
        var utc = new DateTime(2026, 10, 26, 20, 43, 0, DateTimeKind.Utc);
        var zone = DashboardTime.Resolve("America/Hermosillo");
        var local = DashboardTime.Convert(utc, zone);
        Assert.Equal(13, local.Hour);
        Assert.Equal(43, local.Minute);
        Assert.Contains("13:43", DashboardTime.Format(utc, zone));
    }

    [Fact]
    public async Task Family_zone_uses_the_first_device_time_zone()
    {
        await using var db = new ParentalDbContext(
            new DbContextOptionsBuilder<ParentalDbContext>()
                .UseInMemoryDatabase("job19d-tz-" + Guid.NewGuid()).Options);
        var family = new Family { Name = "TZ" };
        var child = new Child { FamilyId = family.Id, Name = "Emma" };
        db.Families.Add(family);
        db.Children.Add(child);
        db.Devices.Add(new Device
        {
            FamilyId = family.Id,
            ChildId = child.Id,
            Name = "Yoga",
            TimeZoneId = "America/Hermosillo"
        });
        await db.SaveChangesAsync();

        var zone = await DashboardTime.FamilyZoneAsync(db, family.Id);
        Assert.Equal(TimeSpan.FromHours(-7), zone.BaseUtcOffset);
    }

    [Fact]
    public void Luna_pages_no_longer_format_dates_with_the_utc_constant()
    {
        var root = FindRoot();
        foreach (var page in new[] { "LunaChild.razor", "LunaReview.razor", "LunaSetup.razor" })
        {
            var source = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
                "Components", "Pages", page));
            Assert.Contains("DashboardTime.Format", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Utc)",
                source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Other_family_pages_use_the_dashboard_helper_instead_of_server_local_time()
    {
        var root = FindRoot();
        var folder = Path.Combine(root, "src", "OpControl.Parental.Server", "Components", "Pages");
        foreach (var page in new[] { "Team.razor", "Devices.razor", "Home.razor", "Downloads.razor", "ServerAdmin.razor", "ServerUpdates.razor" })
        {
            var source = File.ReadAllText(Path.Combine(folder, page));
            Assert.Contains("DashboardTime.", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".ToLocalTime()", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Messages_tile_uses_a_localized_left_today_label()
    {
        var root = FindRoot();
        var child = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Pages", "LunaChild.razor"));
        Assert.Contains("Luna.Child.MessagesLeftToday", child, StringComparison.Ordinal);

        var en = ReadResx(root, "AppStrings.resx");
        var es = ReadResx(root, "AppStrings.es-MX.resx");
        Assert.Equal("{0} left today", en["Luna.Child.MessagesLeftToday"]);
        Assert.Equal("Quedan {0} hoy", es["Luna.Child.MessagesLeftToday"]);
        Assert.Equal("55 left today", string.Format(en["Luna.Child.MessagesLeftToday"], 55));
        Assert.Equal("Quedan 55 hoy", string.Format(es["Luna.Child.MessagesLeftToday"], 55));
    }

    [Fact]
    public void Missing_date_of_birth_points_to_the_children_page()
    {
        var root = FindRoot();
        var child = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Pages", "LunaChild.razor"));
        Assert.Contains("/children#child-", child, StringComparison.Ordinal);
        Assert.Contains("Luna.Child.OpenChildren", child, StringComparison.Ordinal);

        var children = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Server",
            "Components", "Pages", "Children.razor"));
        Assert.Contains("@page \"/children\"", children, StringComparison.Ordinal);
        Assert.Contains("DateOfBirth", children, StringComparison.Ordinal);
        Assert.Contains("Children.Save", children, StringComparison.Ordinal);

        var en = ReadResx(root, "AppStrings.resx");
        var es = ReadResx(root, "AppStrings.es-MX.resx");
        Assert.Contains("{0}", en["Luna.Child.NoDobInfo"], StringComparison.Ordinal);
        Assert.Contains("Children", en["Luna.Child.NoDobInfo"], StringComparison.Ordinal);
        Assert.Contains("{0}", es["Luna.Child.NoDobInfo"], StringComparison.Ordinal);
        Assert.Contains("Niños", es["Luna.Child.NoDobInfo"], StringComparison.Ordinal);
        Assert.Equal("Niños", es["Nav.Children"]);
    }

    private static LunaChildMessageDto Message(Guid chatId, string role, string text) =>
        new(Guid.NewGuid(), chatId, role, text, DateTime.UtcNow, "none");

    private static Dictionary<string, string> ReadResx(string root, string name)
    {
        var path = Path.Combine(root, "src", "OpControl.Parental.Server", "Resources", name);
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => e.Element("value")?.Value ?? "");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
