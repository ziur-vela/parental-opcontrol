// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using OpControl.Parental.Localization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace OpControl.Parental.Luna;

/// <summary>Option 7 from the thinking design: a phase moon plus a rolling letter wave.</summary>
public sealed class ThinkingView : UserControl
{
    private readonly DispatcherTimer _timer;
    private readonly TextBlock[] _letters;
    private readonly Ellipse _shade;
    private readonly TranslateTransform _shadeMove = new();
    private readonly TranslateTransform[] _letterMove;
    private readonly double[] _letterPhase;
    private double _time;

    public ThinkingView(LunaPalette palette)
    {
        var root = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var moon = new Canvas { Width = 30, Height = 30, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        moon.Children.Add(new Ellipse
        {
            Width = 30,
            Height = 30,
            Fill = new SolidColorBrush(Color.Parse("#f6dc7e")),
            Stroke = palette.Key == "gray" ? new SolidColorBrush(Color.Parse("#e2bd52")) : null,
            StrokeThickness = palette.Key == "gray" ? 2 : 0
        });
        _shade = new Ellipse
        {
            Width = 30,
            Height = 30,
            Fill = new SolidColorBrush(palette.Key == "gray" ? Color.Parse("#d0d4dd") : Color.Parse("#3a2f7a")),
            RenderTransform = _shadeMove
        };
        moon.Children.Add(_shade);
        root.Children.Add(moon);

        var word = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 0, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var text = Loc.T("Luna.Chat.Thinking");
        _letters = new TextBlock[text.Length];
        _letterMove = new TranslateTransform[text.Length];
        _letterPhase = new double[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var letter = new TextBlock
            {
                Text = text[i].ToString(),
                FontFamily = LunaThemes.Body,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(palette.Text),
                Opacity = 0.55,
                RenderTransform = _letterMove[i] = new TranslateTransform()
            };
            _letters[i] = letter;
            _letterPhase[i] = i * 0.13;
            word.Children.Add(letter);
        }
        root.Children.Add(word);
        Content = new Border
        {
            Child = root,
            Background = new SolidColorBrush(palette.LunaBubble),
            BorderBrush = new SolidColorBrush(palette.LunaBubbleBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 9, 12, 9),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        _time += 0.04;
        var phase = (_time / 2.4) % 1;
        // The dark side slides across the moon, then eases back.
        var x = phase < 0.5 ? -30 + 60 * (phase / 0.5) : 30 - 60 * ((phase - 0.5) / 0.5);
        _shadeMove.X = x;
        for (var i = 0; i < _letters.Length; i++)
        {
            var wave = 0.5 + 0.5 * Math.Sin((_time * 2 * Math.PI / 1.6) - _letterPhase[i]);
            _letters[i].Opacity = 0.45 + 0.55 * wave;
            _letterMove[i].Y = -2 * wave;
        }
    }

    public void Stop()
    {
        _timer.Stop();
        _shadeMove.X = 0;
        foreach (var letter in _letters) letter.Opacity = 1;
    }
}
