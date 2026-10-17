using System.Reflection;

namespace HurricaneBeastPanel;

static class DpiHelper
{
    /// <summary>
    /// True while <see cref="Fit"/> is scaling a form. Layout handlers that already multiply by the display scale
    /// must skip their work then (the scaling would apply on top), and are run again afterwards.
    /// </summary>
    [ThreadStatic] public static bool Scaling;

    static readonly MethodInfo? OnResize = typeof(Control).GetMethod("OnResize", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Scales a code-built form and all its children from 96-dpi design units to the monitor's DPI.
    /// Call at the end of the constructor, after every control has been added.
    /// </summary>
    public static void Fit(Form form)
    {
        form.AutoScaleMode = AutoScaleMode.None;
        float s = form.DeviceDpi / 96f;
        if (s > 1.01f)
        {
            Scaling = true;
            try { form.Scale(new SizeF(s, s)); }
            finally { Scaling = false; }
        }
        // Window size changed after the start position was computed, so centre again.
        if (form.StartPosition == FormStartPosition.CenterScreen)
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(
                area.X + Math.Max(0, (area.Width - form.Width) / 2),
                area.Y + Math.Max(0, (area.Height - form.Height) / 2));
        }
        // Let every layout handler run once with the final sizes.
        foreach (var c in Descendants(form)) OnResize?.Invoke(c, new object[] { EventArgs.Empty });
    }

    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control c in root.Controls)
        {
            foreach (var d in Descendants(c)) yield return d;
            yield return c;
        }
    }
}
