using Path = Avalonia.Controls.Shapes.Path;
// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Luna;

/// <summary>
/// Approved "Investigating on the Web…" design: the moon in a deerstalker,
/// a blinking eye and a big static magnified eye; only the glass and hand wobble.
/// </summary>
public sealed class LunaSearchingView : UserControl
{
    private readonly DispatcherTimer? _timer;
    private readonly Ellipse _blink;
    private readonly Canvas _magnifier;
    private readonly TranslateTransform _magnifierMove = new();
    private readonly RotateTransform _magnifierTurn;
    private readonly Path _shine;
    private readonly TextBlock[] _letters;
    private readonly TranslateTransform[] _letterMove;
    private readonly double[] _letterPhase;
    private readonly bool _reduced;
    private double _time;

    public LunaSearchingView(LunaPalette palette)
    {
        _reduced = LunaMotion.ReducedMotion;
        var gray = palette.Key == "gray";
        var moon = new Canvas { Width = 46, Height = 56, VerticalAlignment = VerticalAlignment.Center };
        var face = new Canvas { Width = 44, Height = 54, RenderTransform = new TranslateTransform(1, 1) };
        moon.Children.Add(face);

        var ink = Brush("#2b2f3a");
        var moonFill = Brush("#f6dc7e");
        var moonStroke = gray ? Brush("#e2bd52") : null;
        var freckle = Brush("#ecc964");

        face.Children.Add(new Ellipse
        {
            Width = 34, Height = 34, Fill = moonFill, Stroke = moonStroke, StrokeThickness = gray ? 1.5 : 0,
            Margin = new Thickness(3, 17, 0, 0)
        });
        Add(face, new Ellipse { Width = 5.2, Height = 5.2, Fill = freckle, Margin = new Thickness(7.4, 23.4, 0, 0) });

        var hat = new Canvas { Width = 44, Height = 40, RenderTransform = new RotateTransform(-8, 20, 16) };
        var clip = Geometry.Parse("M5 23 Q6 6 20 5 Q34 6 35 22 Q20 19 5 23 Z");
        var hatBody = new Path { Data = clip, Fill = Brush("#9b7a50") };
        hat.Children.Add(hatBody);
        hat.Children.Add(PathShape("M6 22 q-6 1 -7 5 q5 1 9 -2 Z", "#7d5f3b", "#5a4128", 0.8));
        hat.Children.Add(PathShape("M33 21 q7 1 9 5 q-6 1 -10 -1 Z", "#7d5f3b", "#5a4128", 0.8));
        hat.Children.Add(PathShape("M7 22 q-2 6 2 9 q2 -3 1 -8 Z", "#8f6f47", "#5a4128", 0.7));
        var stripes = new Canvas { Width = 44, Height = 54, Clip = clip, Opacity = 0.75 };
        stripes.Children.Add(PathShape("M0 11 H40 M0 16 H40 M11 4 V24 M17 4 V24 M23 4 V24 M29 4 V24", null, "#6b4f30", 0.9));
        var lightStripes = new Canvas { Width = 44, Height = 54, Clip = clip, Opacity = 0.6 };
        lightStripes.Children.Add(PathShape("M0 13.5 H40 M0 18.5 H40 M14 4 V24 M20 4 V24 M26 4 V24", null, "#c9a46f", 0.5));
        hat.Children.Add(stripes);
        hat.Children.Add(lightStripes);
        hat.Children.Add(PathShape("M5 23 Q6 6 20 5 Q34 6 35 22 Q20 19 5 23 Z", null, "#5a4128", 1));
        hat.Children.Add(PathShape("M17 5 q3 -4 6 0", null, "#5a4128", 1.4));
        Add(hat, new Ellipse { Width = 2.8, Height = 2.8, Fill = Brush("#5a4128"), Margin = new Thickness(18.6, 3.6, 0, 0) });
        face.Children.Add(hat);
        Add(face, new Ellipse { Width = 4, Height = 4, Fill = freckle, Margin = new Thickness(14, 42, 0, 0) });

        _blink = new Ellipse
        {
            Width = 4.4, Height = 5.6, Fill = ink, Margin = new Thickness(12.8, 29.2, 0, 0)
        };
        face.Children.Add(_blink);
        face.Children.Add(PathShape("M16 41 q4 3 8 0", null, "#2b2f3a", 2));
        Add(face, new Ellipse { Width = 7.2, Height = 9.2, Fill = ink, Margin = new Thickness(21.4, 27.4, 0, 0) });
        Add(face, new Ellipse { Width = 2.6, Height = 2.6, Fill = Brushes.White, Margin = new Thickness(25, 28.8, 0, 0) });

        _magnifierTurn = new RotateTransform(0, 28, 32);
        var magGroup = new TransformGroup();
        magGroup.Children.Add(new TranslateTransform(-3, 0));
        magGroup.Children.Add(_magnifierTurn);
        magGroup.Children.Add(_magnifierMove);
        _magnifier = new Canvas { Width = 44, Height = 54, RenderTransform = magGroup };
        var blue = gray ? "#1559b3" : "#1f6fd1";
        var glass = gray ? "#1f6fd1" : "#3d8bff";
        _magnifier.Children.Add(Line(33.5, 38.5, 41, 48, blue, 4));
        _magnifier.Children.Add(new Ellipse
        {
            Width = 17, Height = 17, Fill = Brush(gray ? "#cfe6ff" : "#cfe6ff", gray ? 0.55 : 0.45),
            Stroke = Brush(glass), StrokeThickness = 2.6, Margin = new Thickness(19.5, 23.5, 0, 0)
        });
        var hand = new Canvas { Width = 16, Height = 16, RenderTransform = new RotateTransform(-38, 37.6, 44) };
        hand.Children.Add(new Ellipse
        {
            Width = 8.4, Height = 6.4, Fill = moonFill, Stroke = Brush(gray ? "#c99a1f" : "#d9b13f"), StrokeThickness = 1,
            Margin = new Thickness(33.4, 40.8, 0, 0)
        });
        hand.Children.Add(PathShape("M35.6 42.2 v3.6 M37.6 41.6 v4.6 M39.6 42.2 v3.6", null, gray ? "#c99a1f" : "#d9b13f", 0.8));
        hand.Children.Add(PathShape("M33.6 43 q-1.6 1.4 .2 2.8", "#f6dc7e", gray ? "#c99a1f" : "#d9b13f", 1));
        _magnifier.Children.Add(hand);
        _shine = PathShape("M23 29 a6 6 0 0 1 4 -3.6", null, "#ffffff", 1.6);
        _shine.Opacity = 0.45;
        _magnifier.Children.Add(_shine);
        face.Children.Add(_magnifier);

        var root = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        root.Children.Add(moon);
        var word = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        var text = Loc.T("Luna.Chat.Investigating");
        _letters = new TextBlock[text.Length];
        _letterMove = new TranslateTransform[text.Length];
        _letterPhase = new double[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var letter = new TextBlock
            {
                Text = text[i].ToString(), FontFamily = LunaThemes.Body, FontSize = 14,
                FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(palette.Text), Opacity = 0.55,
                RenderTransform = _letterMove[i] = new TranslateTransform()
            };
            _letters[i] = letter;
            _letterPhase[i] = i * 0.13;
            word.Children.Add(letter);
        }
        root.Children.Add(word);
        Content = new Border
        {
            Child = root, Background = new SolidColorBrush(palette.LunaBubble),
            BorderBrush = new SolidColorBrush(palette.LunaBubbleBorder), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 9, 12, 9),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (!_reduced)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }
        else
        {
            foreach (var letter in _letters) letter.Opacity = 1;
        }
    }

    internal bool AnimatingForTest => _timer?.IsEnabled == true;

    private void Tick()
    {
        _time += 0.04;
        var wobble = Math.Sin(_time * 2 * Math.PI / 2.4) * 1.5;
        _magnifierMove.X = wobble;
        _magnifierMove.Y = Math.Cos(_time * 2 * Math.PI / 2.4) * 0.8;
        _magnifierTurn.Angle = Math.Sin(_time * 2 * Math.PI / 2.4 + 0.4) * 3.5;
        var blink = (_time % 4.8) / 4.8;
        _blink.Height = blink is > 0.46 and < 0.52 ? 0.6 : 5.6;
        _shine.Opacity = blink is > 0.55 and < 0.70 ? 0.9 : 0.35;
        for (var i = 0; i < _letters.Length; i++)
        {
            var wave = 0.5 + 0.5 * Math.Sin((_time * 2 * Math.PI / 1.6) - _letterPhase[i]);
            _letters[i].Opacity = 0.45 + 0.55 * wave;
            _letterMove[i].Y = -2 * wave;
        }
    }

    public void Stop()
    {
        _timer?.Stop();
        _magnifierMove.X = 0;
        _magnifierMove.Y = 0;
        _magnifierTurn.Angle = 0;
        _blink.Height = 5.6;
        foreach (var letter in _letters) letter.Opacity = 1;
    }

    private static SolidColorBrush Brush(string hex, double opacity = 1) =>
        new(Color.Parse(hex)) { Opacity = opacity };

    private static void Add(Canvas canvas, Control child)
    {
        canvas.Children.Add(child);
    }

    private static Line Line(double x1, double y1, double x2, double y2, string color, double width) =>
        new()
        {
            StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2),
            Stroke = Brush(color), StrokeThickness = width, StrokeLineCap = PenLineCap.Round
        };

    private static Path PathShape(string data, string? fill, string? stroke, double width) =>
        new()
        {
            Data = Geometry.Parse(data),
            Fill = fill is null ? null : Brush(fill),
            Stroke = stroke is null ? null : Brush(stroke),
            StrokeThickness = width,
            StrokeLineCap = PenLineCap.Round
        };
}
