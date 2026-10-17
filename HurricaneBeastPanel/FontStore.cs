using System.Drawing.Text;

namespace HurricaneBeastPanel;

/// <summary>Installed fonts plus any .ttf/.otf files the user drops into the fonts folder.</summary>
public static class FontStore
{
    static readonly PrivateFontCollection Custom = new();
    static readonly object Lock = new();
    static bool _loaded;

    public static string Dir => Path.Combine(AppSettings.Dir, "fonts");

    public static void EnsureLoaded()
    {
        lock (Lock)
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                Directory.CreateDirectory(Dir);
                foreach (string file in Directory.EnumerateFiles(Dir).Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
                {
                    try { Custom.AddFontFile(file); }
                    catch (Exception ex) { Log.Write($"Font skipped ({Path.GetFileName(file)}): {ex.Message}"); }
                }
            }
            catch (Exception ex) { Log.Write("Fonts folder failed: " + ex.Message); }
        }
    }

    /// <summary>Names of every font a theme can use, installed ones and custom ones, sorted.</summary>
    public static List<string> AllFamilies()
    {
        EnsureLoaded();
        using var installed = new InstalledFontCollection();
        return installed.Families.Select(f => f.Name)
            .Concat(Custom.Families.Select(f => f.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Creates a font in pixels. Falls back to a style the family has, then to Segoe UI, instead of throwing.</summary>
    public static Font Create(string family, float pixels, FontStyle style)
    {
        EnsureLoaded();
        pixels = Math.Max(pixels, 1);
        var custom = Custom.Families.FirstOrDefault(f => f.Name.Equals(family, StringComparison.OrdinalIgnoreCase));
        if (custom != null)
        {
            foreach (var s in new[] { style, FontStyle.Regular, FontStyle.Bold, FontStyle.Italic })
                if (custom.IsStyleAvailable(s)) return new Font(custom, pixels, s, GraphicsUnit.Pixel);
        }
        foreach (var s in new[] { style, style & ~FontStyle.Italic, style & ~FontStyle.Bold, FontStyle.Regular })
        {
            try { return new Font(family, pixels, s, GraphicsUnit.Pixel); }
            catch (ArgumentException) { /* this family does not have that style */ }
        }
        return new Font("Segoe UI", pixels, FontStyle.Regular, GraphicsUnit.Pixel);
    }
}