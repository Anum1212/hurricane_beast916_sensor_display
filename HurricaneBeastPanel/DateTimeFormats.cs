using System.Globalization;

namespace HurricaneBeastPanel;

/// <summary>Ready-made date and time layouts for the "+ Date / Time" menu and the text editor.</summary>
public static class DateTimeFormats
{
    public sealed record Entry(string Label, string Format, bool IsPart = false);

    public sealed record Group(string Title, IReadOnlyList<Entry> Items);

    static Entry F(string format) => new(format, format);
    static Entry P(string label, string format) => new(label, format, IsPart: true);

    public static readonly IReadOnlyList<Group> Groups = new[]
    {
        new Group("Date", new[]
        {
            F("MM/dd/yyyy"), F("MM/dd/yy"), F("M/d/yyyy"), F("M/d/yy"),
            F("dd/MM/yyyy"), F("dd/MM/yy"), F("d/M/yyyy"), F("d/M/yy"),
            F("yyyy/MM/dd"), F("yy/MM/dd"),
            F("yyyy-MM-dd"), F("yy-MM-dd"), F("MM-dd-yyyy"), F("MM-dd-yy"), F("dd-MM-yyyy"), F("dd-MM-yy"),
            F("dd.MM.yyyy"), F("dd.MM.yy"), F("yyyy.MM.dd"), F("yyyyMMdd"),
            F("MMM d, yyyy"), F("MMMM d, yyyy"), F("d MMM yyyy"), F("d MMMM yyyy"),
            F("MMM d"), F("MMMM d"), F("d MMM"), F("d MMMM"),
            F("ddd, MMM d"), F("ddd, MMM d, yyyy"), F("ddd d MMM yyyy"),
            F("dddd, MMMM d"), F("dddd, MMMM d, yyyy"), F("dddd d MMMM"), F("dddd d MMMM yyyy"),
            F("MMM yyyy"), F("MMMM yyyy"), F("MM/yyyy"), F("MM/yy"),
        }),
        new Group("Date parts (one at a time)", new[]
        {
            P("Year (2026)", "yyyy"), P("Year, 2 digits (26)", "yy"),
            P("Month number (4)", "%M"), P("Month number, 2 digits (04)", "MM"),
            P("Month name, short (Apr)", "MMM"), P("Month name, full (April)", "MMMM"),
            P("Day of month (7)", "%d"), P("Day of month, 2 digits (07)", "dd"),
            P("Weekday, short (Tue)", "ddd"), P("Weekday, full (Tuesday)", "dddd"),
        }),
        new Group("Time", new[]
        {
            F("HH:mm"), F("HH:mm:ss"), F("H:mm"), F("H:mm:ss"),
            F("hh:mm tt"), F("h:mm tt"), F("hh:mm:ss tt"), F("h:mm:ss tt"), F("hh:mm"), F("h:mm"),
            F("HHmm"), F("HHmmss"), F("mm:ss"),
        }),
        new Group("Time parts (one at a time)", new[]
        {
            P("Hour, 24-hour (9 / 21)", "%H"), P("Hour, 24-hour, 2 digits (09 / 21)", "HH"),
            P("Hour, 12-hour (9)", "%h"), P("Hour, 12-hour, 2 digits (09)", "hh"),
            P("Minute (5)", "%m"), P("Minute, 2 digits (05)", "mm"),
            P("Second (7)", "%s"), P("Second, 2 digits (07)", "ss"),
            P("AM / PM", "tt"), P("A / P", "%t"),
        }),
        new Group("Date and time", new[]
        {
            F("MM/dd/yyyy HH:mm"), F("MM/dd/yy hh:mm tt"), F("dd/MM/yyyy HH:mm:ss"), F("yyyy-MM-dd HH:mm:ss"),
            F("yyyy-MM-dd HH:mm"), F("ddd MMM d  h:mm tt"), F("dddd, MMMM d  HH:mm"), F("MMM d, yyyy h:mm tt"),
            F("d MMM yyyy  HH:mm"),
        }),
    };

    /// <summary>The token text for a format, e.g. <c>{time:MM/dd/yy}</c>.</summary>
    public static string Token(string format) => "{time:" + format + "}";

    /// <summary>What the format looks like right now, for menu captions.</summary>
    public static string Sample(string format)
    {
        try { return DateTime.Now.ToString(format.Length == 1 ? "%" + format : format, CultureInfo.CurrentCulture); }
        catch (FormatException) { return "?"; }
    }

    /// <summary>All formats as flat (caption, token) pairs, for the insert box in the text editor.</summary>
    public static IEnumerable<(string Caption, string Token)> Flat() =>
        Groups.SelectMany(g => g.Items.Select(i => ($"{g.Title.Split(' ')[0]}:  {i.Label}   ->   {Sample(i.Format)}", Token(i.Format))));
}
