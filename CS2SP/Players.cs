using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using CS2SP.Logic;

namespace CS2SP;

public static class Players
{
    /// <summary>
    /// CSS <c>GetPlayerFromSlot</c> wraps whatever lives at slot+1. World /
    /// gamerules / leftover ents occupy that index during levelload and round
    /// reset — schema reads on the wrong class AV the process. Same gate as
    /// C++ <c>CCSPlayerController::FromSlot</c>.
    /// </summary>
    public static bool IsController(CCSPlayerController? p)
    {
        if (p is null)
            return false;
        try
        {
            if (p.Handle == IntPtr.Zero || !p.IsValid)
                return false;
            var name = p.DesignerName;
            return name is not null
                && name.Equals("cs_player_controller", StringComparison.Ordinal);
        }
        catch (NativeException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValid(CCSPlayerController? p)
    {
        if (!IsController(p))
            return false;
        try
        {
            return !p!.IsHLTV;
        }
        catch (NativeException)
        {
            return false;
        }
        catch
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
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Still in the session. After <c>OnClientDisconnect</c> the controller can
    /// linger as Disconnecting/Disconnected with a zeroed scoreboard — do not
    /// walk those on periodic/round POSTs. Fail closed: a native error here
    /// used to walk leftovers whose service pointers abort srcds.
    /// </summary>
    public static bool IsLiveHuman(CCSPlayerController? p)
    {
        if (!IsHuman(p))
            return false;
        try
        {
            return p!.Connected == PlayerConnectedState.PlayerConnected
                && p.EverFullyConnected;
        }
        catch (NativeException)
        {
            return false;
        }
        catch
        {
            return false;
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
            var p = Utilities.GetPlayerFromSlot(slot);
            return IsController(p) ? p : null;
        }
        catch (NativeException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static CCSPlayerController? FromUserid(int userid)
    {
        try
        {
            var p = Utilities.GetPlayerFromUserid(userid);
            return IsController(p) ? p : null;
        }
        catch (NativeException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static ulong SteamId64(CCSPlayerController player)
    {
        try
        {
            if (player.IsBot || player.IsHLTV)
                return 0;
            if (player.AuthorizedSteamID is { SteamId64: var auth } && SteamIds.IsIndividual(auth))
                return auth;
            // Pre-auth humans already have m_steamID; bots' XUID is not an individual Steam64.
            if (SteamIds.IsIndividual(player.SteamID))
                return player.SteamID;
        }
        catch (NativeException)
        {
        }

        return 0;
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
    /// Controller is fully in-session. Fail closed on native errors so map-load
    /// leftovers are not walked (their service pointers AV process-wide).
    /// </summary>
    public static bool IsConnectedOccupant(CCSPlayerController? p)
    {
        if (!IsValid(p))
            return false;
        try
        {
            return p!.Handle != IntPtr.Zero
                && p.Connected == PlayerConnectedState.PlayerConnected
                && p.EverFullyConnected;
        }
        catch (NativeException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Freeze identity into <paramref name="stats"/>. Do not follow
    /// <c>m_pActionTrackingServices</c> / <c>m_pInGameMoneyServices</c> — those
    /// are raw heap pointers, not CHandles. A non-null leftover during sign-on,
    /// kick, or <c>OnPreResetRound</c> makes <c>Schema.GetRef</c>
    /// <c>AccessViolationException</c>, which .NET 8 cannot catch and which
    /// aborts srcds. Event totals already cover K/D/A/damage; skip engine
    /// money/scoreboard. <paramref name="scoreboard"/> is kept for call-site
    /// compatibility and is ignored.
    /// </summary>
    public static void Capture(CCSPlayerController player, PlayerStats stats, bool scoreboard = true)
    {
        _ = scoreboard;
        try
        {
            if (player.Handle == IntPtr.Zero || !IsController(player))
                return;
        }
        catch
        {
            return;
        }

        try
        {
            stats.IsBot = player.IsBot || player.IsHLTV;
        }
        catch (NativeException)
        {
        }
        catch
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
