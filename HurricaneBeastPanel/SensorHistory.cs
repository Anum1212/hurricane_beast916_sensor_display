namespace HurricaneBeastPanel;

/// <summary>Recent values of every numeric token (about one sample per second) for the graph widgets.</summary>
public static class SensorHistory
{
    const int MaxSamples = 600;

    static readonly object Lock = new();
    static readonly Dictionary<string, List<float>> Data = new();
    static readonly Dictionary<string, (float min, float max)> Extremes = new();

    /// <summary>Lowest value seen since the app started, or null if nothing was recorded.</summary>
    public static float? Min(string key) { lock (Lock) return Extremes.TryGetValue(key, out var e) ? e.min : null; }

    /// <summary>Highest value seen since the app started, or null if nothing was recorded.</summary>
    public static float? Max(string key) { lock (Lock) return Extremes.TryGetValue(key, out var e) ? e.max : null; }

    /// <summary>Records one sample per numeric token (NaN when the sensor has no value).</summary>
    public static void Push(SensorSnapshot snapshot)
    {
        lock (Lock)
        {
            foreach (string key in Tokens.HistoryKeys)
            {
                if (!Data.TryGetValue(key, out var list)) Data[key] = list = new List<float>();
                float? value = Tokens.Number(key, snapshot);
                list.Add(value ?? float.NaN);
                if (value.HasValue && value.Value > 0)
                    Extremes[key] = Extremes.TryGetValue(key, out var e) ? (Math.Min(e.min, value.Value), Math.Max(e.max, value.Value)) : (value.Value, value.Value);
                if (list.Count > MaxSamples) list.RemoveRange(0, list.Count - MaxSamples);
            }
        }
    }

    /// <summary>The last <paramref name="count"/> samples, oldest first. Missing leading samples are NaN.</summary>
    public static float[] Get(string key, int count)
    {
        var result = new float[count];
        Array.Fill(result, float.NaN);
        lock (Lock)
        {
            if (!Data.TryGetValue(key, out var list)) return result;
            int take = Math.Min(count, list.Count);
            for (int i = 0; i < take; i++) result[count - take + i] = list[list.Count - take + i];
        }
        return result;
    }
}
