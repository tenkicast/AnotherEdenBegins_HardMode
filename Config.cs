using BepInEx.Configuration;

namespace AEBHardMode;

/// <summary>Every multiplier for one difficulty tier, editable without rebuilding.</summary>
internal sealed class TierConfig
{
    public readonly ConfigEntry<float> HpMin, HpMax;
    public readonly ConfigEntry<float> AtkMin, AtkMax;
    public readonly ConfigEntry<float> DefMin, DefMax;
    public readonly ConfigEntry<float> AgiMin, AgiMax;
    public readonly ConfigEntry<float> DodgeMin, DodgeMax;

    public TierConfig(
        ConfigFile cfg, string section,
        float hpMin, float hpMax,
        float atkMin, float atkMax,
        float defMin, float defMax,
        float agiMin, float agiMax,
        float dodgeMin, float dodgeMax,
        float damageMult,
        float dmgRampLow,
        float maxHitPercent)
    {
        HpMin = cfg.Bind(section, "MaxHP Min", hpMin, "Lower bound of the max-HP multiplier. 1.5 = +50% HP.");
        HpMax = cfg.Bind(section, "MaxHP Max", hpMax, "Upper bound of the max-HP multiplier.");

        AtkMin = cfg.Bind(section, "Attack Min", atkMin, "Lower bound for BOTH physical and magic attack power.");
        AtkMax = cfg.Bind(section, "Attack Max", atkMax, "Upper bound for BOTH physical and magic attack power.");

        DefMin = cfg.Bind(section, "Defense Min", defMin, "Lower bound of the defense multiplier. This game has a single unified defense stat covering both physical and magic damage.");
        DefMax = cfg.Bind(section, "Defense Max", defMax, "Upper bound of the defense multiplier.");

        AgiMin = cfg.Bind(section, "Agility Min", agiMin, "Lower bound of the agility multiplier. Raises enemy turn frequency.");
        AgiMax = cfg.Bind(section, "Agility Max", agiMax, "Upper bound of the agility multiplier.");

        DodgeMin = cfg.Bind(section, "DodgeRate Min", dodgeMin, "Lower bound of the enemy dodge-rate multiplier (the HIT/AVO equivalent).");
        DodgeMax = cfg.Bind(section, "DodgeRate Max", dodgeMax, "Upper bound of the enemy dodge-rate multiplier.");

        DamageMultiplier = cfg.Bind(section, "Damage Multiplier", damageMult,
            "Multiplies the FINAL damage enemies deal, applied after weakness, critical and boost bonuses. " +
            "Unlike the attack stat this is linear: 2.0 means exactly twice the damage. This is the main " +
            "difficulty dial. It is a single flat value, not a range, so damage stays predictable.");

        DamageRampLow = cfg.Bind(section, "Damage Ramp Low Multiplier", dmgRampLow,
            "Enemy damage multiplier at or below Damage Ramp Start Level, climbing to this tier's Damage "
            + "Multiplier by Damage Ramp Full Level. This is PER-TIER, because Hard's ceiling is also "
            + "1.60 - a single shared floor would leave Hard with no ramp at all. Set equal to this tier's Damage "
            + "Multiplier to disable the ramp for that tier.");

        MaxSingleHitPercent = cfg.Bind(section, "Max Single Hit Percent", maxHitPercent,
            "Caps any single enemy hit at this percentage of the target's maximum HP, so one unlucky "
            + "elemental-weakness roll cannot delete a character from full health. "
            + "SET TO 0 FOR NO CAP - a full-health character can then be one-shot. "
            + "Higher values mean less protection, so raise it or zero it as you move up the tiers.");
    }

    public readonly ConfigEntry<float> DamageMultiplier;
    public readonly ConfigEntry<float> DamageRampLow;
    public readonly ConfigEntry<float> MaxSingleHitPercent;

    public StatRange Hp => new(HpMin.Value, HpMax.Value);
    public StatRange Atk => new(AtkMin.Value, AtkMax.Value);
    public StatRange Def => new(DefMin.Value, DefMax.Value);
    public StatRange Agi => new(AgiMin.Value, AgiMax.Value);
    public StatRange Dodge => new(DodgeMin.Value, DodgeMax.Value);
}

