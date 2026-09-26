using System;
using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace AEBHardMode;

[BepInPlugin(Guid, Name, Version)]
public class HardModePlugin : BasePlugin
{
    public const string Guid = "tenkicast_anotheredenbegins_hardmode";
    public const string Name = "Another Eden Begins - Hard Mode";
    public const string Version = "1.0.2";

    internal static ModConfig Cfg;
    internal static ManualLogSource Logger;

    public override void Load()
    {
        Logger = Log;
        Cfg = new ModConfig(Config);

        // Patches are INSTALLED conditionally, not merely gated inside their bodies.
        // A hook that crashes the game natively cannot be switched off after the fact,
        // so every feature must be disableable from the config without the patch ever
        // being applied.
        if (Cfg.ModActive)
        {
            ApplyPatches(typeof(EnemyStatPatches), "enemy stat scaling");

            if (Cfg.EnableDamageHook.Value)
                ApplyPatches(typeof(DamageCalcPatches), "final damage multiplier and single-hit cap");

            ApplyPatches(typeof(WeaknessPatches), "player weakness and chain skill damage");
            ApplyPatches(typeof(AgilityProbe), "agility measurement");

            // New hook on a master-data repository - installed only when asked for.
            if (Cfg.DumpBossRoster.Value)
                ApplyPatches(typeof(BossRosterDump), "boss roster dump");
            // Always installed so it can report the real vanilla rates, but it only
            // OVERRIDES them when Neutralize Level Difference is enabled. Forcing these
            // to 100 on an unverified assumption zeroed all damage once already.
            ApplyPatches(typeof(LevelDiffPatches),
                Cfg.NeutralizeLevelDiff.Value
                    ? "level-difference neutralisation"
                    : "level-difference measurement only (override disabled)");

            if (Cfg.DisableBenchHpRegen.Value || Cfg.DisableBenchMpRegen.Value)
                ApplyPatches(typeof(BenchRegenPatches), "reserve party regen");

            // Only installed when it would actually change something, so a player who
            // wants vanilla rewards never carries the hook at all.
            if (Cfg.ExperienceMultiplier.Value != 1f || Cfg.MoneyMultiplier.Value != 1f)
                ApplyPatches(typeof(RewardPatches), "experience and money rewards");

            if (Cfg.AlwaysEscape.Value)
                ApplyPatches(typeof(EscapePatches), "guaranteed escape");

            // Installed only when it would actually do something, so a player who has
            // listed no item never carries the shop hooks at all.
            if (Cfg.DumpShopContents.Value
                || !string.IsNullOrWhiteSpace(Cfg.FreeItemIds.Value)
                || !string.IsNullOrWhiteSpace(Cfg.UnlimitedStockItemIds.Value))
                ApplyPatches(typeof(ShopPatches), "shop price and stock overrides");
        }

        if (!Cfg.ModActive)
        {
            // Difficulty is the master switch, so report the whole mod as dormant
            // rather than listing per-feature toggles that are not actually running.
            Log.LogInfo("Difficulty is Off - the game is fully vanilla. No stat scaling, "
                        + "no level-difference change, no reserve party regen change.");
            return;
        }

        TierConfig tier = Cfg.Active;
        Log.LogInfo(
            $"Tier {Cfg.Difficulty.Value}: " +
            $"HP x{tier.HpMin.Value:0.00}-{tier.HpMax.Value:0.00}, " +
            $"ATK x{tier.AtkMin.Value:0.00}-{tier.AtkMax.Value:0.00}, " +
            $"DEF x{tier.DefMin.Value:0.00}-{tier.DefMax.Value:0.00}, " +
            $"AGI x{tier.AgiMin.Value:0.00}-{tier.AgiMax.Value:0.00}, " +
            $"DODGE x{tier.DodgeMin.Value:0.00}-{tier.DodgeMax.Value:0.00}");

        Log.LogInfo($"Damage hook: {Cfg.EnableDamageHook.Value} (x{Cfg.Active.DamageMultiplier.Value:0.00}, "
                    + $"single hit cap {(Cfg.Active.MaxSingleHitPercent.Value <= 0f ? "NONE - one-shots possible" : $"{Cfg.Active.MaxSingleHitPercent.Value:0}% of max HP")})");
        Log.LogInfo($"Rewards: EXP x{Cfg.ExperienceMultiplier.Value:0.00}, money x{Cfg.MoneyMultiplier.Value:0.00}");
        Log.LogInfo($"Player damage: weakness x{Cfg.PlayerWeaknessDamageMultiplier.Value:0.00}, "
                    + $"chain x{Cfg.PlayerChainDamageMultiplier.Value:0.00}"
                    + (Cfg.PlayerWeaknessDamageMultiplier.Value == 1f && Cfg.PlayerChainDamageMultiplier.Value == 1f
                        ? " (both vanilla - difficulty is carried by enemy stats alone)"
                        : string.Empty));
        Log.LogInfo($"Always escape: {Cfg.AlwaysEscape.Value}"
                    + (Cfg.AlwaysEscape.Value ? " (failed escape rolls are forced to succeed; battles that forbid fleeing are unaffected)" : string.Empty));
        string freeIds = string.IsNullOrWhiteSpace(Cfg.FreeItemIds.Value) ? "none" : Cfg.FreeItemIds.Value;
        string stockIds = string.IsNullOrWhiteSpace(Cfg.UnlimitedStockItemIds.Value) ? "none" : Cfg.UnlimitedStockItemIds.Value;
        Log.LogInfo($"Shop: free item ids {freeIds}, unlimited stock item ids {stockIds}"
                    + (Cfg.DumpShopContents.Value ? " (dumping shop contents)" : string.Empty));
        ReportSpecialRules();
        Log.LogInfo($"Level-difference neutralisation: {Cfg.NeutralizeLevelDiff.Value}");
        Log.LogInfo($"Reserve party HP regen disabled: {Cfg.DisableBenchHpRegen.Value}");
        Log.LogInfo($"Reserve party MP regen disabled: {Cfg.DisableBenchMpRegen.Value}");
    }

