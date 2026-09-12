using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CS2SP.Logic;
using Microsoft.Extensions.Logging;

namespace CS2SP;

public sealed class StatsCollector
{
    private readonly BasePlugin _plugin;
    private readonly SpConVars _cvars;
    private readonly PlaycupApiClient _api;
    private readonly ILogger _log;
    private readonly Dictionary<int, int> _healthMap = [];
    private bool _firstKillThisRound;
    private int _uploadBusy;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _periodicTimer;

    public PlayerStatsStore Store { get; } = new();
    public PeriodicClock Clock { get; } = new();

    public StatsCollector(BasePlugin plugin, SpConVars cvars, PlaycupApiClient api, ILogger log)
    {
        _plugin = plugin;
        _cvars = cvars;
        _api = api;
        _log = log;
    }

    public ModProfile Mod => ModProfiles.Resolve(_cvars.Mod.Value);

    public void Start()
    {
        _periodicTimer ??= _plugin.AddTimer(1.0f, OnPeriodicTick, TimerFlags.REPEAT);
    }

    public void OnMapStart()
    {
        _firstKillThisRound = false;
        _healthMap.Clear();
        Clock.Reset();
        Store.Clear();
        Server.NextWorldUpdate(SeedConnectedPlayers);
    }

    public void SeedConnectedPlayers()
    {
        for (var i = 0; i < PlayerStatsStore.MaxPlayers; i++)
        {
            var p = Players.FromSlot(i);
            if (!Players.IsValid(p))
                continue;
            var stats = Store.Replace(i, p!.IsBot);
            stats.SteamId = Players.SteamId64(p);
        }
    }

