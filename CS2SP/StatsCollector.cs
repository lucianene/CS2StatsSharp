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
    private volatile bool _stopped;
    private volatile bool _worldReady;
    private bool _warnedMissingServerId;
    private string _lastMapName = "unknown";
    private int _lastRoundNumber;
    private int _lastMaxRounds;
    private int _storeEpoch;
    private int _queuedFrozenEpoch = -1;
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
        Clock.Reset();
        _queuedFrozenEpoch = -1;
        // If OnMapEnd was skipped, POST last map's parked rows with the cached
        // map name before wiping. Duplicate of OnMapEnd is idempotent.
        Upload(winner: -1, requireWorld: false, captureLive: false, refreshMeta: false);
        _storeEpoch++;
        Store.Clear();
        // One extra tick: the first world update still runs during
        // "Host activate: Loading" when leftover controllers have bad service pointers.
        Server.NextWorldUpdate(() => Server.NextWorldUpdate(TrySeedWorld));
    }

    public void OnMapEnd()
    {
        _worldReady = false;
        Clock.Reset();
        _queuedFrozenEpoch = -1;
        // Entity list is dying. POST parked steam snapshots with the *cached*
        // map/round so a leaver is not labeled as the next map, then OnMapStart
        // can Clear.
        Upload(winner: -1, requireWorld: false, captureLive: false, refreshMeta: false);
    }

    public void OnUnload()
    {
        _queuedFrozenEpoch = -1;
        try
        {
            Upload(winner: -1, requireWorld: false, captureLive: false, refreshMeta: false);
        }
        catch
        {
        }
        Stop();
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
            try
            {
                var p = Players.FromSlot(i);
                if (!Players.IsHuman(p) || !Players.IsConnectedOccupant(p))
                    continue;
                BindController(p!, occupySlot: true, scoreboard: false);
            }
            catch (NativeException)
            {
            }
            catch
            {
            }
        }
    }

    public void OnClientConnected(int slot)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        var p = Players.FromSlot(slot);
        if (Players.IsHuman(p))
        {
            BindController(p!, occupySlot: true, scoreboard: false);
            return;
        }

        Store.Bind(slot, isBot: true, steam: 0);
    }

    public void OnClientPutInServer(int slot)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        var p = Players.FromSlot(slot);
        if (!Players.IsHuman(p))
            return;
        BindController(p!, occupySlot: true, scoreboard: false);
    }

    public void OnClientDisconnect(int slot) => FreezeAndPark(slot, upload: true);

    public void OnClientDisconnectPost(int slot)
    {
        _healthMap.Remove(slot);
        var p = Players.FromSlot(slot);
        if (Players.IsValid(p))
        {
            try
            {
                if (p!.Connected == PlayerConnectedState.PlayerConnected)
                    return;
            }
            catch (NativeException)
            {
            }
        }
        Store.Disconnect(slot);
    }

    public void OnPlayerDisconnect(EventPlayerDisconnect ev)
    {
        var p = ev.Userid;
        var steam = ev.Xuid;
        if (!SteamIds.IsIndividual(steam))
            steam = 0;
        if (Players.IsHuman(p))
        {
            FreezeController(p!, steam);
            return;
        }

        if (steam != 0 && Store.GetBySteam(steam) is { } parked)
            parked.Disconnected = true;
    }

    public void OnClientAuthorized(int slot, ulong steamId64)
    {
        if ((uint)slot >= PlayerStatsStore.MaxPlayers)
            return;
        if (!SteamIds.IsIndividual(steamId64))
            return;
        var p = Players.FromSlot(slot);
        if (p is not null && !Players.IsHuman(p))
            return;
        var stats = Store.AttachSteam(slot, steamId64);
        if (Players.IsValid(p))
        {
            Players.Capture(p!, stats, scoreboard: false);
            Store.NoteSteam(stats);
        }
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
        if (stats is null || stats.Disconnected)
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
        KillFlavor.Apply(
            killerStats,
            weapon,
            noscope: ev.Noscope,
            thruSmoke: ev.Thrusmoke,
            inAir: ev.Attackerinair,
            attackerBlind: ev.Attackerblind || Server.CurrentTime < killerStats.FlashExpiry,
            penetrated: ev.Penetrated,
            distance: ev.Distance);
        KillFlavor.ApplyNotices(killerStats, ev.Dominated, ev.Revenge, ev.Wipe);

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
            {
                assisterStats.Assists++;
                if (ev.Assistedflash)
                    assisterStats.FlashAssists++;
            }
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

    public void OnGrenadeThrown(EventGrenadeThrown ev)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;
        var player = ev.Userid;
        if (!Players.IsValid(player))
            return;
        var stats = StatsOf(player!);
        if (stats is not null)
            WeaponKinds.CountThrow(stats, ev.Weapon);
    }

    public void OnHostageRescued(CCSPlayerController? player)
    {
        if (!_cvars.Enabled.Value || !_worldReady)
            return;
        if (!Players.IsValid(player))
            return;
        var stats = StatsOf(player!);
        if (stats is not null)
            stats.HostageRescues++;
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

    private void Upload(int winner, bool requireWorld = true, bool captureLive = true, bool refreshMeta = true)
    {
        if (_stopped)
            return;
        if (!_cvars.Enabled.Value)
            return;
        if (requireWorld && !_worldReady)
            return;

        var useBusy = captureLive;
        var tookBusy = false;
        if (useBusy)
        {
            if (Interlocked.CompareExchange(ref _uploadBusy, 1, 0) != 0)
            {
                _log.LogInformation("[CS2SP] Skipping stats upload: previous request still in flight.");
                return;
            }

            tookBusy = true;
        }

        var sent = false;
        try
        {
            sent = TryPost(winner, captureLive, refreshMeta, releaseBusy: tookBusy);
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
            if (tookBusy && !sent)
                Interlocked.Exchange(ref _uploadBusy, 0);
        }
    }

    private bool TryPost(int winner, bool captureLive, bool refreshMeta, bool releaseBusy)
    {
        var apiAddress = _cvars.ApiAddressText;
        if (!UploadUrl.IsConfigured(apiAddress))
        {
            _log.LogInformation("[CS2SP] Skipping stats upload: sp_api_round_address is empty (set it in server.cfg).");
            return false;
        }

        var roundNumber = _lastRoundNumber;
        var maxRounds = _lastMaxRounds;
        var mapName = _lastMapName;
        if (refreshMeta)
            RefreshMatchMeta(ref roundNumber, ref maxRounds, ref mapName);

        if (string.IsNullOrEmpty(mapName))
            mapName = "unknown";

        if (captureLive)
            CaptureLiveHumans();

        var matchId = _cvars.MatchIdText;
        var gameMode = _cvars.GameModeText;
        var mod = _cvars.ModText;

        var uploads = new List<PlayerUpload>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stats in Store.HumansForUpload())
        {
            var key = StatsPayload.SteamKey(stats.SteamId);
            if (!seen.Add(key))
                continue;
            uploads.Add(new PlayerUpload
            {
                SteamId = key,
                Name = stats.Name,
                Team = stats.Team,
                Round = stats.RoundForUpload(roundNumber),
                RoundWin = stats.RoundWinForUpload(winner),
                Stats = stats,
                Assists = stats.Assists,
                TotalDamage = stats.Damage,
                Money = stats.Money
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

        _api.Post(uri,
            json,
            serverId: string.IsNullOrEmpty(serverId) ? null : serverId,
            onSuccess: node =>
            {
                if (releaseBusy)
                    Interlocked.Exchange(ref _uploadBusy, 0);
                _log.LogInformation("[CS2SP] Stats upload succeeded: {Body}", node?.ToJsonString() ?? "{}");
            },
            onError: (_, _) =>
            {
                if (releaseBusy)
                    Interlocked.Exchange(ref _uploadBusy, 0);
            });
        return true;
    }

    private void RefreshMatchMeta(ref int roundNumber, ref int maxRounds, ref string mapName)
    {
        try
        {
            var rules = Players.GameRules();
            if (rules is not null)
            {
                try { roundNumber = rules.TotalRoundsPlayed; }
                catch (NativeException) { }
            }

            maxRounds = EngineCvars.GetInt("mp_maxrounds", maxRounds);
            if (!string.IsNullOrEmpty(Server.MapName))
                mapName = Server.MapName;
        }
        catch (NativeException)
        {
        }

        _lastRoundNumber = roundNumber;
        _lastMaxRounds = maxRounds;
        _lastMapName = string.IsNullOrEmpty(mapName) ? "unknown" : mapName;
        mapName = _lastMapName;
    }

    private void CaptureLiveHumans()
    {
        for (var i = 0; i < PlayerStatsStore.MaxPlayers; i++)
        {
            try
            {
                var p = Players.FromSlot(i);
                if (!Players.IsLiveHuman(p))
                    continue;
                CaptureCanonical(p!, occupySlot: false, scoreboard: true);
            }
            catch (NativeException)
            {
            }
            catch
            {
            }
        }
    }

    private PlayerStats? StatsOf(CCSPlayerController player)
    {
        try
        {
            var slot = player.Slot;
            if ((uint)slot >= PlayerStatsStore.MaxPlayers)
                return null;
            var isBot = player.IsBot;
            var steam = Players.SteamId64(player);
            var stats = Store.Resolve(slot, isBot, steam);
            if (stats.Disconnected)
                return null;
            Players.Capture(player, stats, scoreboard: true);
            return Store.NoteSteam(stats);
        }
        catch (NativeException)
        {
            return null;
        }
    }

    private void BindController(CCSPlayerController player, bool occupySlot, bool scoreboard = false)
    {
        CaptureCanonical(player, occupySlot, scoreboard);
    }

    private PlayerStats CaptureCanonical(CCSPlayerController player, bool occupySlot, bool scoreboard)
    {
        var isBot = true;
        try { isBot = player.IsBot || player.IsHLTV; }
        catch (NativeException) { /* fail closed: do not index as a human */ }

        var steam = Players.SteamId64(player);
        if (isBot)
            steam = 0;
        var slot = player.Slot;
        var stats = occupySlot
            ? Store.Bind(slot, isBot, steam)
            : Store.Resolve(slot, isBot, steam);
        Players.Capture(player, stats, scoreboard);
        var canonical = Store.NoteSteam(stats);
        if (!isBot)
            canonical.Disconnected = false;
        return canonical;
    }

    private PlayerStats? FreezeController(CCSPlayerController player, ulong steamHint)
    {
        try
        {
            var isBot = true;
            try { isBot = player.IsBot || player.IsHLTV; }
            catch (NativeException) { }

            var steam = steamHint != 0 ? steamHint : Players.SteamId64(player);
            if (isBot || !SteamIds.IsIndividual(steam))
                steam = 0;
            var slot = player.Slot;
            var stats = Store.Resolve(slot, isBot, steam);
            Players.Capture(player, stats, scoreboard: true);
            if (steam != 0)
                stats.SteamId = steam;
            return Store.NoteSteam(stats);
        }
        catch (NativeException)
        {
            return null;
        }
    }

    private void FreezeAndPark(int slot, bool upload)
    {
        _healthMap.Remove(slot);
        var p = Players.FromSlot(slot);
        PlayerStats? stats = Store.Get(slot);
        if (Players.IsValid(p))
            stats = FreezeController(p!, steamHint: 0) ?? stats;
        else if (stats is not null)
            stats = Store.NoteSteam(stats);

        Store.Disconnect(slot);
        if (upload && stats is { IsBot: false } && SteamIds.IsIndividual(stats.SteamId))
            QueueFrozenUpload();
    }

    private void QueueFrozenUpload()
    {
        var epoch = _storeEpoch;
        if (_queuedFrozenEpoch == epoch)
            return;
        _queuedFrozenEpoch = epoch;
        try
        {
            Server.NextWorldUpdate(() =>
            {
                if (_storeEpoch != epoch)
                    return;
                _queuedFrozenEpoch = -1;
                Upload(winner: -1, requireWorld: false, captureLive: false, refreshMeta: true);
            });
        }
        catch
        {
            _queuedFrozenEpoch = -1;
            Upload(winner: -1, requireWorld: false, captureLive: false, refreshMeta: true);
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
}
