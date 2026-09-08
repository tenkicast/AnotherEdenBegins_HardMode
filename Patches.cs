using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Sava.InGame.Battle.Logic;
using Sava.Main.Domain;
using Sava.Main.MasterData;

namespace AEBHardMode;

/// <summary>
/// Scales enemy stats by postfixing the Raw* getters on EnemyUnitStatus. Nothing is
/// written back to the game, so unloading the plugin fully restores vanilla values.
/// </summary>
[HarmonyPatch]
internal static class EnemyStatPatches
{
    // Distinct salts keep the five stats from rolling in lockstep for a given enemy.
    private const int SaltHp = 1;
    private const int SaltAtk = 2;
    private const int SaltDef = 3;
    private const int SaltAgi = 4;
    private const int SaltDodge = 5;

    /// <summary>
    /// Registers an enemy's multipliers before Setup runs. This has to be a prefix:
    /// Setup seeds currentHp from the max-HP getter, so if we registered afterwards
    /// every enemy would spawn at a fraction of its boosted health.
    /// </summary>
    [HarmonyPatch(typeof(EnemyUnitStatus), nameof(EnemyUnitStatus.Setup))]
    [HarmonyPrefix]
    private static void Setup_Prefix(EnemyUnitStatus __instance, DomainEnemy _domainEnemy)
    {
        TierConfig tier = HardModePlugin.Cfg?.Active;
        if (tier == null || _domainEnemy == null)
            return;

        long id = _domainEnemy.Id;

        // Bosses carry very large base HP, so the tier's HP multiplier stretches their
        // fights far more than a regular encounter's. Scaled back separately.
        ModConfig cfg = HardModePlugin.Cfg;
        // MEASURED, and the enum is 0-BASED: Type=0 is every ordinary enemy, Type=1
        // covers Beast Vares (4844 base HP) and the scripted unkillable Beast King,
        // Type=2 the story bosses. ">=" with the default of 1 catches everything that
        // is not an ordinary enemy.
        bool isBoss = cfg.BossEnemyTypeId.Value >= 0 && _domainEnemy.Type >= cfg.BossEnemyTypeId.Value;
        bool isSpecial = SpecialEnemies.TryGet(
            cfg.SpecialEnemyIds.Value, id,
            cfg.SpecialEnemyHpScale.Value, cfg.SpecialEnemyDamageScale.Value,
            tier.MaxSingleHitPercent.Value,
            out SpecialRule special);

        float damageScale = 1f;
        if (isBoss)
            damageScale *= cfg.BossDamageScale.Value;
        if (isSpecial)
            damageScale *= special.DamageScale;

        // Bosses take the midpoint; only ordinary enemies are rolled per id.
        float hpRoll = isBoss ? Roll.Midpoint(tier.Hp) : Roll.ForStat(id, SaltHp, tier.Hp);

        // Higher-level enemies need more bulk because the party's damage outgrows them.
        // Uses the damage ramp's levels so both curves turn on at the same point.
        hpRoll *= LevelRampFactor(cfg, cfg.HpRampHighFactor.Value, _domainEnemy.Level);

        if (isBoss)
        {
            // Scale the BONUS, not the whole multiplier. A straight multiply would push
            // a boss below vanilla HP on Hard (1.4 x 0.7 = 0.98), making bosses weaker
            // than an unmodded game.
            hpRoll = 1f + ((hpRoll - 1f) * cfg.BossHpScale.Value);
        }

        // Named encounters that are punishing for reasons the mod cannot see, such as
        // story fights where healing is unavailable. Targeted by exact enemy id, which
        // is unique per enemy AND level, so similar enemies are untouched.
        float atkRoll = isBoss ? Roll.Midpoint(tier.Atk) : Roll.ForStat(id, SaltAtk, tier.Atk);
        float defRoll = isBoss ? Roll.Midpoint(tier.Def) : Roll.ForStat(id, SaltDef, tier.Def);

        if (isSpecial)
        {
            // Every bonus is scaled, not just HP. Leaving DEF at full strength would
            // lengthen the fight exactly as extra HP does, and leaving ATK at full
            // strength would keep the enemy hitting above vanilla however far the
            // final damage multiplier is reduced.
            hpRoll = 1f + ((hpRoll - 1f) * special.HpScale);
            defRoll = 1f + ((defRoll - 1f) * special.DefScale);
            atkRoll = 1f + ((atkRoll - 1f) * special.AtkScale);
        }

        // Special encounters keep vanilla speed and dodge. Losing the initiative in a
        // fight with no healing available is unrecoverable rather than merely harder,
        // and an enemy evade burns a turn the party cannot spare.
        float agiRoll = isSpecial ? 1f : (isBoss ? Roll.Midpoint(tier.Agi) : Roll.ForStat(id, SaltAgi, tier.Agi));
        float dodgeRoll = isSpecial ? 1f : (isBoss ? Roll.Midpoint(tier.Dodge) : Roll.ForStat(id, SaltDodge, tier.Dodge));

        var scaling = new Scaling(
            hpRoll,
            atkRoll,
            defRoll,
            agiRoll,
            dodgeRoll,
            isBoss,
            damageScale,
            _domainEnemy.Level,
            isSpecial ? special.HitCapPercent : tier.MaxSingleHitPercent.Value);

        ScalingRegistry.Register(__instance.Pointer, scaling);

        if (HardModePlugin.Cfg.VerboseLogging.Value)
        {
            // Boss-type enemies are prefixed so they can be picked out of a long log at
            // a glance, and effective HP is spelled out because base x multiplier is the
            // number that actually decides how long a fight runs.
            long effectiveHp = (long)(_domainEnemy.HitPoint * scaling.Hp);
            string marker = isBoss ? ">>> BOSS " : "    ";
            string tags = (isBoss ? " BOSS" : string.Empty) + (isSpecial ? " SPECIAL" : string.Empty);

            HardModePlugin.Logger.LogInfo(
                $"{marker}Enemy {id} '{_domainEnemy.Name}' Lv{_domainEnemy.Level} " +
                $"[Type={_domainEnemy.Type}{tags}] " +
                $"HP {_domainEnemy.HitPoint} x{scaling.Hp:0.00} = {effectiveHp}  " +
                $"ATK x{scaling.Atk:0.00} DEF x{scaling.Def:0.00} " +
                $"AGI x{scaling.Agi:0.00} DODGE x{scaling.Dodge:0.00}");

            HardModePlugin.Logger.LogInfo($"    resists: {DescribeResistances(_domainEnemy)}");
        }
    }

