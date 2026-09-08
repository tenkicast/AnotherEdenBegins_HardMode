using System;
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
    public const string Version = "1.0.0";

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
        Log.LogInfo($"Level-difference neutralisation: {Cfg.NeutralizeLevelDiff.Value}");
        Log.LogInfo($"Reserve party HP regen disabled: {Cfg.DisableBenchHpRegen.Value}");
        Log.LogInfo($"Reserve party MP regen disabled: {Cfg.DisableBenchMpRegen.Value}");
    }

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
