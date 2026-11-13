// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Drawing2D;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>Themed parent-message popup in the child's Night sky / Moon gray theme.</summary>
public sealed class MessagePopupForm : Form
{
    private readonly ChildMessageDto _message;
    private readonly MessagePopupPalette _palette;
    private readonly Panel _bubble;
    private readonly Button _primary;
    private readonly Button _reply;

    public event Action<ChildMessageDto>? Seen;
    public event Action<ChildMessageDto>? Acknowledged;
    public event Action? ReplyClicked;

    public MessagePopupForm(ChildMessageDto message)
    {
        _message = message;
        _palette = MessagePopupTheme.For(MessagePopupTheme.Resolve());
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _palette.Background;
        ForeColor = _palette.Text;
        Padding = new Padding(22, 18, 22, 18);
        Width = 486;
        Height = 316;
        MinimumSize = new Size(400, 250);
        KeyPreview = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var head = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        head.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        head.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        head.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var eyebrow = new Label
        {
            Text = Loc.T("Client.Messages.PopupTitle").ToUpperInvariant(),
            Font = Theme.Mono(9f, FontStyle.Bold),
            ForeColor = _palette.Accent,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };
        var sender = new Label
        {
            Text = MessageDisplay.Sender(message),
            Font = Theme.KidDisplay(18f),
            ForeColor = _palette.Text,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        var time = new Label
        {
            Text = MessageDisplay.Time(message),
            Font = Theme.Mono(9f),
            ForeColor = _palette.Muted,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 0, 10)
        };
        head.Controls.Add(eyebrow, 0, 0);
        head.Controls.Add(sender, 0, 1);
        head.Controls.Add(time, 0, 2);

        _bubble = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _palette.Bubble,
            Margin = new Padding(0, 4, 0, 14),
            Padding = new Padding(16, 12, 16, 12)
        };
        _bubble.Resize += (_, _) => Round(_bubble, ScaleRadius(18));
        var body = new Label
        {
            Text = MessageDisplay.Body(message),
            Font = Theme.KidBody(12.5f),
            ForeColor = _palette.BubbleInk,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        _bubble.Controls.Add(body);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _primary = new Button
        {
            Text = message.RequireAck ? Loc.T("Client.Messages.GotIt") : Loc.T("Client.Messages.Close"),
            AutoSize = false,
            Size = new Size(message.RequireAck ? 148 : 124, 46),
            Margin = new Padding(10, 0, 0, 0)
        };
        StyleButton(_primary, primary: true);
        _primary.Click += (_, _) =>
        {
            if (message.RequireAck) Acknowledged?.Invoke(message);
            Close();
        };

        _reply = new Button
        {
            Text = Loc.T("Client.Messages.Reply"),
            AutoSize = false,
            Size = new Size(124, 46)
        };
        StyleButton(_reply, primary: false);
        _reply.Click += (_, _) =>
        {
            ReplyClicked?.Invoke();
            Close();
        };

        buttons.Controls.Add(_primary);
        buttons.Controls.Add(_reply);

        layout.Controls.Add(head, 0, 0);
        layout.Controls.Add(_bubble, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        Resize += (_, _) => Round(this, ScaleRadius(22));
        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(_palette.Border, 1.5f);
            using var path = RoundedPath(new RectangleF(0, 0, Width - 1, Height - 1), 22f);
            e.Graphics.DrawPath(pen, path);
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };

        Dpi.ScaleForm(this);
        Round(this, ScaleRadius(22));
        Round(_bubble, ScaleRadius(18));
        RoundButton(_primary);
        RoundButton(_reply);
        ScrollbarTheme.Attach(this);
    }

    private void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = _palette.Border;
        button.BackColor = primary ? _palette.Bubble : _palette.Secondary;
        button.ForeColor = primary ? _palette.BubbleInk : _palette.SecondaryInk;
        button.Font = Theme.KidBody(11.5f);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        button.Resize += (_, _) => RoundButton(button);
    }

    private int ScaleRadius(int radius) => Math.Max(2, (int)Math.Round(radius * DeviceDpi / 96.0));

    private void RoundButton(Button button) => Round(button, button.Height / 2);

    private static void Round(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = RoundedPath(new RectangleF(0, 0, control.Width, control.Height), radius);
        var old = control.Region;
        control.Region = new Region(path);
        old?.Dispose();
    }

    private static GraphicsPath RoundedPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2f;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW, but still accepts input
            parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Seen?.Invoke(_message);
        if (_message.SpeakAloud)
            Speech.Speak(MessageDisplay.Body(_message), Loc.Language);
    }
}
