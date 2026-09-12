namespace CS2SP.Logic;

public sealed class PlayerStats
{
    public int Slot;
    public ulong SteamId;
    public bool IsBot;

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
    public int BlindKills;
    public int BombKills;
    public int ChickenKills;
    public int TripleKills;
    public int QuadroKills;
    public int PentaKills;
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
}

public sealed class PlayerStatsStore
{
    public const int MaxPlayers = 64;

    private readonly PlayerStats?[] _slots = new PlayerStats[MaxPlayers];

    public PlayerStats Replace(int slot, bool isBot)
    {
        if ((uint)slot >= MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var stats = new PlayerStats { Slot = slot, IsBot = isBot };
        _slots[slot] = stats;
        return stats;
    }

    public PlayerStats GetOrCreate(int slot, bool isBot = false)
    {
        if ((uint)slot >= MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return _slots[slot] ??= new PlayerStats { Slot = slot, IsBot = isBot };
    }

    public PlayerStats? Get(int slot) =>
        (uint)slot < MaxPlayers ? _slots[slot] : null;

    public void Remove(int slot)
    {
        if ((uint)slot < MaxPlayers)
            _slots[slot] = null;
    }

    public void Clear() => Array.Clear(_slots);

    public void ResetAllRounds()
    {
        foreach (var s in _slots)
            s?.ResetRound();
    }

    public IEnumerable<PlayerStats> All
    {
        get
        {
            foreach (var s in _slots)
            {
                if (s is not null)
                    yield return s;
            }
        }
    }
}
