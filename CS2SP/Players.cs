using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

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
