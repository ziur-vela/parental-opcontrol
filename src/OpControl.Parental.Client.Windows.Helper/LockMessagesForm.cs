// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Drawing2D;
using OpControl.Parental.Contracts;
using OpControl.Parental.Localization;

namespace OpControl.Parental.Helper;

/// <summary>
/// Messaging panel that belongs to the lock overlay. It is shown as a form owned
/// by the primary lock form, so it sits above the topmost overlay without lowering
/// or weakening it. The same Helper pipe and cached thread are used as My OpControl,
/// so delivery status and the conversation stay identical after unlocking.
/// </summary>
public sealed class LockMessagesForm : Form
{
    private static readonly Color Back = Color.FromArgb(0x0b, 0x1d, 0x20);
    private static readonly Color Panel = Color.FromArgb(0x12, 0x30, 0x2f);
    private static readonly Color Card = Color.FromArgb(0x13, 0x30, 0x2a);
    private static readonly Color Mint = Color.FromArgb(0x7e, 0xe8, 0xc4);
    private static readonly Color MintDim = Color.FromArgb(0x4f, 0xd1, 0xa5);
    private static readonly Color Pale = Color.FromArgb(0xc8, 0xea, 0xdf);
    private static readonly Color Green = Color.FromArgb(0x16, 0xa3, 0x4a);
    private static readonly Color OwnText = Color.FromArgb(0x15, 0x10, 0x3a);

    private readonly Panel _threadHost;
    private readonly TableLayoutPanel _thread;
    private readonly TextBox _input;
    private readonly Label _status;
    private readonly Button _send;
    private readonly System.Windows.Forms.Timer _poll;
    private readonly IReadOnlyList<ChildMessageDto>? _demo;
    private List<ChildMessageDto> _messages = [];
    private bool _sending;
    private bool _loading;
    private bool _firstLoad = true;

    public LockMessagesForm() : this(null) { }

