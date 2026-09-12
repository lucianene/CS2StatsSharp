namespace CS2SP.Logic;

/// <summary>
/// Caps overkill so credited HP cannot exceed remaining health.
/// Prefers the observed HP delta when it only disagrees with
/// <c>dmg_health</c> by 1 (truncated-float leftover), matching CS2MM.
/// Unseen victims start at 100; a victim already at 0 contributes 0.
/// </summary>
public static class DamageClamp
{
    public const int DefaultHealth = 100;

    public static int HealthBefore(IReadOnlyDictionary<int, int> healthMap, int victimSlot) =>
        healthMap.TryGetValue(victimSlot, out var hp) ? hp : DefaultHealth;

    public static int ToCredit(int dmgHealth, int healthBefore, int healthAfter)
    {
        var remaining = Math.Max(0, healthBefore);
        var cappedEvent = Math.Min(Math.Max(0, dmgHealth), remaining);
        var healthRemoved = Math.Max(0, healthBefore - healthAfter);

        if (healthRemoved > 0 && healthRemoved <= cappedEvent + 1)
            return healthRemoved;

        return cappedEvent;
    }
}
