// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;
using LunaPalettes = OpControl.Parental.Luna.LunaThemes;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job189AvatarTests
{
    [Fact]
    public void Avatar_choice_is_saved_and_reloaded()
    {
        var directory = Path.Combine(Path.GetTempPath(), "job189-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "kid-avatar.txt");
        try
        {
            Assert.Equal(LunaAvatarKeys.Face, KidAvatarStore.ReadLocal(path));
            Assert.Equal(LunaAvatarKeys.Face, KidAvatarStore.ReadLocal());

            KidAvatarStore.WriteLocal(LunaAvatarKeys.Cat, path);
            Assert.Equal(LunaAvatarKeys.Cat, KidAvatarStore.ReadLocal(path));
            Assert.Equal("cat", File.ReadAllText(path).Trim());

            KidAvatarStore.WriteLocal("not-an-avatar", path);
            Assert.Equal(LunaAvatarKeys.Face, KidAvatarStore.ReadLocal(path));

            Assert.Equal(LunaAvatarKeys.Dog, LunaAvatarKeys.Normalize("dog"));
            Assert.Equal(LunaAvatarKeys.Face, LunaAvatarKeys.Normalize("dragon"));
            Assert.All(LunaAvatarKeys.All, key => Assert.True(LunaAvatarKeys.IsValid(key)));
            Assert.Equal(8, LunaAvatarKeys.All.Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Every_avatar_lays_out_at_100_to_200_percent_scaling()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var panel = new WrapPanel { Width = 420 };
            foreach (var key in LunaAvatarKeys.All)
                panel.Children.Add(LunaAvatars.View(key, LunaAvatars.ChatSize, LunaPalettes.Night));

            var window = new Window { Width = 460, Height = 140, Content = panel };
            window.Show();
            foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            {
                window.SetRenderScaling(scale);
                UiHarness.Pump(80);
                foreach (var key in LunaAvatarKeys.All)
                {
                    var avatar = panel.GetVisualDescendants().OfType<Canvas>()
                        .First(c => c.Name == "Avatar_" + key);
                    Assert.InRange(avatar.Bounds.Width, LunaAvatars.ChatSize - 1, LunaAvatars.ChatSize + 1);
                    Assert.InRange(avatar.Bounds.Height, LunaAvatars.ChatSize - 1, LunaAvatars.ChatSize + 1);
                    Assert.NotEmpty(avatar.Children);
                }
            }
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Child_bubbles_are_mint_with_a_meta_line_and_an_avatar()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            Loc.SetLanguage(Loc.English);
            foreach (var (theme, background, ink) in new[]
                     {
                         ("night", "#3ddc97", "#15103a"),
                         ("gray", "#14845a", "#ffffff")
                     })
            {
                var api = new LunaApiClient();
                var view = new LunaAppView(api, theme);
                view.MarkInitializedForTest();
                view.SetAvatarForTest(LunaAvatarKeys.Face);
                var window = new Window
                {
                    Width = 960,
                    Height = 760,
                    WindowDecorations = WindowDecorations.None,
                    Content = view
                };
                window.Show();
                UiHarness.Pump(200);
                var chat = new LunaChatDto(Guid.NewGuid(), "math", "Fractions", false, false,
                    DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
                view.OpenChatForTest(chat,
                [
                    new LunaChildMessageDto(Guid.NewGuid(), chat.Id, LunaChatRoles.Child,
                        "Hello Luna", DateTime.UtcNow.AddMinutes(-1), "none")
                ]);
                UiHarness.Pump(500);

                var text = view.GetVisualDescendants().OfType<TextBlock>()
                    .First(t => t.Text == "Hello Luna");
                var bubble = text.GetVisualAncestors().OfType<Border>().First();
                var backgroundBrush = Assert.IsAssignableFrom<ISolidColorBrush>(bubble.Background);
                Assert.Equal(Color.Parse(background), backgroundBrush.Color);
                var inkBrush = Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground);
                Assert.Equal(Color.Parse(ink), inkBrush.Color);
                Assert.Equal(FontWeight.Bold, text.FontWeight);

                var bubbleTexts = bubble.GetVisualDescendants().OfType<TextBlock>().ToList();
                Assert.Contains(bubbleTexts, t => t.Text is not null && t.Text.StartsWith("You · "));
                Assert.Contains(bubbleTexts, t => t.Text == Loc.T("Client.Messages.Status.Sent"));

                var avatar = view.GetVisualDescendants().OfType<Button>()
                    .First(b => b.Name == "ChildAvatar");
                Assert.Equal(LunaAvatars.ChatSize, avatar.Bounds.Width, 1);
                Assert.NotNull(avatar.GetVisualDescendants().OfType<Canvas>()
                    .FirstOrDefault(c => c.Name == "Avatar_face"));
                Assert.Equal(LunaAvatarKeys.Face, view.AvatarKeyForTest);

                window.Close();
                api.Dispose();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public void The_avatar_picker_and_store_are_wired_to_the_child_bubble()
    {
        var root = FindRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAppView.cs"));
        var avatars = File.ReadAllText(Path.Combine(root, "src", "OpControl.Parental.Luna", "LunaAvatars.cs"));
        Assert.Contains("ShowAvatarPicker", view, StringComparison.Ordinal);
        Assert.Contains("LunaAvatarKeys.All", view, StringComparison.Ordinal);
        Assert.Contains("KidAvatarStore.ReadLocal()", view, StringComparison.Ordinal);
        Assert.Contains("KidAvatarStore.WriteLocal(_avatarKey)", view, StringComparison.Ordinal);
        Assert.Contains("ChatSize = 34", avatars, StringComparison.Ordinal);
        Assert.Contains("LunaAvatarKeys.Robot", avatars, StringComparison.Ordinal);
        Assert.Contains("LunaAvatarKeys.Planet", avatars, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpControl.Parental.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
