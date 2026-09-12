namespace CS2SP.Logic;

public static class WeaponKinds
{
    public static readonly HashSet<string> Pistols = new(StringComparer.Ordinal)
    {
        "glock", "usp_silencer", "p2000", "p250", "tec9", "cz75a", "fiveseven", "deagle", "elite", "revolver"
    };

    public static readonly HashSet<string> Snipers = new(StringComparer.Ordinal)
    {
        "awp", "ssg08", "scar20", "g3sg1"
    };

    public static bool IsPistol(string? weapon) => weapon is not null && Pistols.Contains(weapon);
    public static bool IsSniper(string? weapon) => weapon is not null && Snipers.Contains(weapon);
    public static bool IsBombKill(string? weapon) => weapon == "planted_c4";

    public static bool IsDelayedUtility(string? weapon) =>
        weapon is "hegrenade" or "inferno" or "molotov";

    public static bool IsUtility(string? weapon) => IsDelayedUtility(weapon);
    public static bool IsFire(string? weapon) => weapon is "inferno" or "molotov";
}
