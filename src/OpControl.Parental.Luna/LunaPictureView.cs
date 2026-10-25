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
        var caption = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Left };
        if (!string.IsNullOrWhiteSpace(_layout.CaptionLatex))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(LunaMarkdown.Renderer("$" + _layout.CaptionLatex + "$"));
            if (picture.Answer is { } answer)
            {
                row.Children.Add(new TextBlock
                {
                    Text = "=",
                    FontFamily = LunaThemes.Body,
                    FontSize = 15,
                    Foreground = new SolidColorBrush(palette.Text),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(new LunaFractionText(answer.Numerator, answer.Denominator,
                    new SolidColorBrush(palette.Success)));
            }
            caption.Children.Add(row);
        }
        var stack = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        stack.Children.Add(canvas);
        stack.Children.Add(caption);
        Content = stack;
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
                "answer-outline" => new SolidColorBrush(_palette.Sunken),
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
            context.DrawGeometry(new SolidColorBrush(_style.Dot),
                new Pen(new SolidColorBrush(_style.Shadow), 1.5), geometry);
        }

        private static Point PointOnCircle(Point center, double radius, double angle)
        {
            var radians = angle * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
        }
    }
}

/// <summary>A small stacked fraction used for the emphasised answer.</summary>
public sealed class LunaFractionText : UserControl
{
    public LunaFractionText(int numerator, int denominator, IBrush brush)
    {
        var stack = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = numerator.ToString(),
            FontFamily = LunaThemes.Body,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new Border
        {
            Height = 1.6,
            MinWidth = 22,
            Background = brush,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
        stack.Children.Add(new TextBlock
        {
            Text = denominator.ToString(),
            FontFamily = LunaThemes.Body,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        Content = stack;
    }
}
