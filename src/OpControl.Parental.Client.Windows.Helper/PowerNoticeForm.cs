// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Drawing2D;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>Themed, non-cancellable notice shown before a parent-requested restart/power off/sleep.</summary>
public sealed class PowerNoticeForm : Form
{
    private readonly MessagePopupPalette _palette;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _body;
    private readonly Label _countdown;
    private bool _allowClose;

    public PowerNoticeForm()
    {
        _palette = MessagePopupTheme.For(MessagePopupTheme.Resolve());
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _palette.Background;
        ForeColor = _palette.Text;
        Padding = new Padding(24, 20, 24, 20);
        Width = 520;
        Height = 280;
        MinimumSize = new Size(420, 230);
        KeyPreview = true;
        ControlBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _eyebrow = new Label
        {
            Text = Loc.T("Client.Power.Eyebrow").ToUpperInvariant(),
            Font = Theme.Mono(9f, FontStyle.Bold),
            ForeColor = _palette.Accent,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };
        _title = new Label
        {
            Font = Theme.KidDisplay(24f),
            ForeColor = _palette.Text,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6)
        };
        _body = new Label
        {
            Font = Theme.KidBody(13f),
            ForeColor = _palette.Muted,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            BackColor = Color.Transparent,
            UseMnemonic = false,
            Margin = new Padding(0, 0, 0, 10)
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _palette.Card,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0)
        };
        card.Resize += (_, _) => Round(card, ScaleRadius(18));
        _countdown = new Label
        {
            Font = Theme.Mono(15f, FontStyle.Bold),
            ForeColor = _palette.Text,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent
        };
        card.Controls.Add(_countdown);

        layout.Controls.Add(_eyebrow, 0, 0);
        layout.Controls.Add(_title, 0, 1);
        layout.Controls.Add(_body, 0, 2);
        layout.Controls.Add(card, 0, 3);
        Controls.Add(layout);

        Resize += (_, _) => Round(this, ScaleRadius(22));
        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(_palette.Border, 1.5f);
            using var path = RoundedPath(new RectangleF(0, 0, Width - 1, Height - 1), 22f);
            e.Graphics.DrawPath(pen, path);
        };

        FormClosing += (_, e) =>
        {
            if (!_allowClose) e.Cancel = true;
        };
        Dpi.ScaleForm(this);
        Round(this, ScaleRadius(22));
        Round(card, ScaleRadius(18));
    }

    public void UpdateNotice(PowerNotice notice, DateTime nowUtc)
    {
        var action = notice.Action;
        var remaining = notice.RunAtUtc - nowUtc;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        var clock = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:00}";
        _title.Text = action switch
        {
            DevicePowerAction.Restart => Loc.T("Client.Power.RestartTitle", clock),
            DevicePowerAction.PowerOff => Loc.T("Client.Power.PowerOffTitle", clock),
            _ => Loc.T("Client.Power.SleepTitle", clock)
        };
        _body.Text = action switch
        {
            DevicePowerAction.Restart => Loc.T("Client.Power.RestartBody", DelayText(notice.NoticeSeconds)),
            DevicePowerAction.PowerOff => Loc.T("Client.Power.PowerOffBody", DelayText(notice.NoticeSeconds)),
            _ => Loc.T("Client.Power.SleepBody")
        };
        _countdown.Text = clock;
    }

    public void HideNotice()
    {
        _allowClose = true;
        Close();
    }

    private static string DelayText(int seconds)
    {
        if (seconds <= 5) return Loc.T("Client.Power.DelaySeconds");
        if (seconds <= 60) return Loc.T("Client.Power.DelayMinute");
        return Loc.T("Client.Power.DelayMinutes", Math.Max(1, seconds / 60));
    }

    private int ScaleRadius(int radius) => Math.Max(2, (int)Math.Round(radius * DeviceDpi / 96.0));

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
            parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return parameters;
        }
    }
}
