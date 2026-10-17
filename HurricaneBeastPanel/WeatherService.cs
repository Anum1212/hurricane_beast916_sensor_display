using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace HurricaneBeastPanel;

public enum WeatherKind { Clear, PartlyCloudy, Cloudy, Fog, Rain, Snow, Storm }

/// <summary>Current weather for the chosen place. Temperatures are in °C and wind in km/h.</summary>
public sealed record WeatherInfo(WeatherKind Kind, bool IsDay, float TempC, float FeelsC, float HighC, float LowC,
    float Humidity, float WindKmh, float RainChance, string Condition, string City);

/// <summary>
/// Looks up the weather with Open-Meteo (free, no account). The place is the "Weather location" setting, or, when that is
/// empty, the approximate location of this computer's internet connection (ipwho.is). Nothing is requested until a
/// theme actually shows weather, and the data is refreshed every 15 minutes.
/// </summary>
public static class WeatherService
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    static readonly TimeSpan Refresh = TimeSpan.FromMinutes(15), Retry = TimeSpan.FromSeconds(60);

    static WeatherInfo? _current;
    static DateTime _lastOk = DateTime.MinValue, _lastTry = DateTime.MinValue;
    static string _location = "";
    static (double lat, double lon, string city)? _place;
    static int _busy;

    static WeatherService() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("HurricaneBeastPanel/1.0");

    public static WeatherInfo? Current => _current;

    /// <summary>Called whenever something on screen shows weather; starts a lookup in the background when the data is old.</summary>
    public static void Touch(string location)
    {
        location = (location ?? "").Trim();
        if (!string.Equals(location, _location, StringComparison.OrdinalIgnoreCase))
        {
            _location = location;
            _place = null;
            _lastOk = DateTime.MinValue;
            _lastTry = DateTime.MinValue;
        }
        var now = DateTime.UtcNow;
        bool due = now - _lastOk > Refresh && now - _lastTry > Retry;
        if (!due || Interlocked.Exchange(ref _busy, 1) == 1) return;
        _lastTry = now;
        Task.Run(async () =>
        {
            try
            {
                _place ??= await FindPlaceAsync(_location);
                if (_place is { } p)
                {
                    _current = await FetchAsync(p.lat, p.lon, p.city);
                    _lastOk = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { Log.Write("Weather lookup failed: " + ex.Message); }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });
    }

    static async Task<(double, double, string)?> FindPlaceAsync(string location)
    {
        if (location.Length == 0)
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync("https://ipwho.is/"));
            var r = doc.RootElement;
            if (r.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False) return null;
            return (r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble(),
                r.TryGetProperty("city", out var c) ? c.GetString() ?? "" : "");
        }
        // "Paris" or "Paris, France": only the part before the comma is searched, the rest narrows the match.
        string[] parts = location.Split(',', 2, StringSplitOptions.TrimEntries);
        string url = "https://geocoding-api.open-meteo.com/v1/search?count=10&language=en&name=" + Uri.EscapeDataString(parts[0]);
        using var geo = JsonDocument.Parse(await Http.GetStringAsync(url));
        if (!geo.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
        JsonElement pick = results[0];
        if (parts.Length > 1 && parts[1].Length > 0)
        {
            foreach (var item in results.EnumerateArray())
            {
                string hay = string.Join(" ", new[] { "country", "admin1", "country_code" }
                    .Select(k => item.TryGetProperty(k, out var v) ? v.GetString() : ""));
                if (hay.Contains(parts[1], StringComparison.OrdinalIgnoreCase)) { pick = item; break; }
            }
        }
        return (pick.GetProperty("latitude").GetDouble(), pick.GetProperty("longitude").GetDouble(),
            pick.TryGetProperty("name", out var n) ? n.GetString() ?? parts[0] : parts[0]);
    }

    static async Task<WeatherInfo> FetchAsync(double lat, double lon, string city)
    {
        string url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={lat:0.####}&longitude={lon:0.####}") +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day" +
            "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=1";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        var cur = doc.RootElement.GetProperty("current");
        var daily = doc.RootElement.GetProperty("daily");
        float F(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : float.NaN;
        float First(string name) => daily.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array && a.GetArrayLength() > 0
            && a[0].ValueKind == JsonValueKind.Number ? a[0].GetSingle() : float.NaN;
        int code = cur.TryGetProperty("weather_code", out var c) ? c.GetInt32() : 0;
        bool day = !cur.TryGetProperty("is_day", out var d) || d.GetInt32() != 0;
        var (kind, text) = Describe(code, day);
        return new WeatherInfo(kind, day, F(cur, "temperature_2m"), F(cur, "apparent_temperature"), First("temperature_2m_max"),
            First("temperature_2m_min"), F(cur, "relative_humidity_2m"), F(cur, "wind_speed_10m"), First("precipitation_probability_max"), text, city);
    }

    /// <summary>Turns a WMO weather code into the picture to show and a short description.</summary>
    public static (WeatherKind kind, string text) Describe(int code, bool day) => code switch
    {
        0 => (WeatherKind.Clear, day ? "Sunny" : "Clear"),
        1 => (WeatherKind.Clear, day ? "Mostly sunny" : "Mostly clear"),
        2 => (WeatherKind.PartlyCloudy, "Partly cloudy"),
        3 => (WeatherKind.Cloudy, "Overcast"),
        45 or 48 => (WeatherKind.Fog, "Fog"),
        51 => (WeatherKind.Rain, "Light drizzle"),
        53 => (WeatherKind.Rain, "Drizzle"),
        55 => (WeatherKind.Rain, "Heavy drizzle"),
        56 or 57 => (WeatherKind.Rain, "Freezing drizzle"),
        61 => (WeatherKind.Rain, "Light rain"),
        63 => (WeatherKind.Rain, "Rain"),
        65 => (WeatherKind.Rain, "Heavy rain"),
        66 or 67 => (WeatherKind.Rain, "Freezing rain"),
        71 => (WeatherKind.Snow, "Light snow"),
        73 => (WeatherKind.Snow, "Snow"),
        75 => (WeatherKind.Snow, "Heavy snow"),
        77 => (WeatherKind.Snow, "Snow grains"),
        80 or 81 => (WeatherKind.Rain, "Rain showers"),
        82 => (WeatherKind.Rain, "Heavy showers"),
        85 or 86 => (WeatherKind.Snow, "Snow showers"),
        95 => (WeatherKind.Storm, "Thunderstorm"),
        96 or 99 => (WeatherKind.Storm, "Thunderstorm, hail"),
        _ => (WeatherKind.Cloudy, "Cloudy"),
    };
}