    /// <summary>
    /// Prints the per-encounter rules AS PARSED, not as written, with the numbers
    /// formatted invariantly so a dot always reads as a decimal point.
    ///
    /// This exists because a culture bug shipped in 1.0.1: on a comma-decimal locale
    /// the old parser read "0.5" as 5 and "0.75" as 75, which turned one boss into
    /// ATK x16 and a x108 damage multiplier. Nothing in the log showed what had been
    /// read, so it took a user's boss-scaling dump to spot it. If a value here ever
    /// looks ten or a hundred times too large, the parse is wrong - not the tuning.
    /// </summary>
    private void ReportSpecialRules()
    {
        string raw = Cfg.SpecialEnemyIds.Value;

        if (string.IsNullOrWhiteSpace(raw))
        {
            Log.LogInfo("Per-encounter rules: none configured.");
            return;
        }

        int count = 0;

        foreach (long id in SpecialEnemies.ParsedIds(raw))
        {
            if (!SpecialEnemies.TryGet(raw, id,
                    Cfg.SpecialEnemyHpScale.Value, Cfg.SpecialEnemyDamageScale.Value,
                    Cfg.Active?.MaxSingleHitPercent.Value ?? 0f, out SpecialRule rule))
                continue;

            count++;
            Log.LogInfo(
                $"  rule {id}: hp {F(rule.HpScale)} dmg {F(rule.DamageScale)} "
                + $"def {F(rule.DefScale)} atk {F(rule.AtkScale)} "
                + $"cap {(rule.HitCapPercent <= 0f ? "off" : F(rule.HitCapPercent) + "%")}");
        }

        Log.LogInfo($"Per-encounter rules: {count} parsed (values shown as read, decimal point is '.').");
    }

    /// <summary>Invariant so the log is readable and comparable on any machine.</summary>
    private static string F(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private void ApplyPatches(Type patchClass, string description)
    {
        try
        {
            Harmony.CreateAndPatchAll(patchClass, $"{Guid}.{patchClass.Name}");
            Log.LogInfo($"Hooked {description}.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to hook {description} - that feature is inactive. {ex.Message}");
        }
    }
}