    /// <summary>
    /// Interpolates a per-level factor between 1.0 at the ramp's start level and
    /// <paramref name="highFactor"/> at its full level.
    /// </summary>
    private static float LevelRampFactor(ModConfig cfg, float highFactor, int level)
    {
        int startLevel = cfg.DamageRampStartLevel.Value;
        int fullLevel = cfg.DamageRampFullLevel.Value;

        if (highFactor == 1f || fullLevel <= startLevel || level <= startLevel)
            return 1f;

        if (level >= fullLevel)
            return highFactor;

        float t = (level - startLevel) / (float)(fullLevel - startLevel);

        return 1f + ((highFactor - 1f) * t);
    }

    /// <summary>
    /// Lists an enemy's eight resistance values. The numeric meanings follow the
    /// ResistType enum declaration order (NONE, GUARD, RELIEF, WEAK, ABSORB); the raw
    /// number is printed alongside the label so the mapping can be confirmed against
    /// what actually happens in a fight rather than trusted outright.
    /// </summary>
    private static string DescribeResistances(DomainEnemy enemy)
    {
        return "SLASH=" + Label(enemy.SlashResistType)
             + " THRUST=" + Label(enemy.ThrustResistType)
             + " BASH=" + Label(enemy.BashResistType)
             + " MAGIC=" + Label(enemy.MagicResistType)
             + " | FIRE=" + Label(enemy.FireResistType)
             + " EARTH=" + Label(enemy.EarthResistType)
             + " AIR=" + Label(enemy.AirResistType)
             + " WATER=" + Label(enemy.WaterResistType);
    }

    private static string Label(int resist) => resist switch
    {
        0 => "-",
        1 => "GUARD(1)",
        2 => "RELIEF(2)",
        3 => "WEAK(3)",
        4 => "ABSORB(4)",
        _ => $"?({resist})",
    };

    [HarmonyPatch(typeof(EnemyUnitStatus), nameof(EnemyUnitStatus.RawHpMax))]
    [HarmonyPostfix]
    private static void RawHpMax_Postfix(EnemyUnitStatus __instance, ref long __result)
    {
        if (ScalingRegistry.TryGet(__instance.Pointer, out Scaling s))
            __result = (long)(__result * s.Hp);
    }

