// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpControl.Parental.Helper;

/// <summary>OpControl Style tokens for the Windows helper. Fonts are embedded (OFL).</summary>
internal static class Theme
{
    public static Color Bg = Color.FromArgb(11, 23, 20);
    public static Color Panel = Color.FromArgb(14, 29, 25);
    public static Color Panel2 = Color.FromArgb(17, 36, 32);
    public static Color Line = Color.FromArgb(31, 58, 51);
    public static Color Line2 = Color.FromArgb(42, 74, 66);
    public static Color Text = Color.FromArgb(227, 236, 232);
    public static Color Muted = Color.FromArgb(127, 154, 146);
    public static Color Faint = Color.FromArgb(98, 122, 114);
    public static Color Label = Color.FromArgb(226, 133, 107);
    public static Color Value = Color.FromArgb(60, 201, 167);
    public static Color Brand = Color.FromArgb(0, 200, 83);
    public static Color BrandDark = Color.FromArgb(4, 21, 11);
    public static Color Danger = Color.FromArgb(229, 96, 79);
    public static Color Warn = Color.FromArgb(231, 176, 74);
    public static Color OwnBubble = Color.FromArgb(18, 38, 58);
    public static Color OwnBubbleBorder = Color.FromArgb(36, 71, 106);
    public static Color ParentBubble = Color.FromArgb(19, 48, 42);
    public static Color ParentBubbleBorder = Color.FromArgb(42, 74, 66);
    public static Color BrandText = Color.FromArgb(0, 200, 83);
    public static Color Highlight = Color.FromArgb(27, 58, 50);
    public static bool IsLight { get; private set; }

    public static void Apply(bool light)
    {
        IsLight = light;
        if (light)
        {
            Bg = Color.FromArgb(241, 244, 241);
            Panel = Color.FromArgb(255, 255, 255);
            Panel2 = Color.FromArgb(245, 248, 245);
            Line = Color.FromArgb(213, 222, 216);
            Line2 = Color.FromArgb(188, 203, 194);
            Text = Color.FromArgb(18, 32, 27);
            Muted = Color.FromArgb(76, 98, 89);
            Faint = Color.FromArgb(104, 125, 116);
            Label = Color.FromArgb(161, 63, 37);
            Value = Color.FromArgb(11, 107, 87);
            Brand = Color.FromArgb(0, 200, 83);
            BrandText = Color.FromArgb(0, 122, 61);
            BrandDark = Color.FromArgb(4, 21, 11);
            Danger = Color.FromArgb(178, 58, 45);
            Warn = Color.FromArgb(138, 97, 12);
            OwnBubble = Color.FromArgb(227, 238, 250);
            OwnBubbleBorder = Color.FromArgb(185, 210, 238);
            ParentBubble = Color.FromArgb(227, 244, 234);
            ParentBubbleBorder = Color.FromArgb(188, 220, 200);
            Highlight = Color.FromArgb(209, 238, 225);
        }
        else
        {
            Bg = Color.FromArgb(11, 23, 20);
            Panel = Color.FromArgb(14, 29, 25);
            Panel2 = Color.FromArgb(17, 36, 32);
            Line = Color.FromArgb(31, 58, 51);
            Line2 = Color.FromArgb(42, 74, 66);
            Text = Color.FromArgb(227, 236, 232);
            Muted = Color.FromArgb(127, 154, 146);
            Faint = Color.FromArgb(98, 122, 114);
            Label = Color.FromArgb(226, 133, 107);
            Value = Color.FromArgb(60, 201, 167);
            Brand = Color.FromArgb(0, 200, 83);
            BrandDark = Color.FromArgb(4, 21, 11);
            Danger = Color.FromArgb(229, 96, 79);
            Warn = Color.FromArgb(231, 176, 74);
            OwnBubble = Color.FromArgb(18, 38, 58);
            OwnBubbleBorder = Color.FromArgb(36, 71, 106);
            ParentBubble = Color.FromArgb(19, 48, 42);
            ParentBubbleBorder = Color.FromArgb(42, 74, 66);
            BrandText = Color.FromArgb(0, 200, 83);
            Highlight = Color.FromArgb(27, 58, 50);
        }
    }

    private static readonly List<PrivateFontCollection> Collections = new();
    private static readonly List<IntPtr> FontMemory = new();
    private static readonly FontFamily? MonoRegular;
    private static readonly FontFamily? MonoSemiBold;
    private static readonly FontFamily? SansRegular;
    private static readonly FontFamily? SansSemiBold;
    private static readonly FontFamily? SansBold;

    // GDI+ cannot pick a weight from a variable font, so the lock screen uses
    // one static Fredoka/Nunito face per weight (see tools/fonts).
    private static readonly FontFamily? DisplayMedium;
    private static readonly FontFamily? DisplaySemiBold;
    private static readonly FontFamily? BodyBold;
    private static readonly FontFamily? BodyExtraBold;

