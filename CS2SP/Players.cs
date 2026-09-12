using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2SP;

public static class Players
{
    public static bool IsValid(CCSPlayerController? p) =>
        p is { IsValid: true } && !p.IsHLTV;

    public static bool IsHuman(CCSPlayerController? p) =>
        IsValid(p) && !p!.IsBot;

    public static ulong SteamId64(CCSPlayerController player)
    {
        if (player.AuthorizedSteamID is { SteamId64: var auth } && auth != 0)
            return auth;
        return player.SteamID;
    }

    /// <summary>
    /// Pawn TeamNum when the pawn exists (C++ used GetPawn()->m_iTeamNum),
    /// otherwise 0.
    /// </summary>
    public static int PawnTeamNum(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn is { IsValid: true })
            return pawn.TeamNum;
        return 0;
    }

    public static CsTeam ControllerTeam(CCSPlayerController player) => player.Team;

    public static CCSGameRules? GameRules() =>
        Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
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
