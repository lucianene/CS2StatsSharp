using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;

namespace CS2SP;

public sealed partial class CS2SPPlugin : BasePlugin
{
    public override string ModuleName => "CS2StatsPlugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "playcup";
    public override string ModuleDescription => "Collects and uploads per-round player stats for any CS2 game mode";

    public SpConVars Cvars { get; } = new();
    public PlaycupApiClient Api { get; private set; } = null!;
    public StatsCollector Stats { get; private set; } = null!;

    public override void Load(bool hotReload)
    {
        Cvars.Register(this);
        Api = new PlaycupApiClient(Logger);
        Stats = new StatsCollector(this, Cvars, Api, Logger);

        RegisterListener<Listeners.OnMapStart>(_ => SafeRun("OnMapStart", Stats.OnMapStart));
        RegisterListener<Listeners.OnMapEnd>(() => SafeRun("OnMapEnd", Stats.OnMapEnd));
        RegisterListener<Listeners.OnClientConnected>(slot => SafeRun("OnClientConnected", () => Stats.OnClientConnected(slot)));
        RegisterListener<Listeners.OnClientPutInServer>(slot => SafeRun("OnClientPutInServer", () => Stats.OnClientPutInServer(slot)));
        RegisterListener<Listeners.OnClientDisconnect>(slot => SafeRun("OnClientDisconnect", () => Stats.OnClientDisconnect(slot)));
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => SafeRun("OnClientDisconnectPost", () => Stats.OnClientDisconnectPost(slot)));
        RegisterListener<Listeners.OnClientAuthorized>((slot, steamId) =>
            SafeRun("OnClientAuthorized", () => Stats.OnClientAuthorized(slot, steamId.SteamId64)));

        Stats.Start();
        // Do not walk slots here: Utilities.GetPlayerFromSlot throws
        // NativeException ("Entity system yet is not initialized") and CSS unloads us.
        // Seed on the next world tick (late load / hot reload) and again on OnMapStart.
        Server.NextWorldUpdate(Stats.TrySeedWorld);

        Logger.LogInformation("[CS2SP] CS2StatsPlugin loaded (v{Version}).", ModuleVersion);
    }

    public override void Unload(bool hotReload)
    {
        Stats.OnUnload();
        Api.Dispose();
    }

    private void SafeRun(string name, Action body)
    {
        try
        {
            body();
        }
        catch (NativeException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[CS2SP] {Event} failed", name);
        }
    }

    private HookResult Safe(string name, Action body)
    {
        SafeRun(name, body);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect ev, GameEventInfo _info) =>
        Safe("player_disconnect", () => Stats.OnPlayerDisconnect(ev));

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart _, GameEventInfo _info) =>
        Safe("round_start", Stats.OnRoundStart);

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo _info) =>
        Safe("round_end", () => Stats.OnRoundEnd(ev));

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn ev, GameEventInfo _info) =>
        Safe("player_spawn", () => Stats.OnPlayerSpawn(ev.Userid));

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath ev, GameEventInfo _info) =>
        Safe("player_death", () => Stats.OnPlayerDeath(ev));

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt ev, GameEventInfo _info) =>
        Safe("player_hurt", () => Stats.OnPlayerHurt(ev));

    [GameEventHandler]
    public HookResult OnRoundMvp(EventRoundMvp ev, GameEventInfo _info) =>
        Safe("round_mvp", () => Stats.OnRoundMvp(ev.Userid));

    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted ev, GameEventInfo _info) =>
        Safe("bomb_planted", () => Stats.OnBomb("bomb_planted", ev.Userid));

    [GameEventHandler]
    public HookResult OnBombDefused(EventBombDefused ev, GameEventInfo _info) =>
        Safe("bomb_defused", () => Stats.OnBomb("bomb_defused", ev.Userid));

    [GameEventHandler]
    public HookResult OnBombExploded(EventBombExploded ev, GameEventInfo _info) =>
        Safe("bomb_exploded", () => Stats.OnBomb("bomb_exploded", ev.Userid));

    [GameEventHandler]
    public HookResult OnPlayerBlind(EventPlayerBlind ev, GameEventInfo _info) =>
        Safe("player_blind", () => Stats.OnPlayerBlind(ev));

    [GameEventHandler]
    public HookResult OnOtherDeath(EventOtherDeath ev, GameEventInfo _info) =>
        Safe("other_death", () => Stats.OnChickenDeath(ev));

    [GameEventHandler]
    public HookResult OnGrenadeThrown(EventGrenadeThrown ev, GameEventInfo _info) =>
        Safe("grenade_thrown", () => Stats.OnGrenadeThrown(ev));

    [GameEventHandler]
    public HookResult OnHostageRescued(EventHostageRescued ev, GameEventInfo _info) =>
        Safe("hostage_rescued", () => Stats.OnHostageRescued(ev.Userid));
}
