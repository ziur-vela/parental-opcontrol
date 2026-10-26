// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using LiveMarkdown.Avalonia;
using Avalonia.Threading;

namespace OpControl.Parental.Tests;

/// <summary>Minimal Avalonia application for the manual headless session.</summary>
public sealed class UiTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add((IStyle)AvaloniaXamlLoader.Load(new Uri("avares://LiveMarkdown.Avalonia/Styles.axaml")));
        MarkdownNode.Register<MathInlineNode>();
        MarkdownNode.Register<MathBlockNode>();
    }
}

internal static class UiHarness
{
    /// <summary>Runs timers/render ticks while the UI thread is otherwise busy.</summary>
    public static void Pump(int milliseconds, Action? frame = null)
    {
        var until = Environment.TickCount64 + milliseconds;
        do
        {
            Dispatcher.UIThread.RunJobs();
            frame?.Invoke();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(8);
        } while (Environment.TickCount64 < until);
        Dispatcher.UIThread.RunJobs();
        frame?.Invoke();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