internal sealed class ModConfig
{
    public readonly ConfigEntry<Tier> Difficulty;
    public readonly ConfigEntry<bool> NeutralizeLevelDiff;
    public readonly ConfigEntry<float> MaxLevelAdvantagePercent;
    public readonly ConfigEntry<bool> KeepUnderLevelPenalty;
    public readonly ConfigEntry<int> DamageRampFullLevel;
    public readonly ConfigEntry<int> DamageRampStartLevel;
    public readonly ConfigEntry<float> HpRampHighFactor;
    public readonly ConfigEntry<bool> EnableDamageHook;
    public readonly ConfigEntry<float> PlayerWeaknessDamageMultiplier;
    public readonly ConfigEntry<int> BossEnemyTypeId;
    public readonly ConfigEntry<float> BossHpScale;
    public readonly ConfigEntry<float> BossDamageScale;
    public readonly ConfigEntry<string> SpecialEnemyIds;
    public readonly ConfigEntry<float> SpecialEnemyHpScale;
    public readonly ConfigEntry<float> SpecialEnemyDamageScale;
    public readonly ConfigEntry<float> PlayerChainDamageMultiplier;
    public readonly ConfigEntry<bool> DisableBenchHpRegen;
    public readonly ConfigEntry<bool> DisableBenchMpRegen;
    public readonly ConfigEntry<bool> VerboseLogging;
    public readonly ConfigEntry<bool> DumpBossRoster;
    public readonly ConfigEntry<float> ExperienceMultiplier;
    public readonly ConfigEntry<float> MoneyMultiplier;
    public readonly ConfigEntry<bool> AlwaysEscape;

    public readonly TierConfig Hard;
    public readonly TierConfig VeryHard;
    public readonly TierConfig Extreme;

