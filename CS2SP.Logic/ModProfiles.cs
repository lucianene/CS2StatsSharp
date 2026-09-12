namespace CS2SP.Logic;

/// <summary>
/// Per-mod scoring behavior selected by <c>sp_mod</c>. Unknown / empty
/// names fall back to the first row (matchmaking). Uploads are not gated
/// here: every mod POSTs on the periodic timer, and also on <c>round_end</c>
/// when the engine fires it.
/// </summary>
public readonly record struct ModProfile(
    string Name,
    bool FreeForAll,
    bool ResetStateOnSpawn);

public static class ModProfiles
{
    public static readonly ModProfile Matchmaking = new("matchmaking", FreeForAll: false, ResetStateOnSpawn: false);
    public static readonly ModProfile Deathmatch = new("deathmatch", FreeForAll: true, ResetStateOnSpawn: true);
    public static readonly ModProfile Aim = new("aim", FreeForAll: false, ResetStateOnSpawn: true);

    // Add rows here for retake / zombie / … — handlers only read these flags.
    public static readonly ModProfile[] All =
    [
        Matchmaking,
        Deathmatch,
        Aim,
        // new("retake", FreeForAll: false, ResetStateOnSpawn: false),
        // new("zombie", FreeForAll: false, ResetStateOnSpawn: true),
    ];

    public static ModProfile Resolve(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
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
