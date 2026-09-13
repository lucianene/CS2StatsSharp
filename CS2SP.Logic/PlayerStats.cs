namespace CS2SP.Logic;

public sealed class PlayerStats
{
    public int Slot;
    public ulong SteamId;
    public string Name = "";
    public bool IsBot;
    public bool Disconnected;
    public int Team;
    public int Money;
    /// <summary>
    /// Round stamped on the last live POST / freeze. Leavers keep this so ADR
    /// and <c>round</c> do not drift as later rounds play out (aim / casual / zm).
    /// </summary>
    public int ReportedRound;

    public int Kills;
    public int Deaths;
    public int Assists;
    public int Damage;
    public int Hits;
    public int Headshots;
    public int TeamKills;
    public int BombPlants;
    public int BombDefuses;
    public int BombExplodes;
    public int EnemiesFlashed;
    public int UtilityDamage;
    public int FireDamage;
    public int Dinks;
    public int FirstKills;
    public int ClutchKills;
    public int PistolKills;
    public int SniperKills;
    public int RifleKills;
    public int SmgKills;
    public int ShotgunKills;
    public int KnifeKills;
    public int ZeusKills;
    public int HeKills;
    public int MolotovKills;
    public int BlindKills;
    public int BombKills;
    public int ChickenKills;
    public int NoScopeKills;
    public int WallbangKills;
    public int SmokeKills;
    public int AirKills;
    public int DoubleKills;
    public int TripleKills;
    public int QuadroKills;
    public int PentaKills;
    public int FlashAssists;
    public int ShotsFired;
    public int ShotsOnTarget;
    public int HostageRescues;
    public int FlashThrown;
    public int HeThrown;
    public int SmokeThrown;
    public int MolotovThrown;
    public int DecoyThrown;
    public int Dominations;
    public int Revenges;
    public int TeamWipes;
    public float LongestKillDistance;
    public int Mvps;
    public int RoundKills;
    public bool DeadThisRound;
    public float FlashExpiry;
    public HashSet<ulong> UniqueKills { get; } = [];
    public Dictionary<int, int> RoundGivenDamage { get; } = [];
    public Dictionary<int, int> RoundTakenDamage { get; } = [];
    public Dictionary<int, int> RoundGivenHits { get; } = [];
    public Dictionary<int, int> RoundTakenHits { get; } = [];

    public float Kdr => Deaths > 0 ? (float)Kills / Deaths : Kills;
    public float HeadshotPercentage => Kills > 0 ? (float)Headshots / Kills * 100f : 0f;
    public float Accuracy
    {
        get
        {
            if (ShotsFired <= 0)
                return 0f;
            var hits = ShotsOnTarget > 0 ? ShotsOnTarget : Hits;
            return (float)hits / ShotsFired * 100f;
        }
    }

    public void TakeMaxExtras(PlayerStats src)
    {
        RifleKills = Math.Max(RifleKills, src.RifleKills);
        SmgKills = Math.Max(SmgKills, src.SmgKills);
        ShotgunKills = Math.Max(ShotgunKills, src.ShotgunKills);
        KnifeKills = Math.Max(KnifeKills, src.KnifeKills);
        ZeusKills = Math.Max(ZeusKills, src.ZeusKills);
        HeKills = Math.Max(HeKills, src.HeKills);
        MolotovKills = Math.Max(MolotovKills, src.MolotovKills);
        NoScopeKills = Math.Max(NoScopeKills, src.NoScopeKills);
        WallbangKills = Math.Max(WallbangKills, src.WallbangKills);
        SmokeKills = Math.Max(SmokeKills, src.SmokeKills);
        AirKills = Math.Max(AirKills, src.AirKills);
        DoubleKills = Math.Max(DoubleKills, src.DoubleKills);
        FlashAssists = Math.Max(FlashAssists, src.FlashAssists);
        ShotsFired = Math.Max(ShotsFired, src.ShotsFired);
        ShotsOnTarget = Math.Max(ShotsOnTarget, src.ShotsOnTarget);
        HostageRescues = Math.Max(HostageRescues, src.HostageRescues);
        FlashThrown = Math.Max(FlashThrown, src.FlashThrown);
        HeThrown = Math.Max(HeThrown, src.HeThrown);
        SmokeThrown = Math.Max(SmokeThrown, src.SmokeThrown);
        MolotovThrown = Math.Max(MolotovThrown, src.MolotovThrown);
        DecoyThrown = Math.Max(DecoyThrown, src.DecoyThrown);
        Dominations = Math.Max(Dominations, src.Dominations);
        Revenges = Math.Max(Revenges, src.Revenges);
        TeamWipes = Math.Max(TeamWipes, src.TeamWipes);
        if (src.LongestKillDistance > LongestKillDistance)
            LongestKillDistance = src.LongestKillDistance;
    }

