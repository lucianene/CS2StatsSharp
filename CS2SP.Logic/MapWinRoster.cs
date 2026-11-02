namespace CS2SP.Logic;

/// <summary>
/// Who won the map. Prefer people still connected when the level ends.
/// If the engine already dropped everyone (changelevel), the same decision
/// falls back to the parked roster so the win is not written as a loss.
/// </summary>
public static class MapWinRoster
{
    public const int TeamT = 2;
    public const int TeamCt = 3;

    public static HashSet<string> Winners(
        int ctRoundWins,
        int tRoundWins,
        IEnumerable<PlayerStats> live,
        IEnumerable<PlayerStats> parked)
    {
        var winners = new HashSet<string>(StringComparer.Ordinal);
        var team = WinningTeam(ctRoundWins, tRoundWins);
        if (team == 0)
            return winners;

        var source = live.Where(stats => Counts(stats) && stats.Team == team).ToList();
        if (source.Count == 0)
            source = parked.Where(stats => Counts(stats) && stats.Team == team).ToList();

        foreach (var stats in source)
            winners.Add(StatsPayload.SteamKey(stats.SteamId));

        return winners;
    }

    public static int WinningTeam(int ctRoundWins, int tRoundWins)
    {
        if (ctRoundWins > tRoundWins)
            return TeamCt;
        if (tRoundWins > ctRoundWins)
            return TeamT;
        return 0;
    }

    private static bool Counts(PlayerStats stats) =>
        !stats.IsBot && SteamIds.IsIndividual(stats.SteamId);
}
