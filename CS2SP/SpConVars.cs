using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using CS2SP.Logic;

namespace CS2SP;

public sealed class SpConVars
{
    // Public fields: CSS RegisterFakeConVars only reflects fields, not properties.
    public readonly FakeConVar<bool> Enabled = new("sp_enabled", "Enable CS2StatsPlugin stats collection", true);
    public readonly FakeConVar<bool> Developer = new("sp_developer", "Enable verbose debug logging", false);
    public readonly FakeConVar<string> MatchId = new("sp_match_id", "Match / session ID sent with each stats payload", "");
    public readonly FakeConVar<string> ApiAddress = new("sp_api_round_address", "Full URL for the mod stats endpoint", "https://api-test.playcup.ro/v1/mod-stats/");
    public readonly FakeConVar<string> GameMode = new("sp_game_mode", "Game mode label (deathmatch, zombie, retake, …)", "deathmatch");
    public readonly FakeConVar<string> Mod = new("sp_mod", "Mod type driving stat behavior: matchmaking, deathmatch, retake, aim, zombie, …", "matchmaking");
    public readonly FakeConVar<string> ServerId = new("sp_server_id", "This server's identifier (game_servers.container_name) so the API buckets stats to the right server.", "");
    public readonly FakeConVar<float> DmInterval = new("sp_dm_upload_interval", "Seconds between periodic stats uploads", 30.0f, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(1f, 86400f));

    public string MatchIdText => CvarText.Clean(MatchId.Value);
    public string ApiAddressText => CvarText.Clean(ApiAddress.Value);
    public string GameModeText => CvarText.Clean(GameMode.Value);
    public string ModText => CvarText.Clean(Mod.Value);
    public string ServerIdText => CvarText.CleanHeader(ServerId.Value);

    public void Register(BasePlugin plugin) => plugin.RegisterFakeConVars(this);
}
