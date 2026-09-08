using System;
using System.Collections.Generic;

namespace AEBHardMode;

/// <summary>Difficulty tiers. Off disables the mod entirely.</summary>
public enum Tier
{
    Off = 0,
    Hard = 1,
    VeryHard = 2,
    Extreme = 3,
}

/// <summary>An inclusive multiplier range that a single enemy rolls within.</summary>
internal readonly struct StatRange
{
    public readonly float Min;
    public readonly float Max;

    public StatRange(float min, float max)
    {
        Min = min;
        Max = max;
    }
}

/// <summary>The five multipliers applied to one enemy for the lifetime of that unit.</summary>
internal readonly struct Scaling
{
    public readonly float Hp;
    public readonly float Atk;
    public readonly float Def;
    public readonly float Agi;
    public readonly float Dodge;
    public readonly bool IsBoss;
    public readonly float DamageScale;
    public readonly int Level;
    public readonly float HitCapPercent;

    public Scaling(float hp, float atk, float def, float agi, float dodge, bool isBoss, float damageScale, int level, float hitCapPercent)
    {
        DamageScale = damageScale;
        Level = level;
        HitCapPercent = hitCapPercent;
        Hp = hp;
        Atk = atk;
        Def = def;
        Agi = agi;
        Dodge = dodge;
        IsBoss = isBoss;
    }
}

/// <summary>
/// Tracks which battle units are enemies and what multipliers each one rolled.
/// Keyed by the IL2CPP object pointer, populated by the EnemyUnitStatus.Setup prefix.
/// </summary>
internal static class ScalingRegistry
{
    // Battles allocate a handful of units at a time; the cap only guards against
    // unbounded growth across a long session.
    private const int MaxTracked = 4096;

    private static readonly Dictionary<IntPtr, Scaling> Tracked = new();

    public static void Register(IntPtr unit, Scaling scaling)
    {
        if (Tracked.Count > MaxTracked)
            Tracked.Clear();

        Tracked[unit] = scaling;
    }

    public static bool TryGet(IntPtr unit, out Scaling scaling) => Tracked.TryGetValue(unit, out scaling);
}

/// <summary>Per-enemy overrides, parsed once per config change.</summary>
internal readonly struct SpecialRule
{
    public readonly float HpScale;
    public readonly float DamageScale;
    public readonly float DefScale;
    public readonly float AtkScale;
    public readonly float HitCapPercent;

    public SpecialRule(float hpScale, float damageScale, float defScale, float atkScale, float hitCapPercent)
    {
        HpScale = hpScale;
        DamageScale = damageScale;
        DefScale = defScale;
        AtkScale = atkScale;
        HitCapPercent = hitCapPercent;
    }
}

/// <summary>
/// Parses the special-enemy list. Each entry is either a bare id, which uses the
/// shared default scales, or "id:hpScale:damageScale" for one encounter that needs
/// its own values - useful when two bosses in the same list need very different help.
/// </summary>
internal static class SpecialEnemies
{
    private static string _lastRaw;
    private static Dictionary<long, SpecialRule> _rules = new();

    public static bool TryGet(string raw, long id, float defaultHp, float defaultDamage, float defaultHitCap, out SpecialRule rule)
    {
        if (raw != _lastRaw)
        {
            _lastRaw = raw;
            _rules = Parse(raw);
        }

        if (!_rules.TryGetValue(id, out rule))
            return false;

        // NaN marks "use the shared default", so a bare id follows the global setting.
        float hp = float.IsNaN(rule.HpScale) ? defaultHp : rule.HpScale;
        float dmg = float.IsNaN(rule.DamageScale) ? defaultDamage : rule.DamageScale;

        // Defence and attack default to the bulk and offence scales respectively, so a
        // two-field entry keeps behaving as before, while a four-field entry can pin
        // them independently - needed to reproduce a fight exactly as it was played.
        rule = new SpecialRule(
            hp,
            dmg,
            float.IsNaN(rule.DefScale) ? hp : rule.DefScale,
            float.IsNaN(rule.AtkScale) ? dmg : rule.AtkScale,
            float.IsNaN(rule.HitCapPercent) ? defaultHitCap : rule.HitCapPercent);

        return true;
    }

    private static Dictionary<long, SpecialRule> Parse(string raw)
    {
        var parsed = new Dictionary<long, SpecialRule>();
        if (string.IsNullOrWhiteSpace(raw))
            return parsed;

        foreach (string entry in raw.Split(','))
        {
            string[] fields = entry.Trim().Split(':');
            if (fields.Length == 0 || !long.TryParse(fields[0].Trim(), out long id))
                continue;

            float hp = fields.Length > 1 && float.TryParse(fields[1].Trim(), out float h) ? h : float.NaN;
            float dmg = fields.Length > 2 && float.TryParse(fields[2].Trim(), out float d) ? d : float.NaN;
            float def = fields.Length > 3 && float.TryParse(fields[3].Trim(), out float f) ? f : float.NaN;
            float atk = fields.Length > 4 && float.TryParse(fields[4].Trim(), out float a) ? a : float.NaN;
            float cap = fields.Length > 5 && float.TryParse(fields[5].Trim(), out float c) ? c : float.NaN;

            parsed[id] = new SpecialRule(hp, dmg, def, atk, cap);
        }

        return parsed;
    }
}

/// <summary>
/// Turns an enemy id into a stable multiplier. The same enemy always rolls the same
/// value, so stats stay consistent across encounters, reloads and save files.
/// </summary>
internal static class Roll
{
    /// <summary>
    /// The midpoint of a range, used for BOSSES instead of a roll. Bosses are designed
    /// encounters and a multi-phase boss shares one set of base stats across its phases,
    /// so rolling each phase separately made a later phase land weaker than an earlier
    /// one - Beast Vares Lv11 rolled x1.27 against Lv10's x1.30 despite identical base
    /// HP. Every phase of a boss now gets the same, predictable value.
    /// </summary>
    public static float Midpoint(StatRange range) => (range.Min + range.Max) * 0.5f;

    public static float ForStat(long enemyId, int salt, StatRange range)
    {
        if (range.Max <= range.Min)
            return range.Min;

        return range.Min + Normalized(enemyId, salt) * (range.Max - range.Min);
    }

    /// <summary>SplitMix64, giving a well-distributed value in [0,1) from an id and a per-stat salt.</summary>
    private static float Normalized(long enemyId, int salt)
    {
        unchecked
        {
            ulong h = (ulong)enemyId + 0x9E3779B97F4A7C15UL * (ulong)(salt + 1);
            h ^= h >> 30;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27;
            h *= 0x94D049BB133111EBUL;
            h ^= h >> 31;

            return (float)((h >> 11) / (double)(1UL << 53));
        }
    }
}