    [HarmonyPatch(typeof(EnemyUnitStatus), nameof(EnemyUnitStatus.RawPhysicalAttackPower))]
    [HarmonyPostfix]
    private static void RawPhysicalAttackPower_Postfix(EnemyUnitStatus __instance, ref int __result)
    {
        if (ScalingRegistry.TryGet(__instance.Pointer, out Scaling s))
            __result = (int)(__result * s.Atk);
    }

    [HarmonyPatch(typeof(EnemyUnitStatus), nameof(EnemyUnitStatus.RawMagicAttackPower))]
    [HarmonyPostfix]
    private static void RawMagicAttackPower_Postfix(EnemyUnitStatus __instance, ref int __result)
    {
        if (ScalingRegistry.TryGet(__instance.Pointer, out Scaling s))
            __result = (int)(__result * s.Atk);
    }

    /// <summary>This game has one unified defense stat covering physical and magic damage alike.</summary>
    [HarmonyPatch(typeof(EnemyUnitStatus), nameof(EnemyUnitStatus.RawDefense))]
    [HarmonyPostfix]
    private static void RawDefense_Postfix(EnemyUnitStatus __instance, ref int __result)
    {
        if (ScalingRegistry.TryGet(__instance.Pointer, out Scaling s))
            __result = (int)(__result * s.Def);
    }

    [HarmonyPatch(typeof(EnemyUnitStatus), nameof(EnemyUnitStatus.RawAgility))]
    [HarmonyPostfix]
    private static void RawAgility_Postfix(EnemyUnitStatus __instance, ref int __result)
    {
        if (ScalingRegistry.TryGet(__instance.Pointer, out Scaling s))
            __result = (int)(__result * s.Agi);
    }

    /// <summary>
    /// Dodge lives on UnitStatus, the shared base of EnemyUnitStatus and PCUnitStatus,
    /// so this guards on the concrete type as well as on registration. Without both
    /// checks a freed enemy pointer could be reused by a party member and buff the player.
    /// </summary>
    [HarmonyPatch(typeof(UnitStatus), nameof(UnitStatus.RawAttackDodgeRate))]
    [HarmonyPostfix]
    private static void RawAttackDodgeRate_Postfix(UnitStatus __instance, ref int __result)
    {
        if (__instance.TryCast<EnemyUnitStatus>() == null)
            return;

        if (ScalingRegistry.TryGet(__instance.Pointer, out Scaling s))
            __result = (int)(__result * s.Dodge);
    }
}

/// <summary>
/// Removes the level-difference damage discount. Vanilla weakens enemies once you
/// out-level them, which is why over-levelling trivialises the game.
///
/// NOTE: an earlier version hooked the BattleLevelDiffDamageRate master-data getters
/// directly and crashed the game natively at startup, because those are FlatBuffers
/// generated types deserialised during boot. These two methods are ordinary battle
/// logic that merely READ that table, so they are safe to patch.
/// </summary>
[HarmonyPatch]
internal static class LevelDiffPatches
{
    // MEASURED from a live battle: the rates are on a 10000 scale, not 100.
    //   GetAttackDamageRate(pcLv=3, enemyLv=2)  = 10500  (+1 level  -> +5%)
    //   GetAttackDamageRate(pcLv=3, enemyLv=1)  = 11000  (+2 levels -> +10%)
    //   GetDefenseDamageRate(pcLv=1, enemyLv=3) =  9000  (-2 levels -> -10%)
    // So neutral is 10000 and each level of difference is worth 500. An earlier build
    // assumed 100 was neutral and reduced all damage to 1% of normal, i.e. zero.
    private const int Neutral = 10000;

    /// <summary>
    /// Clamps a level-difference rate to within the configured percentage of neutral,
    /// rather than flattening it to exactly neutral. Keeps a small advantage for being
    /// ahead in level while removing the runaway 5%-per-level scaling.
    /// </summary>
    private static int Clamp(int rate)
    {
        int maxDeviation = (int)(Neutral * (HardModePlugin.Cfg.MaxLevelAdvantagePercent.Value / 100f));

        // Above neutral is the player's ADVANTAGE for out-levelling: always clamped,
        // since that is what makes over-levelled content trivial.
        if (rate > Neutral + maxDeviation)
            return Neutral + maxDeviation;

        // Below neutral is the PENALTY for fighting above your level. Clamping this too
        // removes any drawback to being under-levelled, which is the opposite of the
        // intent - so it is left at vanilla unless explicitly disabled.
        if (!HardModePlugin.Cfg.KeepUnderLevelPenalty.Value && rate < Neutral - maxDeviation)
            return Neutral - maxDeviation;

        return rate;
    }

