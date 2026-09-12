using System.Globalization;
using System.Text.Json.Nodes;

namespace CS2SP.Logic;

public sealed class PlayerUpload
{
    public required string SteamId { get; init; }
    public string Name { get; init; } = "";
    public int Team { get; init; }
    public int Round { get; init; }
    public bool RoundWin { get; init; }
    public required PlayerStats Stats { get; init; }
    public int Assists { get; init; }
    public int TotalDamage { get; init; }
    public int Money { get; init; }
}

public static class StatsPayload
{
    public const string Game = "cs2";

    public static JsonObject? Build(IReadOnlyList<PlayerUpload> players, string mapName, int maxRounds)
    {
        if (players.Count == 0)
            return null;

        var statsObj = new JsonObject();
        foreach (var p in players)
            statsObj[p.SteamId] = ToPlayerObject(p);

        return new JsonObject
        {
            ["stats"] = statsObj,
            ["match"] = new JsonObject
            {
                ["map"] = mapName,
                ["max_rounds"] = maxRounds
            },
            ["game"] = Game
        };
    }

    public static JsonObject ToPlayerObject(PlayerUpload p)
    {
        var s = p.Stats;
        var round = p.Round;
        return new JsonObject
        {
            ["steam_id"] = p.SteamId,
            ["name"] = p.Name,
            ["round"] = round,
            ["round_win"] = p.RoundWin,
            ["kills"] = s.Kills,
            ["team_kills"] = s.TeamKills,
            ["assists"] = p.Assists,
            ["deaths"] = s.Deaths,
            ["kdr"] = s.Kdr,
            ["headshots"] = s.Headshots,
            ["headshot_percentage"] = s.HeadshotPercentage,
            ["mvps"] = s.Mvps,
            ["triple_kills"] = s.TripleKills,
            ["quadro_kills"] = s.QuadroKills,
            ["penta_kills"] = s.PentaKills,
            ["adr"] = round > 0 ? (float)p.TotalDamage / round : 0f,
            ["total_damage"] = p.TotalDamage,
            ["total_hits"] = s.Hits,
            ["bomb_plants"] = s.BombPlants,
            ["bomb_defuses"] = s.BombDefuses,
            ["bomb_explodes"] = s.BombExplodes,
            ["enemies_flashed"] = s.EnemiesFlashed,
            ["utility_damage"] = s.UtilityDamage,
            ["fire_damage"] = s.FireDamage,
            ["dinks"] = s.Dinks,
            ["first_kills"] = s.FirstKills,
            ["clutch_kills"] = s.ClutchKills,
            ["pistol_kills"] = s.PistolKills,
            ["sniper_kills"] = s.SniperKills,
            ["blind_kills"] = s.BlindKills,
            ["bomb_kills"] = s.BombKills,
            ["unique_kills"] = s.UniqueKills.Count,
            ["chicken_kills"] = s.ChickenKills,
            ["money"] = p.Money
        };
    }

    public static string SteamKey(ulong steamId) =>
        steamId.ToString(CultureInfo.InvariantCulture);
}

public static class RoundEndReasons
{
    /// <summary>CSRoundEndReason_GameStart — "Game Commencing!" from mp_restartgame.</summary>
    public const int GameStart = 16;
}
