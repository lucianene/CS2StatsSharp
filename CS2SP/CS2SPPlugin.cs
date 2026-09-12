using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
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

        RegisterListener<Listeners.OnMapStart>(_ => Stats.OnMapStart());
        RegisterListener<Listeners.OnClientConnected>(slot => Stats.OnClientConnected(slot));
        RegisterListener<Listeners.OnClientPutInServer>(slot => Stats.OnClientPutInServer(slot));
        RegisterListener<Listeners.OnClientDisconnect>(slot => Stats.OnClientDisconnect(slot));
        RegisterListener<Listeners.OnClientAuthorized>((slot, steamId) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (!Players.IsValid(player))
                return;
            var stats = Stats.Store.Get(slot) ?? Stats.Store.Replace(slot, player!.IsBot);
            if (steamId.SteamId64 != 0)
                stats.SteamId = steamId.SteamId64;
        });

        Stats.Start();
        Stats.SeedConnectedPlayers();

        Logger.LogInformation("[CS2SP] CS2StatsPlugin loaded (v{Version}).", ModuleVersion);
    }

    public override void Unload(bool hotReload) => Api.Dispose();

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart _, GameEventInfo _info)
    {
        Stats.OnRoundStart();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo _info)
    {
        Stats.OnRoundEnd(ev);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn ev, GameEventInfo _info)
    {
        Stats.OnPlayerSpawn(ev.Userid);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath ev, GameEventInfo _info)
    {
        Stats.OnPlayerDeath(ev);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt ev, GameEventInfo _info)
    {
        Stats.OnPlayerHurt(ev);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundMvp(EventRoundMvp ev, GameEventInfo _info)
    {
        Stats.OnRoundMvp(ev.Userid);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted ev, GameEventInfo _info)
    {
        Stats.OnBomb("bomb_planted", ev.Userid);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombDefused(EventBombDefused ev, GameEventInfo _info)
    {
        Stats.OnBomb("bomb_defused", ev.Userid);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombExploded(EventBombExploded ev, GameEventInfo _info)
    {
        Stats.OnBomb("bomb_exploded", ev.Userid);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerBlind(EventPlayerBlind ev, GameEventInfo _info)
    {
        Stats.OnPlayerBlind(ev);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnOtherDeath(EventOtherDeath ev, GameEventInfo _info)
    {
        Stats.OnChickenDeath(ev);
        return HookResult.Continue;
    }
}