    private static bool Enabled =>
        HardModePlugin.Cfg is { ModActive: true, NeutralizeLevelDiff.Value: true };

        private static int _lastAtkRate = int.MinValue;
    private static int _lastDefRate = int.MinValue;

    /// <summary>
    /// Logs the real vanilla rate before overriding it. Forcing these to 100 assumed
    /// they were percentages; that assumption zeroed all damage, so the true scale has
    /// to be observed rather than guessed.
    /// </summary>
    [HarmonyPatch(typeof(DamageCalculator), nameof(DamageCalculator.GetAttackDamageRate))]
    [HarmonyPostfix]
    private static void GetAttackDamageRate_Postfix(int pcLevel, int enemyLevel, ref int __result)
    {
        if (HardModePlugin.Cfg is not { ModActive: true })
            return;

        if (HardModePlugin.Cfg.VerboseLogging.Value && __result != _lastAtkRate)
        {
            _lastAtkRate = __result;
            HardModePlugin.Logger.LogInfo(
                $"MEASURED GetAttackDamageRate(pcLv={pcLevel}, enemyLv={enemyLevel}) = {__result} (vanilla)");
        }

        if (Enabled)
            __result = Clamp(__result);
    }

    [HarmonyPatch(typeof(DamageCalculator), nameof(DamageCalculator.GetDefenseDamageRate))]
    [HarmonyPostfix]
    private static void GetDefenseDamageRate_Postfix(int pcLevel, int enemyLevel, ref int __result)
    {
        if (HardModePlugin.Cfg is not { ModActive: true })
            return;

        if (HardModePlugin.Cfg.VerboseLogging.Value && __result != _lastDefRate)
        {
            _lastDefRate = __result;
            HardModePlugin.Logger.LogInfo(
                $"MEASURED GetDefenseDamageRate(pcLv={pcLevel}, enemyLv={enemyLevel}) = {__result} (vanilla)");
        }

        if (Enabled)
            __result = Clamp(__result);
    }
}

/// <summary>
/// Scales the FINAL damage enemies deal and caps any single hit.
///
/// This exists because multiplying the attack STAT is unpredictable: the elemental
/// weakness bonus is applied on top of it, so a weakness hit lands at roughly
/// (stat multiplier x weakness bonus) and can delete a character outright while
/// ordinary attacks still feel weak. Hooking the single point where damage is
/// committed gives a linear dial instead, plus somewhere to clamp the spikes.
/// </summary>
[HarmonyPatch]
internal static class DamageCalcPatches
{
    [HarmonyPatch(typeof(DamageCalculator), nameof(DamageCalculator.Calculate))]
    [HarmonyPostfix]
    private static void Calculate_Postfix(BattleUnit attacker, ref Damage __result)
    {
        ModConfig cfg = HardModePlugin.Cfg;
        if (cfg is not { ModActive: true } || __result == null)
            return;

        // Player-dealt damage must be left alone; only enemies are scaled.
        if (attacker == null || attacker.UnitStatus == null)
            return;
        if (attacker.UnitStatus.TryCast<EnemyUnitStatus>() == null)
            return;

        long value = __result.value;
        if (value <= 0)
            return;

        float damageMultiplier = cfg.Active.DamageMultiplier.Value;

        bool attackerIsBoss = false;
        if (ScalingRegistry.TryGet(attacker.UnitStatus.Pointer, out Scaling attackerScaling))
        {
            attackerIsBoss = attackerScaling.IsBoss;
            damageMultiplier = RampedMultiplier(cfg, damageMultiplier, attackerScaling.Level);
            damageMultiplier *= attackerScaling.DamageScale;
        }

        long scaled = (long)(value * damageMultiplier);

        // Cap the result against the ATTACKER's reference rather than the target's,
        // because the target is passed by reference and may not be safe to read here.
        CurrentAttackerHitCap = attackerScaling.HitCapPercent;
        long cap = SingleHitCap(cfg);
        if (cap > 0 && scaled > cap)
            scaled = cap;

        if (scaled < 1)
            scaled = 1;

        if (cfg.VerboseLogging.Value && value != _lastEnemyDamageLogged)
        {
            _lastEnemyDamageLogged = value;
            // Target max HP is printed so a hit can be read as a FRACTION of the
            // character it landed on. "800 damage" means nothing on its own; "800 of
            // 2400" is the number that decides whether a fight is dangerous.
            string share = CurrentTargetMaxHp > 0
                ? $" [{scaled * 100f / CurrentTargetMaxHp:0}% of {CurrentTargetMaxHp} max HP]"
                : string.Empty;

            HardModePlugin.Logger.LogInfo(
                $"ENEMY hit{(attackerIsBoss ? " (BOSS)" : string.Empty)}: {value} -> {scaled} "
                + $"(x{damageMultiplier:0.00}){share}");
        }

        __result.value = scaled;
    }