    static Theme()
    {
        MonoRegular = Load("OpControl.Parental.Helper.fonts.IBMPlexMono-Regular.ttf");
        MonoSemiBold = Load("OpControl.Parental.Helper.fonts.IBMPlexMono-SemiBold.ttf");
        SansRegular = Load("OpControl.Parental.Helper.fonts.Manrope-500.ttf");
        SansSemiBold = Load("OpControl.Parental.Helper.fonts.Manrope-600.ttf");
        SansBold = Load("OpControl.Parental.Helper.fonts.Manrope-700.ttf");
        DisplayMedium = Load("OpControl.Parental.Helper.fonts.Fredoka-Medium.ttf");
        DisplaySemiBold = Load("OpControl.Parental.Helper.fonts.Fredoka-SemiBold.ttf");
        BodyBold = Load("OpControl.Parental.Helper.fonts.Nunito-Bold.ttf");
        BodyExtraBold = Load("OpControl.Parental.Helper.fonts.Nunito-ExtraBold.ttf");
    }

    private static FontFamily? Load(string resource)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream is null) return null;
            // AddFontFile + delete the temp file used to make GDI+ fail later when
            // the Font was created. AddMemoryFont keeps the bytes valid for the
            // lifetime of the collection.
            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            var memory = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            var collection = new PrivateFontCollection();
            collection.AddMemoryFont(memory, bytes.Length);
            Collections.Add(collection);
            FontMemory.Add(memory);
            return collection.Families
                .OrderByDescending(f => f.Name.Length)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    public static Font Sans(float size, FontStyle style = FontStyle.Regular) =>
        Make(style.HasFlag(FontStyle.Bold) ? SansBold : SansRegular, "Segoe UI", size);

    public static Font SansSemi(float size) =>
        Make(SansSemiBold, "Segoe UI Semibold", size);

    public static Font Mono(float size, FontStyle style = FontStyle.Regular) =>
        Make(style.HasFlag(FontStyle.Bold) ? MonoSemiBold : MonoRegular, "Consolas", size);

    // Legacy accessors are used by GDI controls (labels, TextRenderer) that cannot
    // see a PrivateFontCollection. Keep the exact 0.17.3 system fallbacks here;
    // the lock overlay uses the private faces through the Lock* methods below.
    public static Font Display(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI Semibold", size, FontStyle.Regular);

    public static Font Body(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, FontStyle.Regular);

    public static Font DisplayPx(float pixels, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI Semibold", pixels, FontStyle.Regular, GraphicsUnit.Pixel);

    public static Font BodyPx(float pixels, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", pixels, FontStyle.Regular, GraphicsUnit.Pixel);

    /// <summary>Fredoka 600 for the lock title and the primary button.</summary>
    public static Font LockTitle(float pixels) => MakePx(DisplaySemiBold, "Segoe UI Semibold", pixels);

    /// <summary>Fredoka 500 for the lock reason line.</summary>
    public static Font LockReason(float pixels) => MakePx(DisplayMedium, "Segoe UI Semibold", pixels);

    /// <summary>Nunito 700 for the lock hint.</summary>
    public static Font LockHint(float pixels) => MakePx(BodyBold, "Segoe UI", pixels);

    /// <summary>Nunito 800 for "Parent override".</summary>
    public static Font LockSecondary(float pixels) => MakePx(BodyExtraBold, "Segoe UI", pixels);

    private static bool _fontCheckLogged;

    /// <summary>One-line runtime proof that the embedded families resolved (no system fallback).</summary>
    public static string FontCheckReport()
    {
        using var title = LockTitle(56);
        using var reason = LockReason(32);
        using var hint = LockHint(22);
        using var primary = LockTitle(26);
        using var secondary = LockSecondary(18);
        return "lock-fonts " +
               $"title={title.FontFamily.Name} " +
               $"reason={reason.FontFamily.Name} " +
               $"hint={hint.FontFamily.Name} " +
               $"primary={primary.FontFamily.Name} " +
               $"secondary={secondary.FontFamily.Name}";
    }

    public static string LogFontCheckOnce()
    {
        var report = FontCheckReport();
        if (_fontCheckLogged) return report;
        _fontCheckLogged = true;
        HelperPaths.LogEvent(report);
        return report;
    }

    private static Font Make(FontFamily? family, string fallback, float size)
    {
        if (family is not null)
        {
            try { return new Font(family, size, FontStyle.Regular); } catch { }
        }
        return new Font(fallback, size, FontStyle.Regular);
    }

    private static Font MakePx(FontFamily? family, string fallback, float pixels)
    {
        if (family is not null)
        {
            try { return new Font(family, pixels, FontStyle.Regular, GraphicsUnit.Pixel); } catch { }
        }
        return new Font(fallback, pixels, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    public static void StyleForm(Form form)
    {
        form.BackColor = Bg;
        form.ForeColor = Text;
        form.Font = Sans(9.5f);
    }

    public static void StyleLabel(Label label, bool labelStyle = false)
    {
        label.ForeColor = labelStyle ? Label : Text;
        label.Font = labelStyle ? Mono(9f) : Sans(9.5f);
        label.BackColor = Color.Transparent;
    }

    public static void StyleInput(Control control)
    {
        control.BackColor = Panel2;
        control.ForeColor = Text;
        control.Font = Sans(12f);
    }

    public static void StyleButton(Button button, bool primary = false, bool danger = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = danger ? Danger : primary ? Brand : Line2;
        button.BackColor = primary ? Brand : Panel2;
        button.ForeColor = primary ? BrandDark : danger ? Danger : Text;
        button.Font = Mono(9.5f, FontStyle.Bold);
        button.Height = Math.Max(button.Height, 38);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }
}
