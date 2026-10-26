// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using OpControl.Parental.Contracts;
using Path = Avalonia.Controls.Shapes.Path;

namespace OpControl.Parental.Luna;

/// <summary>Flat vector avatars for the child's own chat bubbles.</summary>
public static class LunaAvatars
{
    public const double ChatSize = 34;
    private const double BaseSize = 34;

    private static readonly Color Skin = Color.Parse("#f2c48d");
    private static readonly Color Ink = Color.Parse("#2b2f3a");
    private static readonly Color Pink = Color.Parse("#e88b7a");
    private static readonly Color Cream = Color.Parse("#fff6e8");

    public static string NameKey(string key) => LunaAvatarKeys.Normalize(key) switch
    {
        LunaAvatarKeys.Cat => "Luna.Avatar.Cat",
        LunaAvatarKeys.Dog => "Luna.Avatar.Dog",
        LunaAvatarKeys.Fox => "Luna.Avatar.Fox",
        LunaAvatarKeys.Rocket => "Luna.Avatar.Rocket",
        LunaAvatarKeys.Star => "Luna.Avatar.Star",
        LunaAvatarKeys.Planet => "Luna.Avatar.Planet",
        LunaAvatarKeys.Robot => "Luna.Avatar.Robot",
        _ => "Luna.Avatar.Face"
    };