    public void AddExtras(PlayerStats src)
    {
        RifleKills += src.RifleKills;
        SmgKills += src.SmgKills;
        ShotgunKills += src.ShotgunKills;
        KnifeKills += src.KnifeKills;
        ZeusKills += src.ZeusKills;
        HeKills += src.HeKills;
        MolotovKills += src.MolotovKills;
        NoScopeKills += src.NoScopeKills;
        WallbangKills += src.WallbangKills;
        SmokeKills += src.SmokeKills;
        AirKills += src.AirKills;
        DoubleKills += src.DoubleKills;
        FlashAssists += src.FlashAssists;
        ShotsFired += src.ShotsFired;
        ShotsOnTarget += src.ShotsOnTarget;
        HostageRescues += src.HostageRescues;
        FlashThrown += src.FlashThrown;
        HeThrown += src.HeThrown;
        SmokeThrown += src.SmokeThrown;
        MolotovThrown += src.MolotovThrown;
        DecoyThrown += src.DecoyThrown;
        Dominations += src.Dominations;
        Revenges += src.Revenges;
        TeamWipes += src.TeamWipes;
        if (src.LongestKillDistance > LongestKillDistance)
            LongestKillDistance = src.LongestKillDistance;
    }

    public void ResetRound()
    {
        RoundKills = 0;
        DeadThisRound = false;
        FlashExpiry = 0;
        RoundGivenDamage.Clear();
        RoundTakenDamage.Clear();
        RoundGivenHits.Clear();
        RoundTakenHits.Clear();
    }

    public void AddGiven(int victimSlot, int dmg)
    {
        RoundGivenDamage[victimSlot] = RoundGivenDamage.GetValueOrDefault(victimSlot) + dmg;
        RoundGivenHits[victimSlot] = RoundGivenHits.GetValueOrDefault(victimSlot) + 1;
    }

    public void AddTaken(int attackerSlot, int dmg)
    {
        RoundTakenDamage[attackerSlot] = RoundTakenDamage.GetValueOrDefault(attackerSlot) + dmg;
        RoundTakenHits[attackerSlot] = RoundTakenHits.GetValueOrDefault(attackerSlot) + 1;
    }

    /// <summary>
    /// Live players follow the current round. A parked leaver keeps the round
    /// they left on so later heartbeats / round_end do not shrink ADR.
    /// </summary>
    public int RoundForUpload(int currentRound)
    {
        if (!Disconnected)
        {
            ReportedRound = currentRound;
            return currentRound;
        }

        if (ReportedRound <= 0)
            ReportedRound = currentRound;
        return ReportedRound;
    }

    public bool RoundWinForUpload(int winner) =>
        !Disconnected && winner > 0 && winner == Team;
}

/// <summary>
/// Slot table for live events, steam index for uploads. A disconnect parks
/// the same object under steam so the next POST (and a reconnect) keep the
/// scoreboard snapshot. Map change is the only full wipe.
/// </summary>
public sealed class PlayerStatsStore
{
    public const int MaxPlayers = 64;

    private readonly PlayerStats?[] _slots = new PlayerStats[MaxPlayers];
    private readonly Dictionary<ulong, PlayerStats> _bySteam = [];
    private readonly ulong[] _slotSteam = new ulong[MaxPlayers];

    public PlayerStats Replace(int slot, bool isBot) => Bind(slot, isBot, steam: 0);

