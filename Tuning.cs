using System;
using System.Collections.Generic;
using System.Globalization;

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

    private static void EnsureParsed(string raw)
    {
        if (raw == _lastRaw)
            return;

        _lastRaw = raw;
        _rules = Parse(raw);
    }

    /// <summary>
    /// Every id the rule list actually produced, so startup can report what was read
    /// rather than what was written. A silent mis-parse is exactly how the culture bug
    /// survived to release.
    /// </summary>
    public static IEnumerable<long> ParsedIds(string raw)
    {
        EnsureParsed(raw);
        return _rules.Keys;
    }

    public static bool TryGet(string raw, long id, float defaultHp, float defaultDamage, float defaultHitCap, out SpecialRule rule)
    {
        EnsureParsed(raw);

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

    /// <summary>
    /// Parses the rule list. EVERY number here is read with InvariantCulture, and that
    /// is not cosmetic.
    ///
    /// These values are authored with a DOT in the config file and in the shipped
    /// defaults. On a machine whose locale uses a COMMA as the decimal separator -
    /// Italian, German, French, Spanish, most of Europe and South America - a
    /// culture-sensitive float.TryParse reads the dot as a THOUSANDS separator, so
    /// "0.5" silently becomes 5 and "0.75" becomes 75.
    ///
    /// MEASURED from a user log on an it-IT machine, Galliard Lv18 (0.5:0.75):
    ///     HP x3.21  ATK x16.00  DEF x1.60  and a final damage multiplier of x108
    ///     ENEMY hit (BOSS): 16470 -> 1778760  [156581% of max HP]
    /// Every one of those figures is reproduced exactly by the mis-parse. Forest
    /// Guardian (0.6:0.8) came out at x10.44 final damage instead of x1.044, which is
    /// why it one-shot on all three tiers.
    /// </summary>
    private static Dictionary<long, SpecialRule> Parse(string raw)
    {
        var parsed = new Dictionary<long, SpecialRule>();
        if (string.IsNullOrWhiteSpace(raw))
            return parsed;

        foreach (string entry in raw.Split(','))
        {
            string[] fields = entry.Trim().Split(':');
            if (fields.Length == 0 || !long.TryParse(fields[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id))
                continue;

            float hp = fields.Length > 1 && float.TryParse(fields[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float h) ? h : float.NaN;
            float dmg = fields.Length > 2 && float.TryParse(fields[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float d) ? d : float.NaN;
            float def = fields.Length > 3 && float.TryParse(fields[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : float.NaN;
            float atk = fields.Length > 4 && float.TryParse(fields[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float a) ? a : float.NaN;
            float cap = fields.Length > 5 && float.TryParse(fields[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float c) ? c : float.NaN;

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

/// <summary>
/// Parses and caches a comma-separated list of integer ids from a config string.
/// Cached on the raw string itself, so editing the config picks the new list up
/// without a restart and a malformed entry is simply skipped rather than throwing
/// inside a hook.
/// </summary>
internal static class IdList
{
    private static string _lastRaw;
    private static HashSet<int> _ids = new();

    public static bool Contains(string raw, int id)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!ReferenceEquals(raw, _lastRaw) && raw != _lastRaw)
        {
            _ids = Parse(raw);
            _lastRaw = raw;
        }

        return _ids.Contains(id);
    }

    private static HashSet<int> Parse(string raw)
    {
        var ids = new HashSet<int>();

        foreach (string part in raw.Split(','))
        {
            string trimmed = part.Trim();

            if (trimmed.Length != 0 && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                ids.Add(value);
        }

        return ids;
    }
}
