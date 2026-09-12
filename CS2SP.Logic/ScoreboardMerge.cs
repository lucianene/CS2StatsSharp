namespace CS2SP.Logic;

/// <summary>
/// Scoreboard (action-tracking) values win when they are ahead of event
/// totals. Never shrink — a disconnected snapshot must stay intact if the
/// engine already reset or the controller is gone.
/// </summary>
public static class ScoreboardMerge
{
    public static void Apply(
        PlayerStats stats,
        int kills,
        int deaths,
        int assists,
        int damage,
        int headshots,
        int utilityDamage,
        int enemiesFlashed)
    {
        if (kills > stats.Kills) stats.Kills = kills;
        if (deaths > stats.Deaths) stats.Deaths = deaths;
        if (assists > stats.Assists) stats.Assists = assists;
        if (damage > stats.Damage) stats.Damage = damage;
        if (headshots > stats.Headshots) stats.Headshots = headshots;
        if (utilityDamage > stats.UtilityDamage) stats.UtilityDamage = utilityDamage;
        if (enemiesFlashed > stats.EnemiesFlashed) stats.EnemiesFlashed = enemiesFlashed;
    }

    /// <summary>
    /// Fold <paramref name="src"/> into <paramref name="dest"/> without lowering
    /// any counter. Used when both objects are the same session.
    /// </summary>
    public static void Absorb(PlayerStats dest, PlayerStats src)
    {
        if (ReferenceEquals(dest, src))
            return;

        Apply(dest, src.Kills, src.Deaths, src.Assists, src.Damage, src.Headshots, src.UtilityDamage, src.EnemiesFlashed);
        TakeMaxEventOnly(dest, src);
        TakeIdentity(dest, src);
    }

    /// <summary>
    /// <paramref name="src"/> is a new slot stub (reconnect before Steam auth,
    /// or a lingering controller after disconnect). Scoreboard fields that look
    /// like a copy of <paramref name="dest"/> are not added; extra event totals
    /// on the stub are.
    /// </summary>
    public static void MergeStub(PlayerStats dest, PlayerStats src)
    {
        if (ReferenceEquals(dest, src))
            return;

        dest.Kills = MergeScoreboard(dest.Kills, src.Kills);
        dest.Deaths = MergeScoreboard(dest.Deaths, src.Deaths);
        dest.Assists = MergeScoreboard(dest.Assists, src.Assists);
        dest.Damage = MergeScoreboard(dest.Damage, src.Damage);
        dest.Headshots = MergeScoreboard(dest.Headshots, src.Headshots);
        dest.UtilityDamage = MergeScoreboard(dest.UtilityDamage, src.UtilityDamage);
        dest.EnemiesFlashed = MergeScoreboard(dest.EnemiesFlashed, src.EnemiesFlashed);
        dest.Hits += src.Hits;
        dest.TeamKills += src.TeamKills;
        dest.BombPlants += src.BombPlants;
        dest.BombDefuses += src.BombDefuses;
        dest.BombExplodes += src.BombExplodes;
        dest.FireDamage += src.FireDamage;
        dest.Dinks += src.Dinks;
        dest.FirstKills += src.FirstKills;
        dest.ClutchKills += src.ClutchKills;
        dest.PistolKills += src.PistolKills;
        dest.SniperKills += src.SniperKills;
        dest.BlindKills += src.BlindKills;
        dest.BombKills += src.BombKills;
        dest.ChickenKills += src.ChickenKills;
        dest.TripleKills += src.TripleKills;
        dest.QuadroKills += src.QuadroKills;
        dest.PentaKills += src.PentaKills;
        dest.Mvps += src.Mvps;
        dest.AddExtras(src);
        TakeIdentity(dest, src);
        foreach (var id in src.UniqueKills)
            dest.UniqueKills.Add(id);
    }

    public static void Combine(PlayerStats dest, PlayerStats src)
    {
        if (ReferenceEquals(dest, src))
            return;
        if (dest.Disconnected)
            MergeStub(dest, src);
        else
            Absorb(dest, src);
    }

    /// <summary>
    /// Same value → copy, not a second session. Higher → engine scoreboard
    /// pulled ahead. Lower and non-zero → extra events on a reconnect stub.
    /// </summary>
    public static int MergeScoreboard(int dest, int src)
    {
        if (src <= 0)
            return dest;
        if (src == dest)
            return dest;
        if (src > dest)
            return src;
        return dest + src;
    }

    private static void TakeMaxEventOnly(PlayerStats dest, PlayerStats src)
    {
        if (src.Hits > dest.Hits) dest.Hits = src.Hits;
        if (src.TeamKills > dest.TeamKills) dest.TeamKills = src.TeamKills;
        if (src.BombPlants > dest.BombPlants) dest.BombPlants = src.BombPlants;
        if (src.BombDefuses > dest.BombDefuses) dest.BombDefuses = src.BombDefuses;
        if (src.BombExplodes > dest.BombExplodes) dest.BombExplodes = src.BombExplodes;
        if (src.FireDamage > dest.FireDamage) dest.FireDamage = src.FireDamage;
        if (src.Dinks > dest.Dinks) dest.Dinks = src.Dinks;
        if (src.FirstKills > dest.FirstKills) dest.FirstKills = src.FirstKills;
        if (src.ClutchKills > dest.ClutchKills) dest.ClutchKills = src.ClutchKills;
        if (src.PistolKills > dest.PistolKills) dest.PistolKills = src.PistolKills;
        if (src.SniperKills > dest.SniperKills) dest.SniperKills = src.SniperKills;
        if (src.BlindKills > dest.BlindKills) dest.BlindKills = src.BlindKills;
        if (src.BombKills > dest.BombKills) dest.BombKills = src.BombKills;
        if (src.ChickenKills > dest.ChickenKills) dest.ChickenKills = src.ChickenKills;
        if (src.TripleKills > dest.TripleKills) dest.TripleKills = src.TripleKills;
        if (src.QuadroKills > dest.QuadroKills) dest.QuadroKills = src.QuadroKills;
        if (src.PentaKills > dest.PentaKills) dest.PentaKills = src.PentaKills;
        if (src.Mvps > dest.Mvps) dest.Mvps = src.Mvps;
        dest.TakeMaxExtras(src);
        foreach (var id in src.UniqueKills)
            dest.UniqueKills.Add(id);
    }

    private static void TakeIdentity(PlayerStats dest, PlayerStats src)
    {
        if (src.Money > dest.Money) dest.Money = src.Money;
        if (dest.Team == 0) dest.Team = src.Team;
        if (dest.Name.Length == 0 && src.Name.Length > 0) dest.Name = src.Name;
    }
}