    /// <summary>
    /// This slot now belongs to this client (connect / put-in-server).
    /// Parks whoever was here. Restores a parked steam record in place.
    /// </summary>
    public PlayerStats Bind(int slot, bool isBot, ulong steam = 0)
    {
        if ((uint)slot >= MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(slot));

        if (!isBot && steam != 0 && _bySteam.TryGetValue(steam, out var existing))
        {
            var occupant = _slots[slot];
            if (occupant is not null && !ReferenceEquals(occupant, existing))
                ScoreboardMerge.Combine(existing, occupant);
            DetachFromSlot(existing);
            ParkOccupant(_slots[slot], keepIf: existing);
            return Seat(existing, slot, steam, disconnected: false);
        }

        ParkOccupant(_slots[slot], keepIf: null);
        var created = new PlayerStats { Slot = slot, IsBot = isBot, SteamId = steam };
        _slots[slot] = created;
        _slotSteam[slot] = steam;
        if (!isBot && steam != 0)
            _bySteam[steam] = created;
        return created;
    }

    /// <summary>
    /// Steam became known (auth / controller xuid). Never creates a second
    /// scoreboard for that steam; reconnect stubs are folded into the parked row.
    /// </summary>
    public PlayerStats AttachSteam(int slot, ulong steam)
    {
        if ((uint)slot >= MaxPlayers)
            return new PlayerStats { Slot = slot, SteamId = steam };

        var occupant = _slots[slot] ?? Bind(slot, isBot: false, steam: 0);
        if (steam == 0)
            return occupant;

        DropStaleSteamKey(occupant, steam);

        if (_bySteam.TryGetValue(steam, out var existing) && !ReferenceEquals(existing, occupant))
        {
            ScoreboardMerge.Combine(existing, occupant);
            DetachFromSlot(occupant);
            return Seat(existing, slot, steam, disconnected: false);
        }

        occupant.SteamId = steam;
        occupant.Disconnected = false;
        occupant.IsBot = false;
        _bySteam[steam] = occupant;
        _slotSteam[slot] = steam;
        return occupant;
    }

    /// <summary>
    /// Event-time lookup. Keys humans by steam so a lingering controller after
    /// disconnect cannot mint a zeroed stub that overwrites the parked row.
    /// Will not steal a slot already occupied by a different steam.
    /// </summary>
    public PlayerStats Resolve(int slot, bool isBot, ulong steam)
    {
        if ((uint)slot >= MaxPlayers)
            return new PlayerStats { Slot = slot, IsBot = isBot, SteamId = steam };

        if (!isBot && steam != 0 && _bySteam.TryGetValue(steam, out var known))
        {
            var occupant = _slots[slot];
            if (occupant is null || ReferenceEquals(occupant, known))
                return Seat(known, slot, steam, disconnected: known.Disconnected);

            if (occupant.IsBot || occupant.SteamId == 0 || occupant.SteamId == steam)
            {
                ScoreboardMerge.Combine(known, occupant);
                DetachFromSlot(occupant);
                return Seat(known, slot, steam, disconnected: false);
            }

            // Slot reused by someone else. Keep this steam's row parked.
            return known;
        }

        return Get(slot) ?? Bind(slot, isBot, steam);
    }

    public PlayerStats GetOrCreate(int slot, bool isBot = false)
    {
        if ((uint)slot >= MaxPlayers)
            return new PlayerStats { Slot = slot, IsBot = isBot };
        if (_slots[slot] is { } live)
            return live;
        var steam = _slotSteam[slot];
        if (steam != 0 && _bySteam.TryGetValue(steam, out var parked))
            return Seat(parked, slot, steam, disconnected: parked.Disconnected);

        var created = new PlayerStats { Slot = slot, IsBot = isBot, SteamId = steam };
        _slots[slot] = created;
        if (!isBot && steam != 0)
            _bySteam[steam] = created;
        return created;
    }

    public PlayerStats? Get(int slot) =>
        (uint)slot < MaxPlayers ? _slots[slot] : null;

    public PlayerStats? GetBySteam(ulong steam) =>
        steam != 0 && _bySteam.TryGetValue(steam, out var s) ? s : null;

