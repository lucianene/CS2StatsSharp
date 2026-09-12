namespace CS2SP.Logic;

/// <summary>
/// Kill-style extras from <c>player_death</c> (noscope, wallbang, weapon class).
/// Engine-free so xUnit can cover it.
/// </summary>
public static class KillFlavor
{
    public static void Apply(
        PlayerStats killer,
        string? weapon,
        bool noscope,
        bool thruSmoke,
        bool inAir,
        bool attackerBlind,
        int penetrated,
        float distance)
    {
        var w = WeaponKinds.Normalize(weapon);
        if (WeaponKinds.IsPistol(w)) killer.PistolKills++;
        if (WeaponKinds.IsSniper(w)) killer.SniperKills++;
        if (WeaponKinds.IsRifle(w)) killer.RifleKills++;
        if (WeaponKinds.IsSmg(w)) killer.SmgKills++;
        if (WeaponKinds.IsShotgun(w)) killer.ShotgunKills++;
        if (WeaponKinds.IsKnife(w)) killer.KnifeKills++;
        if (WeaponKinds.IsZeus(w)) killer.ZeusKills++;
        if (WeaponKinds.IsHe(w)) killer.HeKills++;
        if (WeaponKinds.IsFire(w)) killer.MolotovKills++;
        if (WeaponKinds.IsBombKill(w)) killer.BombKills++;

        if (noscope && WeaponKinds.IsSniper(w))
            killer.NoScopeKills++;
        if (thruSmoke)
            killer.SmokeKills++;
        if (inAir)
            killer.AirKills++;
        if (penetrated > 0)
            killer.WallbangKills++;
        if (attackerBlind)
            killer.BlindKills++;
        if (distance > killer.LongestKillDistance)
            killer.LongestKillDistance = distance;
    }

    public static void ApplyNotices(PlayerStats killer, int dominated, int revenge, int wipe)
    {
        if (dominated > 0) killer.Dominations++;
        if (revenge > 0) killer.Revenges++;
        if (wipe > 0) killer.TeamWipes++;
    }
}
