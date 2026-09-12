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
    private readonly List<PlayerStats> _disconnected = [];
    private bool _firstKillThisRound;
    private int _uploadBusy;
    private volatile bool _stopped;
    private volatile bool _worldReady;
    private bool _warnedMissingServerId;
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

    public ModProfile Mod => ModProfiles.Resolve(_cvars.ModText);

    public void Start()
    {
        _periodicTimer ??= _plugin.AddTimer(1.0f, OnPeriodicTick, TimerFlags.REPEAT);
    }

    public void Stop()
    {
        _stopped = true;
        _worldReady = false;
        if (_periodicTimer is { } timer)
        {
            try { timer.Kill(); }
            catch { /* STOP_ON_MAPCHANGE / already killed */ }
            _periodicTimer = null;
        }
    }

    public void OnMapStart()
    {
        _worldReady = false;
        _firstKillThisRound = false;
        _healthMap.Clear();
        _disconnected.Clear();
        Clock.Reset();
        Store.Clear();
        Server.NextWorldUpdate(TrySeedWorld);
    }

    public void OnMapEnd()
    {
        _worldReady = false;
        Clock.Reset();
    }

    /// <summary>
    /// Safe to call from Load (via NextWorldUpdate), OnMapStart, or the
    /// periodic tick. No-ops until game rules exist so we never walk slots
    /// before the entity list is up.
    /// </summary>
    public void TrySeedWorld()
    {
        if (_stopped)
            return;
        if (Players.GameRules() is null)
            return;
        _worldReady = true;
        SeedConnectedPlayers();
    }

    public void SeedConnectedPlayers()
    {
        for (var i = 0; i < PlayerStatsStore.MaxPlayers; i++)
        {
            var p = Players.FromSlot(i);
            if (!Players.IsValid(p))
                continue;
            var isBot = false;
            try { isBot = p!.IsBot; }
            catch (NativeException) { }
            var stats = Store.Replace(i, isBot);
            TouchSteam(p!, stats);
        }
    }

    public void OnClientConnected(int slot)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        var p = Players.FromSlot(slot);
        var isBot = false;
        if (Players.IsValid(p))
        {
            try { isBot = p!.IsBot || p.IsHLTV; }
            catch (NativeException) { }
        }

        var stats = Store.Replace(slot, isBot);
        if (Players.IsValid(p))
            TouchSteam(p!, stats);
    }

    public void OnClientPutInServer(int slot)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        var p = Players.FromSlot(slot);
        if (!Players.IsValid(p))
            return;
        var stats = Store.Get(slot) ?? Store.Replace(slot, false);
        stats.Disconnected = false;
        TouchSteam(p!, stats);
    }

    public void OnClientDisconnect(int slot)
    {
        _healthMap.Remove(slot);
        var stats = Store.Get(slot);
        Store.Remove(slot);
        if (stats is { IsBot: false, SteamId: not 0 })
        {
            stats.Disconnected = true;
            _disconnected.Add(stats);
        }
    }

    public void OnClientAuthorized(int slot, ulong steamId64)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        var p = Players.FromSlot(slot);
        var stats = Store.Get(slot) ?? Store.Replace(slot, false);
        if (steamId64 != 0)
            stats.SteamId = steamId64;
        if (Players.IsValid(p))
            TouchSteam(p!, stats);
    }

    public void OnRoundStart()
    {
        _firstKillThisRound = false;
        _healthMap.Clear();
        Store.ResetAllRounds();
    }

    public void OnPlayerSpawn(CCSPlayerController? player)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;
        if (!Mod.ResetStateOnSpawn)
            return;
        if (!Players.IsValid(player))
            return;

        var stats = StatsOf(player!);
        if (stats is null)
            return;
        stats.ResetRound();
        _healthMap.Remove(stats.Slot);
    }

    public void OnPlayerDeath(EventPlayerDeath ev)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;

        var victim = ev.Userid;
        if (!Players.IsValid(victim))
            return;

        var victimStats = StatsOf(victim!);
        if (victimStats is null)
            return;

        var attacker = ev.Attacker;
        if (!Players.IsValid(attacker))
        {
            // World / environment: C++ required an attacker controller so fall
            // deaths never set DeadThisRound and clutch/KDR drifted. Count them.
            victimStats.Deaths++;
            victimStats.DeadThisRound = true;
            return;
        }

        var killerStats = StatsOf(attacker!);
        if (killerStats is null)
            return;

        var suicide = attacker!.Slot == victim!.Slot;
        var sameTeam = Players.SameTeam(attacker, victim!);
        if (sameTeam is null)
            return;

        switch (KillCredit.ClassifyDeath(killerStats.DeadThisRound, sameTeam.Value, Mod.FreeForAll, suicide))
        {
            case KillOutcome.Skip:
                return;
            case KillOutcome.Suicide:
                victimStats.Deaths++;
                victimStats.DeadThisRound = true;
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
            else
            {
                try
                {
                    if (other.PawnIsAlive)
                        aliveTeammates++;
                }
                catch (NativeException)
                {
                }
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

        if (Players.IsValid(ev.Assister))
        {
            var assisterStats = StatsOf(ev.Assister!);
            if (assisterStats is not null)
                assisterStats.Assists++;
        }
    }

    public void OnPlayerHurt(EventPlayerHurt ev)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
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

        var sameTeam = Players.SameTeam(attacker!, victim);
        if (sameTeam is null)
        {
            CommitHealth();
            return;
        }

        var weapon = ev.Weapon ?? "";
        var attackerStats = StatsOf(attacker!);
        var victimStats = StatsOf(victim);
        if (attackerStats is null || victimStats is null)
        {
            CommitHealth();
            return;
        }

        if (!KillCredit.ShouldCreditDamage(attackerStats.DeadThisRound, sameTeam.Value, Mod.FreeForAll, WeaponKinds.IsDelayedUtility(weapon)))
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
        victimStats.AddTaken(attacker!.Slot, clamped);

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
                Players.Name(victim), victimHealthBefore, victimHealthAfter, dmgHealth, clamped);
        }
    }

    public void OnPlayerBlind(EventPlayerBlind ev)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;

        var attacker = ev.Attacker;
        var victim = ev.Userid;
        if (!Players.IsValid(attacker) || !Players.IsValid(victim))
            return;

        var sameTeam = Players.SameTeam(attacker!, victim!);
        if (sameTeam is null)
            return;
        if (!KillCredit.ShouldCreditFlash(attacker!.Slot == victim!.Slot, sameTeam.Value, Mod.FreeForAll, ev.BlindDuration))
            return;

        var attackerStats = StatsOf(attacker);
        var victimStats = StatsOf(victim);
        if (attackerStats is null || victimStats is null)
            return;
        attackerStats.EnemiesFlashed++;
        victimStats.FlashExpiry = Server.CurrentTime + ev.BlindDuration;
    }

    public void OnRoundMvp(CCSPlayerController? mvp)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;
        if (!Players.IsValid(mvp))
            return;
        var stats = StatsOf(mvp!);
        if (stats is not null)
            stats.Mvps++;
    }

    public void OnBomb(string eventName, CCSPlayerController? player)
    {
        // Metamod counted bomb events even when sp_enabled was 0.
        if (!_worldReady)
            return;
        if (!Players.IsValid(player))
            return;
        var s = StatsOf(player!);
        if (s is null)
            return;
        switch (eventName)
        {
            case "bomb_planted": s.BombPlants++; break;
            case "bomb_defused": s.BombDefuses++; break;
            case "bomb_exploded": s.BombExplodes++; break;
        }
    }

    public void OnChickenDeath(EventOtherDeath ev)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;
        var kind = ev.Othertype;
        if (string.IsNullOrEmpty(kind))
            return;
        if (!kind.Equals("CChicken", StringComparison.OrdinalIgnoreCase)
            && !kind.Equals("chicken", StringComparison.OrdinalIgnoreCase))
            return;
        var attacker = Players.FromUserid(ev.Attacker);
        if (!Players.IsHuman(attacker))
            return;
        var stats = StatsOf(attacker!);
        if (stats is not null)
            stats.ChickenKills++;
    }

    public void OnRoundEnd(EventRoundEnd ev)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;
        if (ev.Reason == RoundEndReasons.GameStart)
            return;
        Upload(ev.Winner);
    }

    public void OnPeriodicTick()
    {
        if (_stopped || !_cvars.Enabled.Value)
            return;
        if (!_worldReady)
        {
            TrySeedWorld();
            return;
        }
        if (!Clock.TryFire(Server.CurrentTime, _cvars.DmInterval.Value))
            return;
        Upload(winner: -1);
    }

    public void ForceUpload()
    {
        if (_stopped)
            return;
        if (!_cvars.Enabled.Value)
        {
            _log.LogInformation("[CS2SP] sp_send_stats ignored: sp_enabled is 0.");
            return;
        }

        if (!_worldReady)
            TrySeedWorld();
        if (!_worldReady)
        {
            _log.LogInformation("[CS2SP] sp_send_stats ignored: entity system not ready.");
            return;
        }

        _log.LogInformation("[CS2SP] sp_send_stats: forcing stats upload...");
        Upload(winner: -1);
    }

    private void Upload(int winner)
    {
        if (_stopped || !_worldReady)
            return;
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
        catch (NativeException)
        {
            sent = false;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[CS2SP] Stats upload failed to build.");
            sent = false;
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

        var apiAddress = _cvars.ApiAddressText;
        if (!UploadUrl.IsConfigured(apiAddress))
        {
            _log.LogInformation("[CS2SP] Skipping stats upload: sp_api_round_address is empty (set it in server.cfg).");
            return false;
        }

        int roundNumber;
        try { roundNumber = rules.TotalRoundsPlayed; }
        catch (NativeException)
        {
            _log.LogInformation("[CS2SP] Skipping stats upload: game rules not ready.");
            return false;
        }

        var maxRounds = EngineCvars.GetInt("mp_maxrounds", 0);
        var mapName = string.IsNullOrEmpty(Server.MapName) ? "unknown" : Server.MapName;
        var matchId = _cvars.MatchIdText;
        var gameMode = _cvars.GameModeText;
        var mod = _cvars.ModText;

        var uploads = new List<PlayerUpload>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < PlayerStatsStore.MaxPlayers; i++)
        {
            try
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
                TouchSteam(p, stats);
                var key = StatsPayload.SteamKey(steam);
                if (!seen.Add(key))
                    continue;

                int assists = stats.Assists;
                int totalDamage = stats.Damage;
                int money = 0;
                try
                {
                    var matchStats = p.ActionTrackingServices?.MatchStats;
                    if (matchStats is not null)
                    {
                        assists = matchStats.Assists;
                        totalDamage = matchStats.Damage;
                    }
                    money = p.InGameMoneyServices?.Account ?? 0;
                }
                catch (NativeException)
                {
                }

                uploads.Add(new PlayerUpload
                {
                    SteamId = key,
                    Name = string.IsNullOrEmpty(stats.Name) ? Players.Name(p) : stats.Name,
                    Team = Players.PawnTeamNum(p),
                    Round = roundNumber,
                    RoundWin = winner == Players.PawnTeamNum(p),
                    Stats = stats,
                    Assists = assists,
                    TotalDamage = totalDamage,
                    Money = money
                });
            }
            catch (NativeException)
            {
            }
        }

        foreach (var stats in _disconnected)
        {
            if (stats.IsBot || stats.SteamId == 0)
                continue;
            var key = StatsPayload.SteamKey(stats.SteamId);
            if (!seen.Add(key))
                continue;
            uploads.Add(new PlayerUpload
            {
                SteamId = key,
                Name = stats.Name,
                Team = 0,
                Round = roundNumber,
                RoundWin = false,
                Stats = stats,
                Assists = stats.Assists,
                TotalDamage = stats.Damage,
                Money = 0
            });
        }

        var body = StatsPayload.Build(uploads, mapName, maxRounds);
        if (body is null)
            return false;

        var json = body.ToJsonString();
        if (_cvars.Developer.Value)
            _log.LogInformation("[CS2SP] [SP_UploadStats] Payload:\n{Payload}", json);

        var url = UploadUrl.Build(apiAddress, matchId, mod, gameMode);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _log.LogWarning("[CS2SP] Skipping stats upload: invalid URL {Url}", url);
            return false;
        }

        var serverId = _cvars.ServerIdText;
        if (string.IsNullOrEmpty(serverId) && !_warnedMissingServerId)
        {
            _warnedMissingServerId = true;
            _log.LogWarning("[CS2SP] sp_server_id is empty; API may drop or mis-bucket this POST.");
        }

        _log.LogInformation(
            "[CS2SP] Sending stats for {Count} player(s) (mod={Mod}, map={Map}, round={Round})",
            uploads.Count,
            string.IsNullOrEmpty(mod) ? "?" : mod,
            mapName,
            roundNumber);

        var flushed = _disconnected.ToList();
        _api.Post(uri,
            json,
            serverId: string.IsNullOrEmpty(serverId) ? null : serverId,
            onSuccess: node =>
            {
                Interlocked.Exchange(ref _uploadBusy, 0);
                foreach (var s in flushed)
                    _disconnected.Remove(s);
                _log.LogInformation("[CS2SP] Stats upload succeeded: {Body}", node?.ToJsonString() ?? "{}");
            },
            onError: (_, _) => Interlocked.Exchange(ref _uploadBusy, 0));
        return true;
    }

    private PlayerStats? StatsOf(CCSPlayerController player)
    {
        try
        {
            var slot = player.Slot;
            if ((uint)slot >= PlayerStatsStore.MaxPlayers)
                return null;
            var stats = Store.GetOrCreate(slot, player.IsBot);
            TouchSteam(player, stats);
            return stats;
        }
        catch (NativeException)
        {
            return null;
        }
    }

    private static int ResolveHealthAfter(CCSPlayerController victim, int eventHealth)
    {
        try
        {
            var pawn = victim.PlayerPawn?.Value;
            if (pawn is null || !pawn.IsValid)
                return Math.Max(0, eventHealth);
            if (pawn.LifeState != 0)
                return 0;
            return Math.Max(0, pawn.Health);
        }
        catch (NativeException)
        {
            return Math.Max(0, eventHealth);
        }
    }

    private static void TouchSteam(CCSPlayerController player, PlayerStats stats)
    {
        var steam = Players.SteamId64(player);
        if (steam != 0)
            stats.SteamId = steam;
        try { stats.IsBot = player.IsBot; }
        catch (NativeException) { }
        var name = Players.Name(player);
        if (name.Length > 0)
            stats.Name = name;
    }
}
