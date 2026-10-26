// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;
using OpControl.Parental.Luna;
using Xunit;

namespace OpControl.Parental.Tests;

[Collection(HeadlessUiCollection.Name)]
public class Job19FDeleteChatsTests
{
    private sealed class FakeApi : ILunaApiClient
    {
        public LunaStatusDto Status { get; set; } = new(
            true, "Emma", "night", 55, null, false, Loc.English, "", null, null);
        public List<LunaChatDto> Chats { get; } = [];
        public Dictionary<Guid, List<LunaChildMessageDto>> Messages { get; } = [];
        public int ChatListCalls { get; private set; }

        public Task ConnectAsync(bool dev, CancellationToken ct = default) => Task.CompletedTask;
        public Task<LunaStatusDto> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(Status);

        public Task<List<LunaChatDto>> GetChatsAsync(bool includeHidden = false, CancellationToken ct = default)
        {
            ChatListCalls++;
            return Task.FromResult(Chats.Where(c => includeHidden || !c.HiddenByChild).ToList());
        }

        public Task<LunaChatDto> CreateChatAsync(string subject, CancellationToken ct = default)
        {
            var chat = new LunaChatDto(Guid.NewGuid(), subject, "", false, false,
                DateTime.UtcNow, DateTime.UtcNow);
            Chats.Add(chat);
            Messages[chat.Id] = [];
            return Task.FromResult(chat);
        }

        public Task<LunaChatDto> UpdateChatAsync(Guid chatId, LunaUpdateChatRequest request, CancellationToken ct = default)
        {
            var chat = Chats.First(c => c.Id == chatId);
            chat = chat with
            {
                Title = request.Title ?? chat.Title,
                HiddenByChild = request.Hidden ?? chat.HiddenByChild
            };
            Chats[Chats.IndexOf(Chats.First(c => c.Id == chatId))] = chat;
            return Task.FromResult(chat);
        }

        public Task<List<LunaChildMessageDto>> GetMessagesAsync(Guid chatId, CancellationToken ct = default)
        {
            if (!Messages.TryGetValue(chatId, out var messages))
                throw new LunaApiException(HttpStatusCode.BadRequest, "Chat not found");
            return Task.FromResult(messages);
        }

        public Task<LunaMessageResponse> SendMessageAsync(Guid chatId, string text, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<LunaMoreRequestDto> RequestMoreAsync(CancellationToken ct = default) =>
            Task.FromResult(new LunaMoreRequestDto(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow, "Pending", null, null));

        public Task SetThemeAsync(string theme, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static LunaChatDto Chat(string title) =>
        new(Guid.NewGuid(), LunaSubjects.Math, title, false, false,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

    private static FakeApi Api(params LunaChatDto[] chats)
    {
        var api = new FakeApi();
        foreach (var chat in chats)
        {
            api.Chats.Add(chat);
            api.Messages[chat.Id] = [];
        }
        return api;
    }

    private static LunaAppView View(ILunaApiClient api)
    {
        Loc.SetLanguage(Loc.English);
        var view = new LunaAppView(api, "night");
        view.MarkInitializedForTest();
        view.InitializeForTestAsync().GetAwaiter().GetResult();
        return view;
    }

    [Fact]
    public async Task Showing_home_refreshes_the_list_and_drops_chats_deleted_on_the_server()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var first = Chat("Old chat");
            var api = Api(first);
            var view = View(api);
            var window = new Window { Width = 960, Height = 760, Content = view };
            window.Show();
            UiHarness.Pump(150);

            Assert.Equal("Home", view.ScreenForTest);
            Assert.Equal([first.Id], view.ChatsForTest.Select(c => c.Id).ToList());

            var second = Chat("New chat");
            api.Chats.Clear();
            api.Chats.Add(second);
            api.Messages[second.Id] = [];

            view.ShowHomeForTest();
            UiHarness.Pump(200);

            Assert.True(api.ChatListCalls >= 2);
            Assert.Equal([second.Id], view.ChatsForTest.Select(c => c.Id).ToList());
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Reopening_luna_refreshes_the_list()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var first = Chat("Old chat");
            var api = Api(first);
            var view = View(api);
            var window = new Window { Width = 960, Height = 760, Content = view };
            window.Show();
            UiHarness.Pump(100);

            var second = Chat("New chat");
            api.Chats.Clear();
            api.Chats.Add(second);
            api.Messages[second.Id] = [];

            view.RefreshForOpenAsync().GetAwaiter().GetResult();
            UiHarness.Pump(150);

            Assert.Equal([second.Id], view.ChatsForTest.Select(c => c.Id).ToList());
            Assert.Equal("Home", view.ScreenForTest);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Opening_a_deleted_chat_shows_home_with_a_removed_note()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
        await session.Dispatch(() =>
        {
            var chat = Chat("Deleted chat");
            var api = Api(chat);
            api.Messages.Clear();
            var view = View(api);
            var window = new Window { Width = 960, Height = 760, Content = view };
            window.Show();
            UiHarness.Pump(100);

            view.OpenChatFromListForTest(chat).GetAwaiter().GetResult();
            UiHarness.Pump(100);

            Assert.Equal("Home", view.ScreenForTest);
            Assert.Equal(Loc.T("Luna.Chat.Removed"), view.HomeNoticeForTest);
            Assert.DoesNotContain(chat.Id, view.ChatsForTest.Select(c => c.Id));
            window.Close();
        }, CancellationToken.None);
    }
}
