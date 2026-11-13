// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace OpControl.Parental.Luna;

/// <summary>Swaps between the web-search animation and the normal thinking animation.</summary>
public sealed class LunaWaitingView : ContentControl
{
    private readonly ThinkingView _thinking;
    private readonly LunaSearchingView _searching;
    private bool _searchingShown;

    public LunaWaitingView(LunaPalette palette)
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        _thinking = new ThinkingView(palette);
        _searching = new LunaSearchingView(palette);
        Content = _thinking;
    }

    internal bool SearchingShown => _searchingShown;

    public void ShowSearching()
    {
        if (_searchingShown) return;
        _searchingShown = true;
        Dispatcher.UIThread.Post(() =>
        {
            _thinking.Stop();
            Content = _searching;
        });
    }

    public void ShowThinking()
    {
        if (!_searchingShown) return;
        _searchingShown = false;
        Dispatcher.UIThread.Post(() =>
        {
            _searching.Stop();
            Content = _thinking;
        });
    }

    public void Stop()
    {
        _thinking.Stop();
        _searching.Stop();
    }
}