    internal LockMessagesForm(IReadOnlyList<ChildMessageDto>? demo)
    {
        _demo = demo;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Back;
        ForeColor = Pale;
        Width = 560;
        Height = 640;
        KeyPreview = true;
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Back,
            Padding = new Padding(18, 14, 18, 16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        var title = new Label
        {
            Text = Loc.T("Client.Messages.Title"),
            Font = Theme.Display(21f, FontStyle.Bold),
            ForeColor = Pale,
            AutoSize = true,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var close = new Button
        {
            Text = "×",
            Font = Theme.Body(18f, FontStyle.Bold),
            ForeColor = Pale,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(36, 36),
            TabStop = true
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Close();
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(close, 1, 0);
        root.Controls.Add(header, 0, 0);

        _thread = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 0,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _thread.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _threadHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 8)
        };
        _threadHost.Controls.Add(_thread);
        _threadHost.ClientSizeChanged += (_, _) => _thread.Width = Math.Max(200, _threadHost.ClientSize.Width - 24);
        root.Controls.Add(_threadHost, 0, 1);

        var quick = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        foreach (var text in new[]
                 {
                     Loc.T("Client.Messages.Quick.MoreTime"),
                     Loc.T("Client.Messages.Quick.Homework"),
                     Loc.T("Client.Messages.Quick.ComeHere")
                 })
        {
            var chip = new Button
            {
                Text = text,
                Font = Theme.Body(12f, FontStyle.Bold),
                ForeColor = Pale,
                BackColor = Panel,
                FlatStyle = FlatStyle.Flat,
                AutoSize = true,
                Padding = new Padding(13, 6, 13, 6),
                Margin = new Padding(0, 0, 8, 8)
            };
            chip.FlatAppearance.BorderColor = MintDim;
            chip.FlatAppearance.BorderSize = 1;
            chip.Click += (_, _) => { _input!.Text = text; _input.Focus(); };
            quick.Controls.Add(chip);
        }
        root.Controls.Add(quick, 0, 2);

        var composer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        composer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        composer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _input = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            MaxLength = 500,
            Height = 58,
            Dock = DockStyle.Top,
            BackColor = Panel,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Body(12f, FontStyle.Bold),
            PlaceholderText = Loc.T("Client.Messages.Placeholder"),
            Margin = new Padding(0, 0, 8, 0)
        };
        _input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };
        _send = new Button
        {
            Text = Loc.T("Client.Messages.Send"),
            Dock = DockStyle.Fill,
            Height = 58,
            BackColor = Green,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Display(15f, FontStyle.Bold),
            Margin = new Padding(0)
        };
        _send.FlatAppearance.BorderSize = 0;
        _send.Click += async (_, _) => await SendAsync();
        _status = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Mint,
            BackColor = Color.Transparent,
            Font = Theme.Body(10.5f, FontStyle.Bold),
            Margin = new Padding(0, 6, 0, 0)
        };
        composer.Controls.Add(_input, 0, 0);
        composer.Controls.Add(_send, 1, 0);
        composer.Controls.Add(_status, 0, 1);
        composer.SetColumnSpan(_status, 2);
        root.Controls.Add(composer, 0, 3);

        Controls.Add(root);

        _poll = new System.Windows.Forms.Timer { Interval = 2000 };
        _poll.Tick += async (_, _) => await RefreshAsync(markSeen: true, quiet: true);

        Paint += (_, e) => DrawFrame(e.Graphics);
        Resize += (_, _) => ApplyRegion();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); }
        };
        Shown += async (_, _) =>
        {
            HelperPaths.LogEvent("lock-messages-shown");
            LoadCache();
            await RefreshAsync(markSeen: true, quiet: false);
            _poll.Start();
            _input.Focus();
        };
        FormClosed += (_, _) =>
        {
            _poll.Stop();
            HelperPaths.LogEvent("lock-messages-closed");
        };

        Dpi.ScaleForm(this);
        ApplyRegion();
        ScrollbarTheme.Attach(this);
    }

    private void LoadCache()
    {
        if (_demo is not null)
        {
            _messages = _demo.Where(m => !m.Deleted).OrderBy(m => m.CreatedUtc).ToList();
            RebuildThread(scrollToEnd: true);
            return;
        }
        var cached = MessageThreadCache.Read();
        if (cached is null) return;
        _messages = cached.Where(m => !m.Deleted).OrderBy(m => m.CreatedUtc).ToList();
        RebuildThread(scrollToEnd: true);
    }

    private async Task RefreshAsync(bool markSeen, bool quiet)
    {
        if (_demo is not null) return;
        if (_loading) return;
        _loading = true;
        if (!quiet) _status.Text = Loc.T("Client.Messages.Loading");
        try
        {
            var response = await PinPipeClient.GetThreadAsync(CancellationToken.None);
            if (response.Ok && response.Messages is not null)
                _messages = response.Messages.Where(m => !m.Deleted).OrderBy(m => m.CreatedUtc).ToList();
            else
            {
                var cached = MessageThreadCache.Read();
                if (cached is not null)
                    _messages = cached.Where(m => !m.Deleted).OrderBy(m => m.CreatedUtc).ToList();
            }
            _status.Text = "";
            RebuildThread(scrollToEnd: quiet || _firstLoad);
            _firstLoad = false;
            if (markSeen)
                foreach (var message in _messages.Where(m => m.Direction == MessageDirection.ParentToChild && m.SeenUtc is null))
                    await PinPipeClient.MarkSeenAsync(message.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (!quiet) _status.Text = Loc.T("Client.Messages.SendFailed");
            HelperPaths.LogEvent($"lock-messages-refresh-failed {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private void RebuildThread(bool scrollToEnd)
    {
        _thread.SuspendLayout();
        _thread.Controls.Clear();
        _thread.RowStyles.Clear();
        _thread.RowCount = 0;
        if (_messages.Count == 0)
        {
            var empty = new Label
            {
                Text = Loc.T("Client.Messages.Empty"),
                AutoSize = true,
                ForeColor = Pale,
                Font = Theme.Body(12f),
                Margin = new Padding(4, 8, 4, 8)
            };
            AddThreadRow(empty);
        }
        foreach (var message in _messages)
            AddThreadRow(BuildRow(message));
        _thread.Width = Math.Max(200, _threadHost.ClientSize.Width - 24);
        _thread.ResumeLayout(true);
        if (scrollToEnd)
            _threadHost.ScrollControlIntoView(_thread.Controls.Count > 0 ? _thread.Controls[^1] : _thread);
    }

    private void AddThreadRow(Control row)
    {
        _thread.RowCount++;
        _thread.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _thread.Controls.Add(row, 0, _thread.RowCount - 1);
    }

    private Control BuildRow(ChildMessageDto message)
    {
        var own = message.Direction == MessageDirection.ChildToParent;
        var row = new FlowLayoutPanel
        {
            FlowDirection = own ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0)
        };

        var stack = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            MaximumSize = new Size(400, 0)
        };
        var head = new Label
        {
            Text = own ? Loc.T("Client.Messages.SentByYou")
                       : $"{MessageDisplay.Sender(message)} · {MessageDisplay.Time(message)}",
            AutoSize = true,
            ForeColor = Pale,
            Font = Theme.Body(10f, FontStyle.Bold),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 3)
        };
        if (own)
        {
            head.Text = $"{Loc.T("Client.Messages.SentByYou")} · {MessageDisplay.Time(message)}";
            head.Anchor = AnchorStyles.Right;
        }
        var bubble = new Label
        {
            Text = MessageDisplay.Body(message),
            AutoSize = true,
            MaximumSize = new Size(390, 0),
            BackColor = own ? Color.FromArgb(0x3d, 0xdc, 0x97) : Card,
            ForeColor = own ? OwnText : Color.FromArgb(0xf4, 0xf1, 0xff),
            Font = Theme.Body(12.5f, FontStyle.Bold),
            Padding = new Padding(12, 9, 12, 9),
            Margin = new Padding(0, 0, 0, 3)
        };
        var status = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            FlowDirection = FlowDirection.LeftToRight
        };
        if (own)
        {
            var ui = MessageStatusRules.ForDisplay(message);
            status.Anchor = AnchorStyles.Right;
            status.Controls.Add(new Label
            {
                Text = $"{MessageStatusRules.Icon(ui)} {Loc.T("Client." + MessageStatusRules.LabelKey(ui))}",
                AutoSize = true,
                ForeColor = Mint,
                Font = Theme.Body(9.5f, FontStyle.Bold),
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            });
        }
        else if (message.RequireAck && message.AcknowledgedUtc is null)
        {
            var gotIt = new Button
            {
                Text = Loc.T("Client.Messages.GotIt"),
                AutoSize = true,
                BackColor = Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.Body(9.5f, FontStyle.Bold),
                Padding = new Padding(8, 3, 8, 3),
                Margin = new Padding(0)
            };
            gotIt.FlatAppearance.BorderSize = 0;
            gotIt.Click += async (_, _) =>
            {
                await PinPipeClient.MarkAcknowledgedAsync(message.Id, CancellationToken.None);
                await RefreshAsync(markSeen: false, quiet: true);
            };
            status.Controls.Add(gotIt);
        }

        stack.Controls.Add(head, 0, 0);
        stack.Controls.Add(bubble, 0, 1);
        stack.Controls.Add(status, 0, 2);
        row.Controls.Add(stack);
        return row;
    }

    private async Task SendAsync()
    {
        if (_sending) return;
        var text = _input.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            _status.Text = Loc.T("Client.Messages.EmptyInput");
            return;
        }
        if (text.Length > 500)
        {
            _status.Text = Loc.T("Client.Messages.TooLong");
            return;
        }
        _sending = true;
        _send.Enabled = false;
        _status.Text = Loc.T("Client.Messages.Sending");
        try
        {
            var result = await PinPipeClient.SendMessageAsync(text, CancellationToken.None);
            if (!result.Ok)
            {
                _status.Text = result.Error ?? Loc.T("Client.Messages.SendFailed");
                return;
            }
            _input.Text = "";
            _status.Text = result.Queued ? Loc.T("Client.Messages.Queued") : Loc.T("Client.Messages.Sent");
            await RefreshAsync(markSeen: false, quiet: true);
        }
        finally
        {
            _sending = false;
            _send.Enabled = true;
        }
    }

    private void ApplyRegion()
    {
        try
        {
            using var path = Rounded(new Rectangle(0, 0, Width, Height), (int)(18 * Dpi.Factor(this)));
            Region?.Dispose();
            Region = new Region(path);
        }
        catch { }
    }

    private void DrawFrame(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = (int)(18 * Dpi.Factor(this));
        using var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), radius);
        using var border = new Pen(Color.FromArgb(0x4a, 0x3d, 0x94), Math.Max(1.5f, 2 * Dpi.Factor(this)));
        g.DrawPath(border, path);
    }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(2, radius * 2);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return parameters;
        }
    }
}
