using System.Drawing.Drawing2D;

namespace HurricaneBeastPanel.Ui;

/// <summary>Colours for context menus in the dark purple style.</summary>
sealed class MenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Pal.Surface;
    public override Color ImageMarginGradientBegin => Pal.Surface;
    public override Color ImageMarginGradientMiddle => Pal.Surface;
    public override Color ImageMarginGradientEnd => Pal.Surface;
    public override Color MenuBorder => Pal.Outline;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Pal.PrimaryContainer;
    public override Color MenuItemSelectedGradientBegin => Pal.PrimaryContainer;
    public override Color MenuItemSelectedGradientEnd => Pal.PrimaryContainer;
    public override Color SeparatorDark => Pal.Outline;
    public override Color SeparatorLight => Pal.Outline;
    public override Color CheckBackground => Pal.PrimaryContainer;
    public override Color CheckSelectedBackground => Pal.PrimaryContainer;
    public override Color CheckPressedBackground => Pal.PrimaryContainer;
}

sealed class MenuRenderer : ToolStripProfessionalRenderer
{
    public MenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Pal.Text : Pal.TextFaint;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Pal.TextDim;
        base.OnRenderArrow(e);
    }
}

public static class UiKit
{
    /// <summary>A context menu / drop-down menu in the dark purple style.</summary>
    public static ContextMenuStrip Menu() => new()
    {
        Renderer = new MenuRenderer(), BackColor = Pal.Surface, ForeColor = Pal.Text, Font = Pal.Ui(9.5f),
        ShowImageMargin = false, ShowCheckMargin = false, Padding = new Padding(4),
    };

    public static ToolStripMenuItem MenuItem(string text, Action onClick, bool enabled = true)
    {
        var item = new ToolStripMenuItem(text) { ForeColor = Pal.Text, Enabled = enabled, Padding = new Padding(6, 6, 6, 6) };
        item.Click += (_, _) => onClick();
        return item;
    }

    /// <summary>Opens a dark menu directly below a control.</summary>
    public static void ShowBelow(ContextMenuStrip menu, Control anchor) =>
        menu.Show(anchor, new Point(0, anchor.Height + 4));

    /// <summary>Tooltips drawn in the app style.</summary>
    public static ToolTip Tips()
    {
        var tip = new ToolTip { OwnerDraw = true, InitialDelay = 450, ReshowDelay = 100, AutoPopDelay = 6000 };
        var font = Pal.Ui(9f);
        tip.Popup += (_, e) =>
        {
            var size = TextRenderer.MeasureText(tip.GetToolTip(e.AssociatedControl), font);
            e.ToolTipSize = new Size(size.Width + 20, size.Height + 14);
        };
        tip.Draw += (_, e) =>
        {
            var g = e.Graphics;
            Pal.Quality(g);
            var r = new RectangleF(0.5f, 0.5f, e.Bounds.Width - 1.5f, e.Bounds.Height - 1.5f);
            Pal.FillRound(g, r, 8, Pal.SurfaceHi);
            Pal.StrokeRound(g, r, 8, Pal.Outline);
            Pal.DrawText(g, e.ToolTipText, font, Pal.Text, r, StringAlignment.Center);
        };
        return tip;
    }
}

/// <summary>Message boxes in the app style.</summary>
public static class UiDialog
{
    /// <summary>
    /// Builds (without showing) a message dialog with absolute layout in 96-dpi design units; DpiHelper.Fit scales it.
    /// Auto-sizing panels collapsed this dialog before, so every size here is explicit.
    /// </summary>
    internal static UiForm Build(string title, string message, (string text, DialogResult result, ButtonStyle style)[] buttons)
    {
        const int width = 480, pad = 28, textWidth = width - pad * 2;
        // Rough line count for the body text (about 54 characters per line at this width).
        int lines = message.Split('\n').Sum(p => Math.Max(1, (int)Math.Ceiling(p.TrimEnd('\r').Length / 54.0)));
        int bodyHeight = lines * 24;
        int bodyTop = 66, buttonsTop = bodyTop + bodyHeight + 26, height = buttonsTop + 38 + 26;

        var form = new UiForm
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(width, height),
            KeyPreview = true,
        };
        form.Controls.Add(new Label
        {
            Text = title, AutoSize = false, Location = new Point(pad, 22), Size = new Size(textWidth, 32),
            ForeColor = Pal.Text, Font = Pal.Ui(13f, FontStyle.Bold), BackColor = Color.Transparent,
        });
        form.Controls.Add(new Label
        {
            Text = message, AutoSize = false, Location = new Point(pad, bodyTop), Size = new Size(textWidth, bodyHeight),
            ForeColor = Pal.TextDim, Font = Pal.Ui(10f), BackColor = Color.Transparent,
        });