    private static long _lastEnemyDamageLogged = long.MinValue;

    /// <summary>
    /// Scales the tier multiplier down for low-level enemies. A flat value punishes an
    /// early-game mob as hard as a late one, even though the party's own damage grows
    /// several times over across the same span.
    /// </summary>
    private static float RampedMultiplier(ModConfig cfg, float tierMultiplier, int level)
    {
        int startLevel = cfg.DamageRampStartLevel.Value;
        int fullLevel = cfg.DamageRampFullLevel.Value;
        float low = cfg.Active.DamageRampLow.Value;

        if (level >= fullLevel || fullLevel <= startLevel)
            return tierMultiplier;

        if (level <= startLevel)
            return low;

        float t = (level - startLevel) / (float)(fullLevel - startLevel);

        return low + ((tierMultiplier - low) * t);
    }

    /// <summary>
    /// Resolves the single-hit cap in absolute HP. Returns 0 when capping is disabled
    /// or when the target's max HP could not be read.
    /// </summary>
    private static long SingleHitCap(ModConfig cfg)
    {
        float percent = CurrentAttackerHitCap;
        if (percent <= 0f)
            return 0;

        long targetMaxHp = CurrentTargetMaxHp;
        if (targetMaxHp <= 0)
            return 0;

        return (long)(targetMaxHp * (percent / 100f));
    }

    /// <summary>
    /// Max HP of the unit currently being damaged, captured by the prefix below.
    /// Calculate takes its target by reference, so the value is read before the
    /// original method runs rather than dereferencing the ref afterwards.
    /// </summary>
    private static long CurrentTargetMaxHp;
    private static float CurrentAttackerHitCap;

    [HarmonyPatch(typeof(DamageCalculator), nameof(DamageCalculator.Calculate))]
    [HarmonyPrefix]
    private static void Calculate_Prefix(ref BattleUnit target)
    {
        CurrentTargetMaxHp = 0;

        if (target == null || target.UnitStatus == null)
            return;

        UnitStatus status = target.UnitStatus;
        if (status.TryCast<PCUnitStatus>() == null)
            return;

        CurrentTargetMaxHp = status.HpMax();
    }
}

/// <summary>
/// Stops the benched Another Party from passively regenerating each turn, which
/// otherwise turns Valor Change into a free full-heal rotation.
/// </summary>
[HarmonyPatch]
internal static class BenchRegenPatches
{
    [HarmonyPatch(typeof(Turn), nameof(Turn.SubHpHealRate), MethodType.Getter)]
    [HarmonyPostfix]
    private static void SubHpHealRate_Postfix(ref int __result)
    {
        if (HardModePlugin.Cfg is { ModActive: true, DisableBenchHpRegen.Value: true })
            __result = 0;
    }

    [HarmonyPatch(typeof(Turn), nameof(Turn.SubMpHealRate), MethodType.Getter)]
    [HarmonyPostfix]
    private static void SubMpHealRate_Postfix(ref int __result)
    {
        if (HardModePlugin.Cfg is { ModActive: true, DisableBenchMpRegen.Value: true })
            __result = 0;
    }
}

/// <summary>
/// Scales weakness damage dealt by the player.
///
/// Two earlier attempts missed: WeaknessDamageAdditionalRate and
/// UnitAttackConfig.DamageBoostRateByWeaknessResistType were both hooked and neither
/// fired once during a real weakness hit. CreateDamage is the actual choke point - it
/// receives the final damage value and the ResistType together, so a player weakness
/// hit can be identified exactly rather than inferred.
///
/// Chain Points are unaffected: they come from BattleChainAbility.OnWeaknessDamaged,
/// an event carrying only a BattleUnitType and no damage value.
/// </summary>
[HarmonyPatch]
internal static class WeaknessPatches
{
    private static long _lastPlayerLogged = long.MinValue;

