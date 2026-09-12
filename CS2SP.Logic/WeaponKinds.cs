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

    public static readonly HashSet<string> Rifles = new(StringComparer.Ordinal)
    {
        "ak47", "m4a1", "m4a1_silencer", "m4a1_silencer_off", "famas", "galilar", "aug", "sg556", "sg553"
    };

    public static readonly HashSet<string> Smgs = new(StringComparer.Ordinal)
    {
        "mp9", "mp7", "mp5sd", "ump45", "p90", "bizon", "mac10"
    };

    public static readonly HashSet<string> Shotguns = new(StringComparer.Ordinal)
    {
        "nova", "xm1014", "mag7", "sawedoff"
    };

    /// <summary>
    /// CS2 events usually omit the <c>weapon_</c> prefix; CSS sometimes leaves it on.
    /// Aliases match the scoreboard names the C++ tables used.
    /// </summary>
    public static string Normalize(string? weapon)
    {
        if (string.IsNullOrWhiteSpace(weapon))
            return "";

        var s = weapon.Trim();
        if (s.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase))
            s = s[7..];
        s = s.ToLowerInvariant();
        return s switch
        {
            "hkp2000" => "p2000",
            "usp" => "usp_silencer",
            "incgrenade" => "inferno",
            "m4a4" => "m4a1",
            _ => s
        };
    }

    public static bool IsPistol(string? weapon) => Pistols.Contains(Normalize(weapon));
    public static bool IsSniper(string? weapon) => Snipers.Contains(Normalize(weapon));
    public static bool IsRifle(string? weapon) => Rifles.Contains(Normalize(weapon));
    public static bool IsSmg(string? weapon) => Smgs.Contains(Normalize(weapon));
    public static bool IsShotgun(string? weapon) => Shotguns.Contains(Normalize(weapon));
    public static bool IsBombKill(string? weapon) => Normalize(weapon) == "planted_c4";
    public static bool IsZeus(string? weapon) => Normalize(weapon) == "taser";
    public static bool IsHe(string? weapon) => Normalize(weapon) == "hegrenade";

    public static bool IsKnife(string? weapon)
    {
        var w = Normalize(weapon);
        return w.StartsWith("knife", StringComparison.Ordinal) || w is "bayonet" or "knifegg";
    }

    public static bool IsDelayedUtility(string? weapon)
    {
        var w = Normalize(weapon);
        return w is "hegrenade" or "inferno" or "molotov";
    }

    public static bool IsUtility(string? weapon) => IsDelayedUtility(weapon);
    public static bool IsFire(string? weapon)
    {
        var w = Normalize(weapon);
        return w is "inferno" or "molotov";
    }

    public static void CountThrow(PlayerStats stats, string? weapon)
    {
        switch (Normalize(weapon))
        {
            case "flashbang": stats.FlashThrown++; break;
            case "hegrenade": stats.HeThrown++; break;
            case "smokegrenade": stats.SmokeThrown++; break;
            case "molotov":
            case "inferno": stats.MolotovThrown++; break;
            case "decoy":
            case "decoygrenade": stats.DecoyThrown++; break;
        }
    }
}