    public ModConfig(ConfigFile cfg)
    {
        const string general = "01 - General";

        Difficulty = cfg.Bind(general, "Difficulty", Tier.Hard,
            "Off, Hard, VeryHard or Extreme. Off disables all enemy stat scaling.");

        NeutralizeLevelDiff = cfg.Bind(general, "Neutralize Level Difference", true,
            "The game softens damage when your level exceeds the enemy's, so out-levelling content trivialises it. " +
            "When true, that discount is clamped away and enemies stay dangerous even when you over-level.");

        MaxLevelAdvantagePercent = cfg.Bind(general, "Max Level Advantage Percent", 0f,
            "How much of a damage swing the level difference is still allowed to produce, in percent. "
            + "Vanilla gives 5% per level of difference with no cap, so being 2 levels ahead is +10% damage. "
            + "1.0 clamps that to at most 1% in either direction. 0 removes the level difference entirely. "
            + "Only applies when Neutralize Level Difference is true.");

        KeepUnderLevelPenalty = cfg.Bind(general, "Keep Under Level Penalty", true,
            "When true, the level clamp applies ONLY to your ADVANTAGE. Out-levelling content still stops "
            + "trivialising it, but being UNDER-levelled keeps its full vanilla penalty, so fighting above "
            + "your level is genuinely harder. When false the clamp is symmetric, which also deletes the "
            + "penalty - that made it possible to beat a boss two levels down with no drawback.");

        DamageRampStartLevel = cfg.Bind(general, "Damage Ramp Start Level", 11,
            "Enemies at or below this level all use Damage Ramp Low Multiplier. The ramp only begins above "
            + "it. Early enemies are already dangerous relative to a weak party, so the extra scaling is "
            + "only needed once the party's own damage has outgrown them.");

        DamageRampFullLevel = cfg.Bind(general, "Damage Ramp Full Level", 16,
            "Enemy level at which the tier's full Damage Multiplier applies. Between Damage Ramp Start Level "
            + "and this, the multiplier scales smoothly from the low value to the tier value.");

        HpRampHighFactor = cfg.Bind(general, "HP Ramp High Factor", 1.1f,
            "Extra HP applied to enemies at Damage Ramp Full Level and above, on top of the tier's MaxHP "
            + "roll. Uses the same start and full levels as the damage ramp. 1.0 disables it. "
            + "The party's damage outgrows enemy HP at higher levels - measured, a Lv10 Sweeper at 1879 "
            + "effective HP was dying in under two hits to ~1000-damage attacks - so late enemies need more "
            + "bulk while early ones, which were already fine, keep the tier value unchanged. "
            + "Kept small on purpose: it MULTIPLIES the tier's "
            + "MaxHP roll, so at 1.2 a VeryHard Lv18 enemy lands on x2.29 in total. The ramp is meant to "
            + "stop late enemies evaporating, not to double their health. "
            + "NOTE THE CAP: the factor stops growing at Damage Ramp Full Level and is FLAT above it, so a "
            + "Lv23 enemy gets exactly the same ramp as a Lv16 one. Raise Damage Ramp Full Level if you want "
            + "the curve to keep climbing into the late game.");

        EnableDamageHook = cfg.Bind(general, "Enable Damage Hook", true,
            "Master control for the final-damage multiplier and the single-hit cap below. "
            + "If the game ever crashes on launch, set this to false first - the hook is not installed at all when disabled.");

        PlayerWeaknessDamageMultiplier = cfg.Bind(general, "Player Weakness Damage Multiplier", 1.0f,
            "Scales the bonus damage YOUR characters get for hitting an enemy's elemental weakness. "
            + "1.0 is vanilla (no change). 0.5 halves the weakness bonus. Enemy weakness bonuses are untouched. "
            + "LEFT AT VANILLA on purpose - the mod carries its difficulty entirely in ENEMY STATS. "
            + "Nerfing the player's weakness damage was "
            + "never really a difficulty control: exploiting weakness is how the combat system intends you "
            + "to kill things, so cutting it multiplied every fight's LENGTH without making one enemy more "
            + "dangerous. Enemy HP and Defense were raised to compensate, which costs the same time but "
            + "leaves your best play feeling like your best play. "
            + "This does NOT affect Chain Point generation, which is awarded by an event rather than by damage dealt. "
            + "With Verbose Logging on, the real vanilla rate is printed so you can choose a value from data.");

        PlayerChainDamageMultiplier = cfg.Bind(general, "Player Chain Skill Damage Multiplier", 1.0f,
            "Scales the damage YOUR Chain Skills deal. 1.0 is vanilla, 0.5 halves them. "
            + "LEFT AT VANILLA alongside the weakness multiplier, for the same reason: reducing it "
            + "taxed fight length rather than danger. Chain Skills are the payoff for building Chain "
            + "Points, and a payoff that has been quietly reduced is just a longer fight. "
            + "Identified via UnitAttack.isChainAttack, so it affects chain skills only and not "
            + "ordinary attacks or normal skills. Enemy damage is untouched. "
            + "With Verbose Logging on, every chain hit prints its real damage value.");

        BossEnemyTypeId = cfg.Bind(general, "Boss Enemy Type Id", 1,
            "Enemy types at or above this value get boss handling. MEASURED from real battles, the values "
            + "are 0-BASED: Type=0 is every ordinary enemy (Goblin, Yaksha, Elite Goblin, Abbetos), Type=1 "
            + "covers Beast Vares and Beast King, and Type=2 covers the story bosses Stoneguard Abbetos and "
            + "Forest Guardian. So 1 catches everything that is not an ordinary enemy. "
            + "Verbose Logging prints [Type=?] and marks matches with BOSS. -1 disables boss handling.");

        BossHpScale = cfg.Bind(general, "Boss HP Scale", 1.0f,
            "Multiplies the HP bonus applied to BOSSES only, on top of the tier's MaxHP roll. "
            + "Bosses already have huge base HP (Stoneguard Abbetos has 3999 at Lv5), so the same multiplier "
            + "that makes a goblin interesting turns a boss into a war of attrition - which is punishing in a "
            + "demo with no items, because the fight outlasts your MP rather than testing your play. "
            + "Scales the BONUS only, so a boss can never drop below vanilla HP: a 1.78 roll at 0.5 becomes 1.39. 1.0 gives bosses the full tier multiplier, 0 gives them vanilla HP. "
            + "Tuned by playtest. At 0.8 a Lv23 boss reached 79175 effective HP from a 41375 base, which "
            + "is a half-hour fight rather than a hard one; below 0.7 bosses died too quickly once player "
            + "damage was left at vanilla. "
            + "THIS IS THE FIGHT-LENGTH DIAL: it adds turns without making any single enemy hit harder, "
            + "which is the opposite trade from Boss Damage Scale. "
            + "1.0 was CHOSEN FROM DATA, not picked round: it is the value at which the hand-tuned "
            + "Special Enemy Ids land back on their playtested difficulty (Forest Guardian +1%, "
            + "Beast Vares -2%, Galliard +2%). Specials halve whatever bonus reaches them, so a smaller "
            + "boss scale leaves them 5-8% softer than intended while plain bosses are already back at "
            + "parity. It also means bosses simply take the full tier multiplier with no discount.");

        BossDamageScale = cfg.Bind(general, "Boss Damage Scale", 0.9f,
            "Scales the tier's enemy Damage Multiplier for BOSSES only. Boss damage compounds twice - once "
            + "through the boosted attack stat and again through the final damage multiplier - and in a demo "
            + "with no items, high boss damage forces the party to spend its turns healing instead of "
            + "attacking, which lengthens the fight and demands yet more healing. 1.0 gives bosses the full "
            + "tier damage multiplier. "
            + "Held slightly below 1.0 on purpose. Bosses land on a softer "
            + "multiplier than ordinary enemies (MEASURED at Lv23: bosses 427-1265 at x1.45 against "
            + "ordinary enemies 406-651 at x1.70), which looks backwards but is not a mistake: high boss "
            + "damage lengthens a fight the WRONG way, by forcing the party to spend turns healing "
            + "instead of attacking. Fight length belongs to Boss HP Scale; this dial is danger only.");

        SpecialEnemyIds = cfg.Bind(general, "Special Enemy Ids",
            "310200202909:0.6:0.8:1:1,"          // Forest Guardian
            + "310900211910:0.5:0.7,"            // Beast Vares  Lv10
            + "310900211911:0.5:0.7,"            // Beast Vares  Lv11
            + "310900211912:0.5:0.7,"            // Beast Vares  Lv12
            + "310900311918:0.5:0.75,"           // Galliard     Lv18
            + "310900311919:0.5:0.75,"           // Galliard     Lv19
            + "310900311920:0.5:0.75,"           // Galliard     Lv20
            + "310900311921:0.5:0.75",           // Galliard     Lv21
            "Comma-separated enemy ids that get extra scaling ON TOP of normal boss scaling. Each entry is "
            + "either a bare id, which uses the shared scales below, or \"id:hpScale:damageScale\" for one "
            + "encounter needing its own values. Optional 4th and 5th fields pin DEFENCE and ATTACK "
            + "separately: id:hp:damage:def:atk:hitcap%. Fields may be left empty, e.g. 12345:0.5:0.7:::50 caps "
            + "that one enemy's hits at 50% of the target's max HP. Omitted, defence follows the hp scale and attack follows "
            + "the damage scale. Example: 310900211910:0:0.75 is vanilla bulk with 75% of the offence bonus. "
            + "For fights that are unusually punishing for reasons the mod cannot see - for example story "
            + "battles where healing is unavailable. Ids are unique per enemy AND level (Goblin Lv1 is "
            + "310200101901, Lv2 is 310200101902), so this targets one exact encounter and never a similar "
            + "enemy. Defaults are Stoneguard Abbetos (310200203905) and Forest Guardian (310200202909). "
            + "Leave empty to disable. Verbose Logging prints each enemy's id.");

        SpecialEnemyHpScale = cfg.Bind(general, "Special Enemy HP Scale", 0.6f,
            "HP bonus scale for the ids listed above, applied after the boss scale. Scales the BONUS only, "
            + "so these enemies can never drop below vanilla HP.");

        SpecialEnemyDamageScale = cfg.Bind(general, "Special Enemy Damage Scale", 0.8f,
            "Damage multiplier scale for the ids listed above, applied after the boss damage scale.");

        DumpBossRoster = cfg.Bind(general, "Dump Boss Roster", false,
            "Logs EVERY boss-type enemy in the game once at startup - id, name, level, type and base HP - "
            + "by reading the master data repository. Use it to find multi-phase bosses: a boss that "
            + "escalates appears as several ids at consecutive levels, and each level needs its own entry in "
            + "Special Enemy Ids. Galliard was found this way, spanning Lv18 to Lv21. Turn off afterwards.");

        VerboseLogging = cfg.Bind(general, "Verbose Logging", false,
            "Log every enemy's rolled multipliers to the BepInEx console. Useful for tuning, noisy in normal play.");

        const string rewards = "06 - Rewards";

        ExperienceMultiplier = cfg.Bind(rewards, "Experience Multiplier", 1.0f,
            "Multiplies the experience every enemy awards. 1.0 is VANILLA and is the default - this is a "
            + "difficulty mod, so it does not hand out extra rewards unless you ask it to. 1.5 is a good "
            + "starting point if you want it. "
            + "Harder enemies take longer to kill, so vanilla reward rates turn a difficulty mod into a "
            + "grind mod - you spend the extra time fighting the same battles rather than seeing new ones. "
            + "This is a reward dial, not a difficulty one, so it is deliberately separate from the tiers "
            + "and applies the same on Hard, VeryHard and Extreme. "
            + "Applied at DomainEnemy.Experience, before the game's own level-difference correction, so "
            + "over-levelled enemies still award proportionally less.");

        MoneyMultiplier = cfg.Bind(rewards, "Money Multiplier", 1.0f,
            "Multiplies the money every enemy awards. 1.0 is VANILLA and is the default, for the same "
            + "reason as the experience multiplier above. Covers both the Gold field and the "
            + "DropCurrency field, because the demo's enemies use them inconsistently and only one of the "
            + "two is populated per enemy. Shop and equipment prices are NOT touched - those live on "
            + "separate master data - so this raises income without also raising costs.");

        const string convenience = "07 - Convenience";

        AlwaysEscape = cfg.Bind(convenience, "Always Escape", true,
            "Verified working in playtesting. Hooks BattleEscapeEvent.CanExecute, ordinary battle logic "
            + "that nothing outside a live escape attempt touches. "
            + "Makes fleeing a battle always succeed. A failed escape costs the whole party a turn AND "
            + "leaves you in the fight you were trying to leave, which in a mod where enemies hit harder "
            + "is a punishment for a decision you already made rather than a decision of its own. "
            + "This does NOT let you flee battles the game forbids fleeing: story and boss fights hide "
            + "the Escape command entirely, which is a separate gate this never touches. It only removes "
            + "the dice roll from escapes that were already allowed. "
            + "*** IF THE GAME EVER CRASHES ON LAUNCH, SET THIS TO FALSE FIRST *** - the hook is not "
            + "installed at all when this is false.");

        const string systems = "02 - Systems";

        DisableBenchHpRegen = cfg.Bind(systems, "Disable Reserve Party HP Regen", false,
            "*** KNOWN TO CRASH THE GAME ON LAUNCH - LEAVE THIS FALSE *** " +
            "Vanilla passively heals your benched (Another Party) members every turn of a battle, which lets " +
            "Valor Change act as a free full-heal rotation. Setting this true installs a hook on FlatBuffers-" +
            "generated master data that crashes during startup. Kept only so the feature can be finished later.");

        DisableBenchMpRegen = cfg.Bind(systems, "Disable Reserve Party MP Regen", false,
            "*** KNOWN TO CRASH THE GAME ON LAUNCH - LEAVE THIS FALSE *** As above but for MP.");

        // Three tiers, each a self-contained set of multipliers. This game has a single
        // unified defense stat, so one Defense row covers physical and magic alike.
        Hard = new TierConfig(cfg, "03 - Tier - Hard",
            1.24f, 1.38f,   // +24~38% HP - sized so fights stay brisk with player damage left at vanilla
            1.15f, 1.25f,   // +15~25% ATK - UNCHANGED throughout; enemy damage was never the complaint
            1.08f, 1.16f,   // +8~16% DEF - DEF lengthens a fight exactly as HP does, so it moves with HP
            1.00f, 1.00f,   // vanilla speed - extra enemy turns are unanswerable without items
            1.00f, 1.00f,   // vanilla dodge - enemy evades burn the player's scarce turns
            1.60f,          // damage ceiling at Lv16+
            1.45f,          // damage floor at Lv11 - gives Hard a real ramp; its ceiling is only 1.60
            0f);            // cap OFF by default - it is set PER-ENEMY via the 6th special field

        VeryHard = new TierConfig(cfg, "04 - Tier - VeryHard",
            1.46f, 1.65f,   // +46~65% HP - higher values plus the ramp pushed a Lv18 enemy to x2.29
                            // in total, which made a damage sponge rather than a threat.
            1.35f, 1.50f,   // +35~50% ATK - UNCHANGED
            1.18f, 1.30f,   // +18~30% DEF
            1.00f, 1.00f,   // vanilla speed: MEASURED a 1.04 bump flipping a 31-vs-31 tie
            1.00f, 1.05f,   // near-vanilla dodge
            1.70f,          // damage ceiling at Lv16+
            1.60f,          // damage floor at Lv11
            0f);            // cap OFF by default

        Extreme = new TierConfig(cfg, "05 - Tier - Extreme",
            1.70f, 1.90f,   // +70~90% HP - Extreme keeps its lead over VeryHard through DAMAGE and
                            // speed, not through bulk, so its HP stays close to VeryHard's.
            1.60f, 1.85f,   // +60~85% ATK - UNCHANGED
            1.28f, 1.40f,   // +28~40% DEF
            1.03f, 1.05f,   // small speed edge - the ONLY tier that still raises agility
            1.00f, 1.05f,   // dodge trimmed: enemy evades burn the player's scarce turns
            2.10f,          // damage ceiling at Lv16+ - the real step up from VeryHard's 1.70
            1.60f,          // damage floor at Lv11
            0f);            // cap OFF by default
    }

    /// <summary>
    /// Difficulty acts as the mod's master switch. Setting it to Off restores a fully
    /// vanilla game, including the system changes below, rather than only disabling
    /// enemy stat scaling. The per-feature toggles still apply on every other tier.
    /// </summary>
    public bool ModActive => Difficulty.Value != Tier.Off;

    public TierConfig Active => Difficulty.Value switch
    {
        Tier.Hard => Hard,
        Tier.VeryHard => VeryHard,
        Tier.Extreme => Extreme,
        _ => null,
    };
}