    /// <summary>
    /// Single choke point for scaling player damage. CreateDamage receives the final
    /// value, the resist type and the originating UnitAttack all as parameters, so a
    /// weakness hit and a chain hit can each be identified exactly, with no reaching
    /// into objects that may not be fully constructed.
    /// </summary>
    [HarmonyPatch(typeof(DefaultDamageLogic), nameof(DefaultDamageLogic.CreateDamage))]
    [HarmonyPrefix]
    private static void CreateDamage_Prefix(UnitAttack attack, BattleUnit attacker, ref long value, ResistType resistType, int skillPower)
    {
        ModConfig cfg = HardModePlugin.Cfg;
        if (cfg is not { ModActive: true } || value <= 0)
            return;

        // Both multipliers default to 1.0 from v1.5.0, which makes this hook a no-op.
        // It stays INSTALLED rather than being skipped, because its verbose log is the
        // only place a chain hit and a weakness hit can be compared side by side - that
        // is how the two were measured (chains raw 432-644, weakness raw 383-530) - and
        // because the dials are still there for anyone who wants them.
        if (!cfg.VerboseLogging.Value
            && cfg.PlayerWeaknessDamageMultiplier.Value == 1f
            && cfg.PlayerChainDamageMultiplier.Value == 1f)
            return;

        // Only the player's damage is scaled; enemies keep theirs.
        if (attacker == null || attacker.UnitStatus == null)
            return;
        if (attacker.UnitStatus.TryCast<PCUnitStatus>() == null)
            return;

        bool isWeak = resistType == ResistType.WEAK;
        bool isChain = attack != null && attack.isChainAttack;

        long before = value;

        // The two reductions must NOT stack. Landing a chain skill on an enemy's
        // weakness is the most skilful play the combat system asks for; multiplying
        // both penalties together made it hit for less than an ordinary weakness
        // poke, punishing the better play. Apply whichever single reduction is
        // stronger instead.
        float weaknessMultiplier = cfg.PlayerWeaknessDamageMultiplier.Value;
        float chainMultiplier = cfg.PlayerChainDamageMultiplier.Value;

        float multiplier = (isWeak, isChain) switch
        {
            (true, true) => Mathf.Min(weaknessMultiplier, chainMultiplier),
            (true, false) => weaknessMultiplier,
            (false, true) => chainMultiplier,
            _ => 1f,
        };

        long after = (long)(before * multiplier);
        if (after < 1)
            after = 1;

        // Logs EVERY player hit, not just reduced ones, so a chain skill can be compared
        // directly against an ordinary attack that lands on a weakness. Throttled by
        // damage value so a repeated identical hit does not flood the log.
        if (cfg.VerboseLogging.Value && before != _lastPlayerLogged)
        {
            _lastPlayerLogged = before;

            string kind = isChain ? "CHAIN" : "normal";
            string resist = resistType switch
            {
                ResistType.WEAK => "WEAK",
                ResistType.GUARD => "GUARD",
                ResistType.RELIEF => "RELIEF",
                ResistType.ABSORB => "ABSORB",
                _ => "-",
            };

            HardModePlugin.Logger.LogInfo(
                "PLAYER " + kind.PadRight(6) + " vs " + resist.PadRight(6) + " : "
                + before.ToString().PadLeft(5) + " -> " + after.ToString().PadLeft(5)
                + $" (x{multiplier:0.00}, power={skillPower})");
        }

        value = after;
    }
}

/// <summary>
/// Reports the actual agility values of both sides, so "the enemy is faster than me"
/// can be checked against numbers rather than impressions. Logging only - it changes
/// nothing. Enemy agility already carries the mod's multiplier at this point, and the
/// unmodified value is printed alongside it.
/// </summary>
[HarmonyPatch]
internal static class AgilityProbe
{
    private static readonly HashSet<string> Seen = new();

