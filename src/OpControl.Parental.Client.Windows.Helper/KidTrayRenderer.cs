// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

namespace OpControl.Parental.Helper;

internal sealed class KidMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => KidTheme.Card;
    public override Color ImageMarginGradientBegin => KidTheme.Card;
    public override Color ImageMarginGradientMiddle => KidTheme.Card;
    public override Color ImageMarginGradientEnd => KidTheme.Card;
    public override Color MenuBorder => KidTheme.CardBorder;
    public override Color MenuItemBorder => KidTheme.CardBorder;
    public override Color MenuItemSelected => KidTheme.Highlight;
    public override Color MenuItemSelectedGradientBegin => KidTheme.Highlight;
    public override Color MenuItemSelectedGradientEnd => KidTheme.Highlight;
    public override Color SeparatorDark => KidTheme.SubBorder;
    public override Color SeparatorLight => KidTheme.SubBorder;
}

internal sealed class KidMenuRenderer : ToolStripProfessionalRenderer
{
    public KidMenuRenderer() : base(new KidMenuColors()) { RoundedEdges = false; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(KidTheme.Card);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        KidDraw.FillRound(e.Graphics, new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2), 12, KidTheme.Highlight);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? KidTheme.Text : KidTheme.Faint;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(KidTheme.SubBorder);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }
}
