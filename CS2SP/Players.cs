using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using CS2SP.Logic;

namespace CS2SP;

public static class Players
{
    public static bool IsValid(CCSPlayerController? p)
    {
        if (p is null)
            return false;
        try
        {
            return p.IsValid && !p.IsHLTV;
        }
        catch (NativeException)
        {
            return false;
        }
    }

    public static bool IsHuman(CCSPlayerController? p)
    {
        if (!IsValid(p))
            return false;
        try
        {
            return !p!.IsBot;
        }
        catch (NativeException)
        {
            return false;
        }
    }

    /// <summary>
    /// Still in the session. After <c>OnClientDisconnect</c> the controller can
    /// linger as Disconnecting/Disconnected with a zeroed scoreboard — do not
    /// walk those on periodic/round POSTs.
    /// </summary>
    public static bool IsLiveHuman(CCSPlayerController? p)
    {
        if (!IsHuman(p))
            return false;
        try
        {
            return p!.Connected == PlayerConnectedState.PlayerConnected;
        }
        catch (NativeException)
        {
            return true;
        }
    }

    public static string Name(CCSPlayerController player)
    {
        try
        {
            return player.PlayerName ?? "";
        }
        catch (NativeException)
        {
            return "";
        }
    }

    /// <summary>Null if team cannot be read (entity system down).</summary>
    public static bool? SameTeam(CCSPlayerController a, CCSPlayerController b)
    {
        try
        {
            return a.Team == b.Team;
        }
        catch (NativeException)
        {
            return null;
        }
    }

    public static CCSPlayerController? FromSlot(int slot)
    {
        try
        {
            return Utilities.GetPlayerFromSlot(slot);
        }
        catch (NativeException)
        {
            return null;
        }
    }

    public static CCSPlayerController? FromUserid(int userid)
    {
        try
        {
            return Utilities.GetPlayerFromUserid(userid);
        }
        catch (NativeException)
        {
            return null;
        }
    }

    public static ulong SteamId64(CCSPlayerController player)
    {
        try
        {
            if (player.AuthorizedSteamID is { SteamId64: var auth } && auth != 0)
                return auth;
            return player.SteamID;
        }
        catch (NativeException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Pawn TeamNum when the pawn exists (C++ used GetPawn()->m_iTeamNum),
    /// otherwise 0.
    /// </summary>
    public static int PawnTeamNum(CCSPlayerController player)
    {
        try
        {
            var pawn = player.PlayerPawn?.Value;
            if (pawn is { IsValid: true })
                return pawn.TeamNum;
        }
        catch (NativeException)
        {
        }

        return 0;
    }

    public static CsTeam ControllerTeam(CCSPlayerController player)
    {
        try
        {
            return player.Team;
        }
        catch (NativeException)
        {
            return CsTeam.None;
        }
    }

    /// <summary>
    /// Freeze identity + scoreboard into <paramref name="stats"/> while the
    /// controller is still readable (connect, tick, disconnect).
    /// </summary>
    public static void Capture(CCSPlayerController player, PlayerStats stats)
    {
        try
        {
            stats.IsBot = player.IsBot || player.IsHLTV;
        }
        catch (NativeException)
        {
        }

        var steam = SteamId64(player);
        if (steam != 0)
            stats.SteamId = steam;

        var name = Name(player);
        if (name.Length > 0)
            stats.Name = name;

        var team = PawnTeamNum(player);
        if (team == 0)
            team = (int)ControllerTeam(player);
        if (team != 0)
            stats.Team = team;

        try
        {
            var money = player.InGameMoneyServices?.Account;
            if (money is int m)
                stats.Money = m;
        }
        catch (NativeException)
        {
        }

        try
        {
            var match = player.ActionTrackingServices?.MatchStats;
            if (match is null)
                return;
            ScoreboardMerge.Apply(
                stats,
                match.Kills,
                match.Deaths,
                match.Assists,
                match.Damage,
                match.HeadShotKills,
                match.UtilityDamage,
                match.EnemiesFlashed);
            if (match.ShotsFiredTotal > stats.ShotsFired)
                stats.ShotsFired = match.ShotsFiredTotal;
            if (match.ShotsOnTargetTotal > stats.ShotsOnTarget)
                stats.ShotsOnTarget = match.ShotsOnTargetTotal;
            if (match.EnemyKnifeKills > stats.KnifeKills)
                stats.KnifeKills = match.EnemyKnifeKills;
            if (match.EnemyTaserKills > stats.ZeusKills)
                stats.ZeusKills = match.EnemyTaserKills;
            if (match.Enemy2Ks > stats.DoubleKills)
                stats.DoubleKills = match.Enemy2Ks;
            if (match.Enemy3Ks > stats.TripleKills)
                stats.TripleKills = match.Enemy3Ks;
            if (match.Enemy4Ks > stats.QuadroKills)
                stats.QuadroKills = match.Enemy4Ks;
            if (match.Enemy5Ks > stats.PentaKills)
                stats.PentaKills = match.Enemy5Ks;
        }
        catch (NativeException)
        {
        }
    }

    public static CCSGameRules? GameRules()
    {
        try
        {
            return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .FirstOrDefault()?.GameRules;
        }
        catch (NativeException)
        {
            return null;
        }
    }
}

public static class EngineCvars
{
    public static int GetInt(string name, int fallback = 0)
    {
        var cv = ConVar.Find(name);
        if (cv is null)
            return fallback;
        try
        {
            return cv.Type switch
            {
                ConVarType.Int32 => cv.GetPrimitiveValue<int>(),
                ConVarType.Int16 => cv.GetPrimitiveValue<short>(),
                ConVarType.Float32 => (int)cv.GetPrimitiveValue<float>(),
                _ => int.TryParse(cv.StringValue, out var n) ? n : fallback
            };
        }
        catch
        {
            return fallback;
        }
    }
}