    public static Control View(string? key, double size, LunaPalette palette)
    {
        key = LunaAvatarKeys.Normalize(key);
        var canvas = new Canvas { Width = BaseSize, Height = BaseSize, Name = "Avatar_" + key };
        var ringColor = palette.Key == "gray" ? Color.Parse("#14845a") : Color.Parse("#3ddc97");
        void Place(Shape shape, double x, double y, double w, double h)
        {
            shape.Width = w;
            shape.Height = h;
            Canvas.SetLeft(shape, x);
            Canvas.SetTop(shape, y);
            canvas.Children.Add(shape);
        }
        void AddPath(Path path) => canvas.Children.Add(path);

        // The ring sits on top of the avatar edge so every shape is framed.
        Place(new Ellipse
        {
            Stroke = new SolidColorBrush(ringColor),
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        }, 1, 1, 32, 32);

        switch (key)
        {
            case LunaAvatarKeys.Cat: Cat(AddPath, Place); break;
            case LunaAvatarKeys.Dog: Dog(AddPath, Place); break;
            case LunaAvatarKeys.Fox: Fox(AddPath, Place); break;
            case LunaAvatarKeys.Rocket: Rocket(AddPath); break;
            case LunaAvatarKeys.Star: Star(AddPath); break;
            case LunaAvatarKeys.Planet: Planet(AddPath, Place); break;
            case LunaAvatarKeys.Robot: Robot(AddPath, Place); break;
            default: Face(AddPath, Place); break;
        }

        return new Viewbox
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Child = canvas
        };
    }

    private static void Face(Action<Path> addPath, Action<Shape, double, double, double, double> place)
    {
        place(Ellipse(Skin), 3, 3, 28, 28);
        place(Ellipse(Ink), 10.6, 13, 3.2, 4.2);
        place(Ellipse(Ink), 20.2, 13, 3.2, 4.2);
        addPath(Stroke("M11 21 Q17 26 23 21", 1.8));
    }

    private static void Cat(Action<Path> addPath, Action<Shape, double, double, double, double> place)
    {
        var fur = Color.Parse("#f6b26b");
        var ear = Color.Parse("#e79a52");
        addPath(Fill("M7 10 L8 3 L14 7 Z", ear));
        addPath(Fill("M27 10 L26 3 L20 7 Z", ear));
        addPath(Fill("M8 9 L9 5 L12.5 7.5 Z", Pink));
        addPath(Fill("M26 9 L25 5 L21.5 7.5 Z", Pink));
        place(Ellipse(fur), 4, 6, 26, 23);
        place(Ellipse(Ink), 11, 14, 3, 4);
        place(Ellipse(Ink), 20, 14, 3, 4);
        place(Ellipse(Pink), 15.5, 19, 3, 2.4);
        Whiskers(addPath);
        addPath(Stroke("M12 23 Q17 27 22 23", 1.6));
    }

    private static void Dog(Action<Path> addPath, Action<Shape, double, double, double, double> place)
    {
        var fur = Color.Parse("#c98b5e");
        var dark = Color.Parse("#8a5a3b");
        addPath(Fill("M7 9 C2 9 2 20 6 24 C9 26 10 20 10 15 Z", dark));
        addPath(Fill("M27 9 C32 9 32 20 28 24 C25 26 24 20 24 15 Z", dark));
        place(Ellipse(fur), 4, 6, 26, 23);
        place(Ellipse(Color.Parse("#f2d3b3")), 9, 17, 16, 11);
        place(Ellipse(Ink), 11, 13, 3, 4);
        place(Ellipse(Ink), 20, 13, 3, 4);
        place(Ellipse(Ink), 15.5, 19, 3.4, 2.6);
        addPath(Stroke("M12 24 Q17 27 22 24", 1.6));
    }

    private static void Fox(Action<Path> addPath, Action<Shape, double, double, double, double> place)
    {
        var fur = Color.Parse("#e76f51");
        var ear = Color.Parse("#d95d40");
        addPath(Fill("M6 11 L7 2 L14 8 Z", ear));
        addPath(Fill("M28 11 L27 2 L20 8 Z", ear));
        addPath(Fill("M8 9 L9 4 L12.5 8 Z", Cream));
        addPath(Fill("M26 9 L25 4 L21.5 8 Z", Cream));
        place(Ellipse(fur), 4, 7, 26, 21);
        place(Ellipse(Cream), 9, 18, 16, 10);
        place(Ellipse(Ink), 11, 13, 3, 4);
        place(Ellipse(Ink), 20, 13, 3, 4);
        place(Ellipse(Ink), 15.5, 19, 3.4, 2.4);
        addPath(Stroke("M12 23 Q17 26 22 23", 1.6));
    }

    private static void Rocket(Action<Path> addPath)
    {
        var body = Color.Parse("#f4f1ff");
        addPath(Fill("M17 3 C21 7 22.5 13 22.5 20 L11.5 20 C11.5 13 13 7 17 3 Z", body, 1.4));
        addPath(Fill("M17 3 C19 6 20 9 20 12 L14 12 C14 9 15 6 17 3 Z", Color.Parse("#e76f51")));
        addPath(new Path
        {
            Data = Geometry.Parse("M14 10 A3 3 0 1 1 20 10 A3 3 0 1 1 14 10"),
            Fill = new SolidColorBrush(Color.Parse("#5b8cff")),
            Stroke = new SolidColorBrush(Ink),
            StrokeThickness = 1
        });
        addPath(Fill("M12 18 L8 23 L12 23 Z", Color.Parse("#3ddc97")));
        addPath(Fill("M22 18 L26 23 L22 23 Z", Color.Parse("#3ddc97")));
        addPath(Fill("M14 21 L17 29 L20 21 Z", Color.Parse("#ffb547")));
    }

    private static void Star(Action<Path> addPath)
    {
        addPath(Fill("M17 3 L20.4 12.2 L30 12.8 L22.4 18.8 L25 28.2 L17 22.5 L9 28.2 L11.6 18.8 L4 12.8 L13.6 12.2 Z",
            Color.Parse("#f6dc7e"), 1.3));
        addPath(new Path
        {
            Data = Geometry.Parse("M12 15 A2 2 0 1 1 16 15 A2 2 0 1 1 12 15"),
            Fill = new SolidColorBrush(Ink)
        });
        addPath(new Path
        {
            Data = Geometry.Parse("M18 15 A2 2 0 1 1 22 15 A2 2 0 1 1 18 15"),
            Fill = new SolidColorBrush(Ink)
        });
        addPath(Stroke("M13 20 Q17 23 21 20", 1.4));
    }

    private static void Planet(Action<Path> addPath, Action<Shape, double, double, double, double> place)
    {
        place(Ellipse(Color.Parse("#5b8cff")), 6, 6, 22, 22);
        place(Ellipse(Color.Parse("#3ddc97")), 10, 9, 7, 5);
        place(Ellipse(Color.Parse("#3ddc97")), 17, 19, 6, 4);
        addPath(new Path
        {
            Data = Geometry.Parse("M2 17 A15 4 0 1 1 32 17 A15 4 0 1 1 2 17"),
            Stroke = new SolidColorBrush(Color.Parse("#f6dc7e")),
            StrokeThickness = 2.2,
            Fill = Brushes.Transparent,
            RenderTransform = new RotateTransform(-16),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
        });
        place(Ellipse(Cream), 23, 4, 4, 4);
    }

    private static void Robot(Action<Path> addPath, Action<Shape, double, double, double, double> place)
    {
        var metal = Color.Parse("#9aa7b8");
        place(new Rectangle { Fill = new SolidColorBrush(Ink), RadiusX = 1, RadiusY = 1 }, 16, 2, 2, 6);
        place(Ellipse(Color.Parse("#f6dc7e")), 14.5, 1, 5, 5);
        place(new Rectangle { Fill = new SolidColorBrush(metal), RadiusX = 3, RadiusY = 3 }, 5, 7, 24, 21);
        place(new Rectangle { Fill = new SolidColorBrush(Color.Parse("#15103a")), RadiusX = 4, RadiusY = 4 }, 8, 11, 18, 12);
        place(Ellipse(Color.Parse("#f4f1ff")), 11, 14, 3, 3.4);
        place(Ellipse(Color.Parse("#f4f1ff")), 20, 14, 3, 3.4);
        addPath(Stroke("M13 20 L21 20", 1.6, Color.Parse("#f4f1ff")));
        place(new Rectangle { Fill = new SolidColorBrush(metal), RadiusX = 1.5, RadiusY = 1.5 }, 2, 13, 3, 8);
        place(new Rectangle { Fill = new SolidColorBrush(metal), RadiusX = 1.5, RadiusY = 1.5 }, 29, 13, 3, 8);
    }

    private static void Whiskers(Action<Path> addPath)
    {
        foreach (var data in new[] { "M9 21 L3 19", "M9 23 L3 24", "M25 21 L31 19", "M25 23 L31 24" })
            addPath(Stroke(data, 0.9));
    }

    private static Ellipse Ellipse(Color fill) => new() { Fill = new SolidColorBrush(fill) };

    private static Path Fill(string data, Color color, double stroke = 0) => new()
    {
        Data = Geometry.Parse(data),
        Fill = new SolidColorBrush(color),
        Stroke = stroke > 0 ? new SolidColorBrush(Ink) : null,
        StrokeThickness = stroke,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round
    };

    private static Path Stroke(string data, double thickness, Color? color = null) => new()
    {
        Data = Geometry.Parse(data),
        Stroke = new SolidColorBrush(color ?? Ink),
        StrokeThickness = thickness,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round
    };
}
