namespace CS2SP.Logic;

public enum KillOutcome
{
    Skip,
    TeamKill,
    Kill
}

public static class KillCredit
{
    /// <summary>
    /// A dead attacker can still credit delayed utility (HE / molotov / inferno).
    /// Direct damage after death is ignored.
    /// </summary>
    public static bool ShouldCreditDamage(bool attackerDeadThisRound, bool sameTeam, bool freeForAll, bool delayedUtility)
    {
        if (!freeForAll && sameTeam)
            return false;
        if (attackerDeadThisRound && !delayedUtility)
            return false;
        return true;
    }

    public static bool ShouldCreditFlash(bool attackerIsVictim, bool sameTeam, bool freeForAll, float blindDuration)
    {
        if (attackerIsVictim)
            return false;
        if (!freeForAll && sameTeam)
            return false;
        return blindDuration > 0f;
    }

    public static KillOutcome ClassifyDeath(bool killerDeadThisRound, bool sameTeam, bool freeForAll)
    {
        if (killerDeadThisRound)
            return KillOutcome.Skip;
        if (!freeForAll && sameTeam)
            return KillOutcome.TeamKill;
        return KillOutcome.Kill;
    }

    public static void ApplyMultiKills(PlayerStats killer, int roundKills)
    {
        if (roundKills == 3) killer.TripleKills++;
        if (roundKills == 4) killer.QuadroKills++;
        if (roundKills == 5) killer.PentaKills++;
    }

    public static bool IsDink(int hitgroup, int victimHealthAfter) =>
        hitgroup == 1 && victimHealthAfter > 0;

    public static bool IsClutch(int aliveTeammates) => aliveTeammates == 0;
}
