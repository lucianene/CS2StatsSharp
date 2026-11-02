namespace CS2SP.Logic;

/// <summary>
/// One database row per map. An empty <c>sp_match_id</c> used to keep a
/// single session, so the next map overwrote the finished map's stats.
/// </summary>
public static class MapSession
{
    public static string ForMap(string? mapName, long unixSeconds)
    {
        var map = MapNames.Normalize(mapName);
        if (map.Length == 0)
            map = "unknown";
        if (unixSeconds < 0)
            unixSeconds = 0;
        return $"{map}_{unixSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