        // Buttons run right to left from the bottom-right corner.
        int x = width - pad;
        DialogResult escape = buttons.Length > 0 ? buttons[0].result : DialogResult.Cancel;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            var (text, result, style) = buttons[i];
            int w = Math.Max(88, text.Length * 9 + 44);
            var b = new MButton { Text = text, Style = style, Size = new Size(w, 38), Location = new Point(x - w, buttonsTop) };
            var res = result;
            b.Click += (_, _) => form.DialogResult = res;
            form.Controls.Add(b);
            x -= w + 10;
            if (result is DialogResult.Cancel) escape = result;
        }
        form.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) form.DialogResult = escape; };
        DpiHelper.Fit(form);
        return form;
    }

    public static DialogResult Show(IWin32Window? owner, string title, string message,
        params (string text, DialogResult result, ButtonStyle style)[] buttons)
    {
        using var form = Build(title, message, buttons);
        return form.ShowDialog(owner);
    }

    public static bool Confirm(IWin32Window? owner, string title, string message, string ok = "OK", string cancel = "Cancel", bool danger = false) =>
        Show(owner, title, message, (cancel, DialogResult.Cancel, ButtonStyle.Text), (ok, DialogResult.OK, ButtonStyle.Filled)) == DialogResult.OK;

    public static void Info(IWin32Window? owner, string title, string message) =>
        Show(owner, title, message, ("OK", DialogResult.OK, ButtonStyle.Filled));

    /// <summary>Yes = save, No = discard, Cancel = stay.</summary>
    public static DialogResult SaveChanges(IWin32Window? owner, string title, string message) =>
        Show(owner, title, message, ("Cancel", DialogResult.Cancel, ButtonStyle.Text), ("Don't save", DialogResult.No, ButtonStyle.Outlined), ("Save", DialogResult.Yes, ButtonStyle.Filled));
}

/// <summary>Small single-line text prompt in the app style.</summary>
public static class InputDialog
{
    public static string? Prompt(IWin32Window owner, string title, string label, string initial)
    {
        using var form = new UiForm
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(420, 190),
        };
        var heading = new Label { Text = title, AutoSize = true, ForeColor = Pal.Text, Font = Pal.Ui(12.5f, FontStyle.Bold), BackColor = Color.Transparent, Location = new Point(24, 20) };
        var caption = new Label { Text = label, AutoSize = true, ForeColor = Pal.TextDim, BackColor = Color.Transparent, Location = new Point(24, 58) };
        var box = new MTextBox { Text = initial, Location = new Point(24, 82), Size = new Size(372, 38) };
        var ok = new MButton { Text = "OK", Style = ButtonStyle.Filled, Location = new Point(212, 136), Size = new Size(88, 38) };
        var cancel = new MButton { Text = "Cancel", Style = ButtonStyle.Text, Location = new Point(308, 136), Size = new Size(88, 38) };
        form.Controls.AddRange(new Control[] { heading, caption, box, ok, cancel });
        ok.Click += (_, _) => form.DialogResult = DialogResult.OK;
        cancel.Click += (_, _) => form.DialogResult = DialogResult.Cancel;
        form.KeyPreview = true;
        form.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { form.DialogResult = DialogResult.OK; e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) form.DialogResult = DialogResult.Cancel;
        };
        form.Shown += (_, _) => { box.Focus(); box.SelectAll(); };
        DpiHelper.Fit(form);
        return form.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
    }
}