    /// <summary>
    /// Index this object by steam. If that steam already has a row, fold this
    /// object into it and return the canonical row — never replace a parked
    /// scoreboard with a zeroed stub.
    /// </summary>
    public PlayerStats NoteSteam(PlayerStats stats)
    {
        if (stats.IsBot || stats.SteamId == 0)
            return stats;

        DropStaleSteamKey(stats, stats.SteamId);

        if (_bySteam.TryGetValue(stats.SteamId, out var existing) && !ReferenceEquals(existing, stats))
        {
            ScoreboardMerge.Combine(existing, stats);
            var slot = stats.Slot;
            DetachFromSlot(stats);
            if ((uint)slot < MaxPlayers)
            {
                var occupant = _slots[slot];
                if (occupant is null || ReferenceEquals(occupant, stats) || occupant.SteamId == 0 || occupant.SteamId == stats.SteamId)
                    return Seat(existing, slot, stats.SteamId, disconnected: existing.Disconnected);
            }

            _bySteam[stats.SteamId] = existing;
            return existing;
        }

        _bySteam[stats.SteamId] = stats;
        if ((uint)stats.Slot < MaxPlayers)
            _slotSteam[stats.Slot] = stats.SteamId;
        return stats;
    }

    public void Disconnect(int slot)
    {
        if ((uint)slot >= MaxPlayers)
            return;
        var stats = _slots[slot];
        if (stats is null)
            return;

        if (stats.SteamId == 0)
            stats.SteamId = _slotSteam[slot];
        ParkOccupant(stats, keepIf: null);
        // Keep _slotSteam so a lingering controller on this slot reuses the
        // parked row instead of creating a blank one. A later Bind overwrites it.
    }

    public void Remove(int slot) => Disconnect(slot);

    public void Clear()
    {
        Array.Clear(_slots);
        Array.Clear(_slotSteam);
        _bySteam.Clear();
    }

    public void ResetAllRounds()
    {
        foreach (var s in _slots)
        {
            if (s is { Disconnected: false })
                s.ResetRound();
        }

        foreach (var s in _bySteam.Values)
        {
            if (!s.Disconnected)
                s.ResetRound();
        }
    }

    /// <summary>
    /// Humans for a heartbeat. Parked leavers are omitted so later POSTs stop
    /// refreshing their live-list row. Pass <paramref name="includeDisconnected"/>
    /// for the map-end flush (the one-shot leave POST adds pending leavers
    /// separately so older parked rows are not re-sent).
    /// </summary>
    public IEnumerable<PlayerStats> HumansForUpload(bool includeDisconnected = false)
    {
        foreach (var s in _bySteam.Values)
        {
            if (s.IsBot || !SteamIds.IsIndividual(s.SteamId))
                continue;
            if (!includeDisconnected && s.Disconnected)
                continue;
            yield return s;
        }
    }

    public IEnumerable<PlayerStats> All
    {
        get
        {
            var seen = new HashSet<PlayerStats>();
            foreach (var s in _slots)
            {
                if (s is not null && seen.Add(s))
                    yield return s;
            }

            foreach (var s in _bySteam.Values)
            {
                if (seen.Add(s))
                    yield return s;
            }
        }
    }

    private PlayerStats Seat(PlayerStats stats, int slot, ulong steam, bool disconnected)
    {
        stats.Slot = slot;
        stats.IsBot = false;
        stats.SteamId = steam;
        stats.Disconnected = disconnected;
        _slots[slot] = stats;
        _slotSteam[slot] = steam;
        _bySteam[steam] = stats;
        return stats;
    }

    private void DetachFromSlot(PlayerStats stats)
    {
        if ((uint)stats.Slot < MaxPlayers && _slots[stats.Slot] == stats)
            _slots[stats.Slot] = null;
    }

    private void DropStaleSteamKey(PlayerStats stats, ulong newSteam)
    {
        if (stats.SteamId == 0 || stats.SteamId == newSteam)
            return;
        if (_bySteam.TryGetValue(stats.SteamId, out var mapped) && ReferenceEquals(mapped, stats))
            _bySteam.Remove(stats.SteamId);
    }

    private void ParkOccupant(PlayerStats? occupant, PlayerStats? keepIf)
    {
        if (occupant is null || ReferenceEquals(occupant, keepIf))
            return;

        DetachFromSlot(occupant);
        if (occupant.IsBot)
        {
            if (occupant.SteamId != 0)
                _bySteam.Remove(occupant.SteamId);
            return;
        }

        occupant.Disconnected = true;
        if (occupant.SteamId != 0)
            _bySteam[occupant.SteamId] = occupant;
    }
}
