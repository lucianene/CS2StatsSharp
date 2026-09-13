namespace CS2SP.Logic;

/// <summary>
/// Per-mod scoring behavior selected by <c>sp_mod</c>. Unknown / empty
/// names fall back to the first row (matchmaking). Uploads are not gated
/// here: every mod POSTs on the periodic timer, on <c>round_end</c> when
/// the engine fires it, and on a coalesced connect/warmup presence heartbeat
/// so the web live list refreshes before the first scored round. Round /
/// connect / leave POSTs reset the periodic clock so they do not stack.
/// A leaver is POSTed once (frozen mid-round kills), then omitted.
/// </summary>
public readonly record struct ModProfile(
    string Name,
    bool FreeForAll,
    bool ResetStateOnSpawn);

public static class ModProfiles
{
    /// <summary>Round-based teams, no mid-round respawn (CS2MM-style).</summary>
    public static readonly ModProfile Matchmaking = new("matchmaking", FreeForAll: false, ResetStateOnSpawn: false);

    /// <summary>FFA + instant respawn. PlayCup cs2dm.</summary>
    public static readonly ModProfile Deathmatch = new("deathmatch", FreeForAll: true, ResetStateOnSpawn: true);

    /// <summary>Team scoring + respawn waves + round_end. PlayCup cs2aim.</summary>
    public static readonly ModProfile Aim = new("aim", FreeForAll: false, ResetStateOnSpawn: true);

    /// <summary>Round-based teams, no mid-round respawn. PlayCup cs2casual.</summary>
    public static readonly ModProfile Casual = new("casual", FreeForAll: false, ResetStateOnSpawn: false);

    /// <summary>
    /// Team scoring (humans vs zombies). Zombies respawn so DeadThisRound must
    /// clear on spawn; humans stay dead until round_start. PlayCup cs2zm.
    /// </summary>
    public static readonly ModProfile Zombie = new("zombie", FreeForAll: false, ResetStateOnSpawn: true);

    /// <summary>Round-based teams. Not a PlayCup container; same flags as casual.</summary>
    public static readonly ModProfile Retake = new("retake", FreeForAll: false, ResetStateOnSpawn: false);

    public static readonly ModProfile[] All =
    [
        Matchmaking,
        Deathmatch,
        Aim,
        Casual,
        Zombie,
        Retake,
    ];

    public static ModProfile Resolve(string? name)
    {
        name = CvarText.Clean(name);
        if (name.Length > 0)
        {
            foreach (var p in All)
            {
                if (string.Equals(name, p.Name, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
        }

        return All[0];
    }
}