    [HarmonyPatch(typeof(UnitStatus), nameof(UnitStatus.Agility))]
    [HarmonyPostfix]
    private static void Agility_Postfix(UnitStatus __instance, ref int __result)
    {
        ModConfig cfg = HardModePlugin.Cfg;
        if (cfg is not { ModActive: true } || !cfg.VerboseLogging.Value)
            return;

        bool isEnemy = __instance.TryCast<EnemyUnitStatus>() != null;
        string side = isEnemy ? "ENEMY" : "PC   ";

        float multiplier = 1f;
        if (isEnemy && ScalingRegistry.TryGet(__instance.Pointer, out Scaling scaling))
            multiplier = scaling.Agi;

        int vanilla = multiplier > 0f ? (int)(__result / multiplier) : __result;

        int level = __instance.Level();

        // Keyed by side, level and value so each distinct combination is reported once.
        // Level is what makes the numbers comparable - "the enemy is faster" only means
        // something once you can see whether it is also a higher level than you.
        string key = side + ":" + level + ":" + __result;
        if (!Seen.Add(key))
            return;

        if (Seen.Count > 200)
            Seen.Clear();

        HardModePlugin.Logger.LogInfo(
            "AGILITY " + side + " Lv" + level + " = " + __result
            + (isEnemy ? $"  (vanilla {vanilla}, mod x{multiplier:0.00})" : "  (unmodified)"));
    }
}

/// <summary>
/// Multiplies the experience and money enemies award, to keep a difficulty mod from
/// turning into a grind mod: tougher enemies take longer to kill, so at vanilla reward
/// rates the extra time is spent repeating fights rather than progressing.
///
/// These hang off DomainEnemy, the domain wrapper whose Level, Type and HitPoint the
/// stat patches already read safely every battle. The underlying FlatBuffers types
/// (Sava.Main.MasterData.Enemy and its GeneratedSerializer) are deliberately NOT
/// touched - patching generated master-data getters crashed the game natively during
/// startup once already.
/// </summary>
[HarmonyPatch]
internal static class RewardPatches
{
    private static bool _loggedExp;
    private static bool _loggedMoney;

