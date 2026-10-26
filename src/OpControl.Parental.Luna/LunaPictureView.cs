using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Luna;

/// <summary>Draws the approved Luna fraction pictures with vector shapes.</summary>
public sealed class LunaPictureView : UserControl
{
    private const double CaptionSize = 20;
    private static readonly Regex Fraction = new(@"\\frac\{(\d+)\}\{(\d+)\}", RegexOptions.Compiled);

    private readonly LunaPictureLayout _layout;
    private readonly LunaPalette _palette;
    private readonly LunaSubjectStyle _style;

    public LunaPictureView(LunaPicture picture, LunaPalette palette, LunaSubjectStyle style)
    {
        _layout = LunaPictures.Layout(picture);
        _palette = palette;
        _style = style;

        var canvas = new PictureCanvas(_layout, palette, style)
        {
            Width = _layout.Width,
            Height = _layout.Height
        };
        var scaled = new Viewbox
        {
            Child = canvas,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var stack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(scaled);
        if (BuildCaption() is { } caption)
            stack.Children.Add(caption);
        Content = stack;
    }

    internal LunaPictureLayout LayoutForTest => _layout;

    private Control? BuildCaption()
    {
        var answer = _layout.AnswerLatex is { } answerLatex && TryParse(answerLatex, out var a)
            ? new LunaFraction(a.Numerator, a.Denominator)
            : null;
        var terms = ParseTerms(_layout.CaptionLatex);
        if (terms.Count == 0) return null;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        for (var i = 0; i < terms.Count; i++)
        {
            if (i > 0) row.Children.Add(Operator("+"));
            row.Children.Add(new LunaFractionText(terms[i].Numerator, terms[i].Denominator,
                new SolidColorBrush(_palette.Text), CaptionSize));
        }
        if (answer is { } value)
        {
            row.Children.Add(Operator("="));
            row.Children.Add(new LunaFractionText(value.Numerator, value.Denominator,
                new SolidColorBrush(_palette.Success), CaptionSize));
        }
        return row;
    }

    private TextBlock Operator(string text) => new()
    {
        Text = text,
        FontFamily = LunaThemes.Body,
        FontSize = CaptionSize - 2,
        FontWeight = FontWeight.Bold,
        Foreground = new SolidColorBrush(_palette.Text),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static List<LunaFraction> ParseTerms(string latex)
    {
        var terms = new List<LunaFraction>();
        foreach (Match match in Fraction.Matches(latex ?? ""))
        {
            if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var numerator) &&
                int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var denominator))
                terms.Add(new LunaFraction(numerator, denominator));
        }
        return terms;
    }

    private static bool TryParse(string latex, out LunaFraction fraction)
    {
        var match = Fraction.Match(latex ?? "");
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var numerator) &&
            int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var denominator))
        {
            fraction = new LunaFraction(numerator, denominator);
            return true;
        }
        fraction = null!;
        return false;
    }

    private sealed class PictureCanvas : Control
    {
        private readonly LunaPictureLayout _layout;
        private readonly LunaPalette _palette;
        private readonly LunaSubjectStyle _style;

        public PictureCanvas(LunaPictureLayout layout, LunaPalette palette, LunaSubjectStyle style)
        {
            _layout = layout;
            _palette = palette;
            _style = style;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            foreach (var shape in _layout.Shapes)
            {
                switch (shape.Kind)
                {
                    case "bar":
                        DrawBar(context, shape);
                        break;
                    case "bar-divider":
                        context.DrawRectangle(new SolidColorBrush(_style.Shadow), null,
                            new Rect(shape.X - shape.Width / 2, shape.Y, shape.Width, shape.Height));
                        break;
                    case "pie":
                        context.DrawEllipse(new SolidColorBrush(_palette.Sunken),
                            new Pen(new SolidColorBrush(_style.Shadow), 2.5),
                            new Rect(shape.X, shape.Y, shape.Width, shape.Height));
                        break;
                    case "pie-slice":
                        DrawSlice(context, shape);
                        break;
                    case "topping":
                        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(210, 255, 246, 200)), null,
                            new Rect(shape.X, shape.Y, shape.Width, shape.Height));
                        break;
                    case "line":
                        context.DrawRectangle(new SolidColorBrush(_palette.Success), null,
                            new RoundedRect(new Rect(shape.X, shape.Y, shape.Width, shape.Height), 2));
                        break;
                    case "line-mark":
                        context.DrawRectangle(new SolidColorBrush(_style.Dot), null,
                            new RoundedRect(new Rect(shape.X - 2, shape.Y, shape.Width, shape.Height), 2));
                        break;
                }
            }
        }

        private void DrawBar(DrawingContext context, LunaPictureShape shape)
        {
            var brush = shape.ColorRole switch
            {
                "term1" => new SolidColorBrush(_style.Dot),
                "term2" => new SolidColorBrush(_palette.Accent),
                _ => new SolidColorBrush(_palette.Sunken)
            };
            var penColor = shape.ColorRole == "answer-outline" ? _palette.Success : _style.Shadow;
            var pen = new Pen(new SolidColorBrush(penColor), shape.ColorRole == "answer-outline" ? 3 : 2.5);
            context.DrawRectangle(brush, pen,
                new RoundedRect(new Rect(shape.X, shape.Y, shape.Width, shape.Height),
                    LunaPictures.BarCorner));
        }

        private void DrawSlice(DrawingContext context, LunaPictureShape shape)
        {
            var radius = shape.Width / 2;
            var center = new Point(shape.X, shape.Y);
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                var start = PointOnCircle(center, radius - 2, shape.StartAngle);
                var end = PointOnCircle(center, radius - 2, shape.EndAngle);
                path.BeginFigure(center, true);
                path.LineTo(start);
                path.ArcTo(end, new Size(radius - 2, radius - 2), 0,
                    Math.Abs(shape.EndAngle - shape.StartAngle) > 180,
                    SweepDirection.Clockwise);
                path.EndFigure(true);
            }
            var fill = shape.ColorRole == "term1" ? _style.Dot : _palette.Sunken;
            context.DrawGeometry(new SolidColorBrush(fill),
                new Pen(new SolidColorBrush(_style.Shadow), 1.5), geometry);
        }

        private static Point PointOnCircle(Point center, double radius, double angle)
        {
            var radians = angle * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
        }
    }
}

/// <summary>A small stacked fraction used for captions and the emphasised answer.</summary>
public sealed class LunaFractionText : UserControl
{
    public LunaFractionText(int numerator, int denominator, IBrush brush, double fontSize = 15)
    {
        var stack = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = numerator.ToString(CultureInfo.InvariantCulture),
            FontFamily = LunaThemes.Body,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new Border
        {
            Height = Math.Max(1.6, fontSize / 12),
            MinWidth = fontSize + 4,
            Background = brush,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
        stack.Children.Add(new TextBlock
        {
            Text = denominator.ToString(CultureInfo.InvariantCulture),
            FontFamily = LunaThemes.Body,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        Content = stack;
    }
}