    public void OnClientConnected(int slot)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        var p = Players.FromSlot(slot);
        var isBot = p is { IsValid: true } && (p.IsBot || p.IsHLTV);
        var stats = Store.Replace(slot, isBot);
        if (p is { IsValid: true })
            stats.SteamId = Players.SteamId64(p);
    }

    public void OnClientPutInServer(int slot)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        var p = Players.FromSlot(slot);
        if (!Players.IsValid(p))
            return;
        var stats = Store.Get(slot) ?? Store.Replace(slot, p!.IsBot);
        stats.IsBot = p!.IsBot;
        var steam = Players.SteamId64(p);
        if (steam != 0)
            stats.SteamId = steam;
    }

    public void OnClientDisconnect(int slot)
    {
        Store.Remove(slot);
        _healthMap.Remove(slot);
    }

    public void OnRoundStart()
    {
        _firstKillThisRound = false;
        _healthMap.Clear();
        Store.ResetAllRounds();
    }

    public void OnPlayerSpawn(CCSPlayerController? player)
    {
        if (!_cvars.Enabled.Value)
            return;
        if (!Mod.ResetStateOnSpawn)
            return;
        if (!Players.IsValid(player))
            return;

        var stats = Store.GetOrCreate(player!.Slot, player.IsBot);
        stats.ResetRound();
        _healthMap.Remove(player.Slot);
    }

    public void OnPlayerDeath(EventPlayerDeath ev)
    {
        if (!_cvars.Enabled.Value)
            return;

        var victim = ev.Userid;
        if (!Players.IsValid(victim))
            return;

        var victimStats = Store.GetOrCreate(victim!.Slot, victim.IsBot);
        TouchSteam(victim, victimStats);

        var attacker = ev.Attacker;
        if (!Players.IsValid(attacker))
        {
            // World / environment: C++ required an attacker controller so fall
            // deaths never set DeadThisRound and clutch/KDR drifted. Count them.
            victimStats.Deaths++;
            victimStats.DeadThisRound = true;
            return;
        }

        var killerStats = Store.GetOrCreate(attacker!.Slot, attacker.IsBot);
        TouchSteam(attacker, killerStats);

        var sameTeam = Players.ControllerTeam(attacker) == Players.ControllerTeam(victim);
        switch (KillCredit.ClassifyDeath(killerStats.DeadThisRound, sameTeam, Mod.FreeForAll))
        {
            case KillOutcome.Skip:
                return;
            case KillOutcome.TeamKill:
                killerStats.TeamKills++;
                victimStats.Deaths++;
                victimStats.DeadThisRound = true;
                return;
        }

        victimStats.Deaths++;
        victimStats.DeadThisRound = true;
        killerStats.Kills++;
        killerStats.RoundKills++;
        if (ev.Headshot)
            killerStats.Headshots++;

        KillCredit.ApplyMultiKills(killerStats, killerStats.RoundKills);

        if (!_firstKillThisRound)
        {
            _firstKillThisRound = true;
            killerStats.FirstKills++;
        }

        var killerTeam = Players.ControllerTeam(attacker);
        var aliveTeammates = 0;
        for (var j = 0; j < PlayerStatsStore.MaxPlayers; j++)
        {
            var other = Players.FromSlot(j);
            if (!Players.IsValid(other) || other!.Slot == attacker.Slot)
                continue;
            if (Players.ControllerTeam(other) != killerTeam)
                continue;
            if (Store.Get(other.Slot) is { } otherStats)
            {
                if (!otherStats.DeadThisRound)
                    aliveTeammates++;
            }
            else if (other.PawnIsAlive)
            {
                aliveTeammates++;
            }
        }
        if (KillCredit.IsClutch(aliveTeammates))
            killerStats.ClutchKills++;

        var weapon = ev.Weapon ?? "";
        if (WeaponKinds.IsPistol(weapon)) killerStats.PistolKills++;
        if (WeaponKinds.IsSniper(weapon)) killerStats.SniperKills++;
        if (WeaponKinds.IsBombKill(weapon)) killerStats.BombKills++;

        if (Server.CurrentTime < killerStats.FlashExpiry)
            killerStats.BlindKills++;

        if (Players.IsHuman(victim))
        {
            var steam = Players.SteamId64(victim);
            if (steam != 0)
                killerStats.UniqueKills.Add(steam);
        }

        if (ev.Assister is { IsValid: true } assister && !assister.IsHLTV)
            Store.GetOrCreate(assister.Slot, assister.IsBot).Assists++;
    }

    public void OnPlayerHurt(EventPlayerHurt ev)
    {
        if (!_cvars.Enabled.Value)
            return;

        var victim = ev.Userid;
        if (!Players.IsValid(victim))
            return;

        var victimId = victim!.Slot;
        var victimHealthAfter = ResolveHealthAfter(victim, ev.Health);
        var attacker = ev.Attacker;

        void CommitHealth() => _healthMap[victimId] = victimHealthAfter;

        if (!Players.IsValid(attacker))
        {
            CommitHealth();
            return;
        }

        var sameTeam = Players.ControllerTeam(attacker!) == Players.ControllerTeam(victim);
        var weapon = ev.Weapon ?? "";
        var attackerStats = Store.GetOrCreate(attacker!.Slot, attacker.IsBot);
        var victimStats = Store.GetOrCreate(victim.Slot, victim.IsBot);
        TouchSteam(attacker, attackerStats);
        TouchSteam(victim, victimStats);

        if (!KillCredit.ShouldCreditDamage(attackerStats.DeadThisRound, sameTeam, Mod.FreeForAll, WeaponKinds.IsDelayedUtility(weapon)))
        {
            CommitHealth();
            return;
        }

        var dmgHealth = ev.DmgHealth;
        var victimHealthBefore = DamageClamp.HealthBefore(_healthMap, victimId);
        var clamped = DamageClamp.ToCredit(dmgHealth, victimHealthBefore, victimHealthAfter);
        CommitHealth();

        if (clamped <= 0)
            return;

        attackerStats.Damage += clamped;
        attackerStats.Hits++;
        attackerStats.AddGiven(victimId, clamped);
        victimStats.AddTaken(attacker.Slot, clamped);

        if (KillCredit.IsDink(ev.Hitgroup, victimHealthAfter))
            attackerStats.Dinks++;

        if (WeaponKinds.IsUtility(weapon))
            attackerStats.UtilityDamage += clamped;
        if (WeaponKinds.IsFire(weapon))
            attackerStats.FireDamage += clamped;

        if (_cvars.Developer.Value)
        {
            _log.LogInformation(
                "[CS2SP] dmg event: victim={Victim} healthBefore={Before} healthAfter={After} dmg={Dmg} clampedDmg={Clamped}",
                victim.PlayerName, victimHealthBefore, victimHealthAfter, dmgHealth, clamped);
        }
    }

    public void OnPlayerBlind(EventPlayerBlind ev)
    {
        if (!_cvars.Enabled.Value)
            return;

        var attacker = ev.Attacker;
        var victim = ev.Userid;
        if (!Players.IsValid(attacker) || !Players.IsValid(victim))
            return;

        var sameTeam = Players.ControllerTeam(attacker!) == Players.ControllerTeam(victim!);
        if (!KillCredit.ShouldCreditFlash(attacker!.Slot == victim!.Slot, sameTeam, Mod.FreeForAll, ev.BlindDuration))
            return;

        var attackerStats = Store.GetOrCreate(attacker.Slot, attacker.IsBot);
        var victimStats = Store.GetOrCreate(victim.Slot, victim.IsBot);
        TouchSteam(attacker, attackerStats);
        TouchSteam(victim, victimStats);
        attackerStats.EnemiesFlashed++;
        victimStats.FlashExpiry = Server.CurrentTime + ev.BlindDuration;
    }

    public void OnRoundMvp(CCSPlayerController? mvp)
    {
        if (!_cvars.Enabled.Value)
            return;
        if (!Players.IsValid(mvp))
            return;
        Store.GetOrCreate(mvp!.Slot, mvp.IsBot).Mvps++;
    }

    public void OnBomb(string eventName, CCSPlayerController? player)
    {
        // Metamod counted bomb events even when sp_enabled was 0.
        if (!Players.IsValid(player))
            return;
        var s = Store.GetOrCreate(player!.Slot, player.IsBot);
        switch (eventName)
        {
            case "bomb_planted": s.BombPlants++; break;
            case "bomb_defused": s.BombDefuses++; break;
            case "bomb_exploded": s.BombExplodes++; break;
        }
    }

    public void OnChickenDeath(EventOtherDeath ev)
    {
        if (!_cvars.Enabled.Value)
            return;
        if (!string.Equals(ev.Othertype, "CChicken", StringComparison.Ordinal))
            return;
        var attacker = Players.FromUserid(ev.Attacker);
        if (!Players.IsHuman(attacker))
            return;
        Store.GetOrCreate(attacker!.Slot).ChickenKills++;
    }

    public void OnRoundEnd(EventRoundEnd ev)
    {
        if (!_cvars.Enabled.Value)
            return;
        if (ev.Reason == RoundEndReasons.GameStart)
            return;
        Upload(ev.Winner);
    }

    public void OnPeriodicTick()
    {
        if (!_cvars.Enabled.Value)
            return;
        if (!Clock.TryFire(Server.CurrentTime, _cvars.DmInterval.Value))
            return;
        Upload(winner: -1);
    }

    public void ForceUpload()
    {
        if (!_cvars.Enabled.Value)
        {
            _log.LogInformation("[CS2SP] sp_send_stats ignored: sp_enabled is 0.");
            return;
        }

        _log.LogInformation("[CS2SP] sp_send_stats: forcing stats upload...");
        Upload(winner: -1);
    }

    private void Upload(int winner)
    {
        if (Interlocked.CompareExchange(ref _uploadBusy, 1, 0) != 0)
        {
            _log.LogInformation("[CS2SP] Skipping stats upload: previous request still in flight.");
            return;
        }

        var sent = false;
        try
        {
            sent = TryPost(winner);
        }
        finally
        {
            if (!sent)
                Interlocked.Exchange(ref _uploadBusy, 0);
        }
    }

    private bool TryPost(int winner)
    {
        var rules = Players.GameRules();
        if (rules is null)
        {
            _log.LogInformation("[CS2SP] Skipping stats upload: game rules not ready.");
            return false;
        }

        var apiAddress = _cvars.ApiAddress.Value;
        if (!UploadUrl.IsConfigured(apiAddress))
        {
            _log.LogInformation("[CS2SP] Skipping stats upload: sp_api_round_address is empty (set it in server.cfg).");
            return false;
        }

        var roundNumber = rules.TotalRoundsPlayed;
        var maxRounds = EngineCvars.GetInt("mp_maxrounds", 0);
        var mapName = string.IsNullOrEmpty(Server.MapName) ? "unknown" : Server.MapName;
        var matchId = _cvars.MatchId.Value ?? "";
        var gameMode = _cvars.GameMode.Value ?? "";
        var mod = _cvars.Mod.Value ?? "";

        var uploads = new List<PlayerUpload>();
        for (var i = 0; i < PlayerStatsStore.MaxPlayers; i++)
        {
            var p = Players.FromSlot(i);
            if (!Players.IsHuman(p))
                continue;

            var stats = Store.Get(p!.Slot);
            if (stats is null)
                continue;

            var steam = Players.SteamId64(p);
            if (steam == 0)
                steam = stats.SteamId;
            if (steam == 0)
                continue;

            stats.SteamId = steam;
            var matchStats = p.ActionTrackingServices?.MatchStats;
            var playerTeam = Players.PawnTeamNum(p);
            uploads.Add(new PlayerUpload
            {
                SteamId = StatsPayload.SteamKey(steam),
                Name = p.PlayerName ?? "",
                Team = playerTeam,
                Round = roundNumber,
                RoundWin = winner == playerTeam,
                Stats = stats,
                Assists = matchStats is not null ? matchStats.Assists : stats.Assists,
                TotalDamage = matchStats is not null ? matchStats.Damage : stats.Damage,
                Money = p.InGameMoneyServices?.Account ?? 0
            });
        }

        var body = StatsPayload.Build(uploads, mapName, maxRounds);
        if (body is null)
            return false;

        var json = body.ToJsonString();
        if (_cvars.Developer.Value)
            _log.LogInformation("[CS2SP] [SP_UploadStats] Payload:\n{Payload}", json);

        var url = UploadUrl.Build(apiAddress, matchId, mod, gameMode);
        var serverId = _cvars.ServerId.Value;
        _log.LogInformation(
            "[CS2SP] Sending stats for {Count} player(s) (mod={Mod}, map={Map}, round={Round})",
            uploads.Count,
            string.IsNullOrEmpty(mod) ? "?" : mod,
            mapName,
            roundNumber);

        _api.Post(url, json,
            serverId: string.IsNullOrEmpty(serverId) ? null : serverId,
            onSuccess: node =>
            {
                Interlocked.Exchange(ref _uploadBusy, 0);
                _log.LogInformation("[CS2SP] Stats upload succeeded: {Body}", node?.ToJsonString() ?? "{}");
            },
            onError: (_, _) => Interlocked.Exchange(ref _uploadBusy, 0));
        return true;
    }

    private static int ResolveHealthAfter(CCSPlayerController victim, int eventHealth)
    {
        var pawn = victim.PlayerPawn?.Value;
        if (pawn is null || !pawn.IsValid)
            return Math.Max(0, eventHealth);
        if (pawn.LifeState != 0)
            return 0;
        return Math.Max(0, pawn.Health);
    }

    private static void TouchSteam(CCSPlayerController player, PlayerStats stats)
    {
        var steam = Players.SteamId64(player);
        if (steam != 0)
            stats.SteamId = steam;
        stats.IsBot = player.IsBot;
    }
}
