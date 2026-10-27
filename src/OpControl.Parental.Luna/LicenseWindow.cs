// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Luna;

/// <summary>The full project license, scrollable, read inside the app.</summary>
public sealed class LicenseWindow : Window
{
    private readonly LunaPalette _palette;

    public LicenseWindow(LunaPalette palette)
    {
        _palette = palette;
        Title = Loc.T("Client.Kid.About.FullLicenseTitle");
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        CanResize = true;
        ShowInTaskbar = false;
        Width = 760;
        Height = 620;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Border
        {
            CornerRadius = new CornerRadius(20),
            BorderThickness = new Thickness(2),
            BorderBrush = LunaBrushes.B(_palette.Border),
            Background = LunaBrushes.B(_palette.Bg),
            ClipToBounds = true
        };
        var grid = new Grid { RowDefinitions = new RowDefinitions("52,*") };
        grid.Children.Add(BuildTitleBar());
        var text = new SelectableTextBlock
        {
            Text = AboutContent.LicenseText,
            FontFamily = LunaThemes.Body,
            FontSize = 12.5,
            Foreground = LunaBrushes.B(_palette.Text),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(22, 12, 22, 22)
        };
        var scroll = new ScrollViewer
        {
            Content = text,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);
        root.Child = grid;
        Content = root;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        };
    }

    private Control BuildTitleBar()
    {
        var bar = new Border
        {
            Background = LunaBrushes.B(_palette.Surface),
            BorderBrush = LunaBrushes.B(_palette.Surface2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 0, 10, 0)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = new TextBlock
        {
            Text = Loc.T("Client.Kid.About.FullLicenseTitle"),
            FontFamily = LunaThemes.Display,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = LunaBrushes.B(_palette.Text),
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(title);
        var close = new Button
        {
            Content = new TextBlock
            {
                Text = "×",
                FontFamily = LunaThemes.Body,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = LunaBrushes.B(_palette.Muted)
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, Loc.T("Client.Kid.About.Close"));
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        grid.Children.Add(close);
        bar.Child = grid;
        bar.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            var onButton = (e.Source as Visual)?.GetVisualAncestors().OfType<Button>().Any() == true;
            if (!onButton) BeginMoveDrag(e);
        };
        return bar;
    }
}