    /// <summary>
    /// Scaling here rather than at the battle-result screen means the game's own
    /// level-difference correction still runs afterwards on the boosted figure, so
    /// grinding enemies far below your level stays as unrewarding as vanilla intends.
    /// </summary>
    [HarmonyPatch(typeof(DomainEnemy), nameof(DomainEnemy.Experience), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Experience_Postfix(DomainEnemy __instance, ref long __result)
    {
        ModConfig cfg = HardModePlugin.Cfg;
        if (cfg is not { ModActive: true } || __result <= 0)
            return;

        float multiplier = cfg.ExperienceMultiplier.Value;
        if (multiplier == 1f)
            return;

        long before = __result;
        __result = (long)((__result * (double)multiplier) + 0.5d);

        if (cfg.VerboseLogging.Value && !_loggedExp)
        {
            _loggedExp = true;
            HardModePlugin.Logger.LogInfo(
                $"REWARD exp: {before} -> {__result} (x{multiplier:0.00}) [first award only, from '{__instance.Name}']");
        }
    }

    [HarmonyPatch(typeof(DomainEnemy), nameof(DomainEnemy.Gold), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Gold_Postfix(ref int __result) => ScaleMoney(ref __result, "gold");

    /// <summary>
    /// The demo populates Gold on some enemies and DropCurrency on others, so both are
    /// scaled. They are alternative fields on the same reward, never summed, so this
    /// cannot double up.
    /// </summary>
    [HarmonyPatch(typeof(DomainEnemy), nameof(DomainEnemy.DropCurrency), MethodType.Getter)]
    [HarmonyPostfix]
    private static void DropCurrency_Postfix(ref int __result) => ScaleMoney(ref __result, "dropCurrency");

    private static void ScaleMoney(ref int result, string field)
    {
        ModConfig cfg = HardModePlugin.Cfg;
        if (cfg is not { ModActive: true } || result <= 0)
            return;

        float multiplier = cfg.MoneyMultiplier.Value;
        if (multiplier == 1f)
            return;

        int before = result;
        result = (int)((result * (double)multiplier) + 0.5d);

        if (cfg.VerboseLogging.Value && !_loggedMoney)
        {
            _loggedMoney = true;
            HardModePlugin.Logger.LogInfo(
                $"REWARD {field}: {before} -> {result} (x{multiplier:0.00}) [first award only]");
        }
    }
}

/// <summary>
/// Makes fleeing always succeed, by forcing the escape rate the battle logic reads.
///
/// A failed escape costs the party a turn and leaves it in the fight it was trying
/// to leave - a punishment for a decision already made rather than a decision of its
/// own, and a harsh one in a mod where enemies hit harder.
///
/// This does NOT unlock fights the game forbids fleeing. Story and boss battles hide
/// the Escape command outright, which is a separate gate; only the dice roll behind
/// an escape that was already offered is removed.
///
/// TWO EARLIER ATTEMPTS FAILED, both by hooking EscapeRateMaster.get_Rate:
///
///   v1.6.0  CRASHED ON LAUNCH. The log showed the hook install, run once, then die:
///             MEASURED EscapeRate id=1 = 127 (vanilla) -> forcing 10000
///           get_Rate is read while master data is still being CONSTRUCTED, and
///           returning a foreign value into that native load path is fatal - the same
///           family of failure as Turn.SubHpHealRate. Checking that EscapeRateMaster
///           is a plain POCO rather than a FlatSharp type was necessary but NOT
///           sufficient: WHEN a getter runs matters as much as what type owns it.
///
///   v1.6.1  Gated the override on a battle being on screen. That fixed the crash,
///           but the battle menu came up with NO COMMANDS AT ALL, and the log said
///           why: "ESCAPE rate id=0: 0 (vanilla)". The rates are a TABLE keyed by
///           attempt number - id=0 is 0, id=1 is 127 - so overriding every row at
///           once corrupted the lookup the command menu depends on.
///
/// So the master data is the wrong lever entirely: it is shared, table-shaped, and
/// read in contexts that have nothing to do with fleeing. BattleEscapeEvent is the
/// actual decision point - ordinary battle logic in Sava.InGame.Battle.Logic, the
/// same namespace as DamageCalculator, carrying the attempt counter that the rate
/// table is keyed by. Nothing outside a live escape attempt touches it.
/// </summary>
[HarmonyPatch]
internal static class EscapePatches
{
    /// <summary>
    /// Forces the escape check to succeed. Only ever flips a false to a true.
    ///
    /// VERIFIED from a real battle - the log printed
    ///     ESCAPE attempt #1: vanilla said FAIL -> forced SUCCESS
    /// twice, with the command menu intact and no errors. CanExecute is the success
    /// roll, not the "is Escape offered" gate, so battles that forbid fleeing stay
    /// unfleeable: their Escape command never appears and this never runs.
    /// </summary>
    [HarmonyPatch(typeof(BattleEscapeEvent), nameof(BattleEscapeEvent.CanExecute))]
    [HarmonyPostfix]
    private static void CanExecute_Postfix(BattleEscapeEvent __instance, ref bool __result)
    {
        ModConfig cfg = HardModePlugin.Cfg;
        if (cfg is not { ModActive: true, AlwaysEscape.Value: true })
            return;

        if (__result)
            return;

        if (cfg.VerboseLogging.Value)
        {
            HardModePlugin.Logger.LogInfo(
                $"ESCAPE attempt #{__instance.count}: vanilla said FAIL -> forced SUCCESS");
        }

        __result = true;
    }
}

/// <summary>
/// Dumps every boss-type enemy in the game once, straight from the master data
/// repository. Multi-phase bosses appear as separate ids at consecutive levels and
/// each phase needs its own rule - Galliard's Lv20 and Lv21 phases were running at
/// full scaling for exactly this reason, having been tuned only at Lv18 and Lv19.
/// </summary>
[HarmonyPatch]
internal static class BossRosterDump
{
    private static bool _dumped;

    [HarmonyPatch(typeof(DomainEnemyRepository), nameof(DomainEnemyRepository.OnMasterDataLoaded))]
    [HarmonyPostfix]
    private static void OnMasterDataLoaded_Postfix(DomainEnemyRepository __instance)
    {
        if (_dumped || HardModePlugin.Cfg is not { DumpBossRoster.Value: true })
            return;

        _dumped = true;

        try
        {
            var list = __instance.domainEnemyList;
            if (list == null)
                return;

            HardModePlugin.Logger.LogInfo("=== BOSS ROSTER (Type >= 1) ===");

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                DomainEnemy enemy = list[i];
                if (enemy == null || enemy.Type < 1)
                    continue;

                count++;
                HardModePlugin.Logger.LogInfo(
                    "  " + enemy.Id
                    + "  Lv" + enemy.Level.ToString().PadRight(3)
                    + " Type=" + enemy.Type
                    + "  baseHP=" + enemy.HitPoint.ToString().PadRight(9)
                    + " '" + enemy.Name + "'");
            }

            HardModePlugin.Logger.LogInfo($"=== {count} boss-type enemies, {list.Count} enemies total ===");
        }
        catch (System.Exception ex)
        {
            HardModePlugin.Logger.LogError($"Boss roster dump failed: {ex.Message}");
        }
    }
}
