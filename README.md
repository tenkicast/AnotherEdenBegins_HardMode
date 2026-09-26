# Another Eden Begins — Hard Mode

A runtime difficulty mod for *Another Eden Begins*. Enemies get more health, more defence, and hit harder across three difficulty tiers.

No game file is ever modified. The mod works entirely through in-memory hooks — deleting the plugin restores the vanilla game exactly, and saves are never touched.

> ## ⚠ Before you install
>
> **The BepInEx version matters more than anything else here.** You need a bleeding-edge BepInEx 6 build — the stable release crashes this game on startup. See [Installing](#installing-users) below; this is the single most common cause of "the mod doesn't work".
>
> **If your system uses a comma as the decimal separator, update to 1.0.2.** Versions before it read the per-encounter rules using your regional settings, so `0.5` was taken as `5`. On the affected encounters that produced wildly inflated stats — one boss measured at ATK ×16 with a ×108 damage multiplier. Fixed; no config change needed.
>
> Balance is tuned by hand and is a matter of taste. If a fight feels wrong, it probably is — please say so rather than assuming it's intended.
>
> Offered with no warranty of any kind — see the [LICENSE](LICENSE). It modifies no game files and touches no saves, so the worst realistic outcome is that the game fails to start.

---

### Download

**[Download from Nexus Mods](https://www.nexusmods.com/anotheredenbegins/mods/1)** — releases are published there, not here. This repository holds the source only.

## Features

- **Three tiers** — Hard, VeryHard, Extreme, plus Off. Ships on Hard.
- **Per-enemy scaling** — HP, attack, defence and final damage. Ordinary enemies roll their values from a hash of their enemy ID, so a given enemy is always identical across encounters, reloads and saves.
- **Separate boss tuning** — bosses take the midpoint of each range rather than a roll, so a boss fight is never decided by luck.
- **Level-aware ramps** — scaling grows, so early enemies aren't punished with endgame numbers.
- **Level-difference neutralisation** — out-levelling content stops trivialising it, while the penalty for being *under*-levelled is kept.
- **Per-encounter overrides** — eight hand-tuned story fights, targeted by exact enemy ID.
- **Guaranteed escape** — fleeing always succeeds where the game permits fleeing at all.
- **Optional EXP and money multipliers** — off by default.
- **Shop overrides** — name any item by id to make it free, or to stop it ever running out of stock. Off by default.

Player damage is untouched: no changes to weakness bonuses, Chain Skills, crit, or your stats. The difficulty comes entirely from the enemies.

---

## Installing (users)

**You need BepInEx 6 (IL2CPP, x64), bleeding-edge build `6.0.0-be.788` or newer.**

Stable BepInEx releases only support IL2CPP metadata v23–31. This game is Unity 6000.3.4f1 with **metadata v39**, so a stable build crashes on startup right after `Chainloader initialized`. This is the single most common cause of "the mod doesn't work".

1. Install BepInEx 6 bleeding edge into the game folder.
2. Launch once and let it reach the title screen. The first run sits on a **black screen for several minutes** while BepInEx generates interop assemblies. This is normal.
3. Drop `AnotherEdenHardMode.dll` into `BepInEx/plugins/`.
4. Launch. The config is generated at `BepInEx/config/tenkicast_anotheredenbegins_hardmode.cfg`.

Full user documentation is in [`README.txt`](README.txt) in the release archive.

---

## Configuration

Everything is editable in `BepInEx/config/tenkicast_anotheredenbegins_hardmode.cfg`, and every setting carries an explanatory comment. The one most people want:

```ini
Difficulty = Hard     # Off | Hard | VeryHard | Extreme
```

Spelling matters — `VeryHard` is one word, capital H.

Setting `Difficulty = Off` disables the entire mod without uninstalling it.

### Shop overrides

`[08 - Shop]` can make chosen items free, or give them unlimited stock:

```ini
Dump Shop Contents       = true      # find the ids, then turn this back off
Free Item Ids            = 241911050
Unlimited Stock Item Ids = 241911050
```

Items are targeted by **id**, because the game gives them no category of their own — an ability-reset potion is an ordinary consumable. Turn on the dump, open the shop, and the log prints a row per item; match yours by the price shown on screen. Anything not listed is untouched.

All numbers in the config are read with the invariant culture, so **always write decimals with a dot**, whatever your system uses.

---

## Building from source

Requires the **.NET SDK 6.0 or newer** (the plugin targets `net6.0`, which is what BepInEx 6 bundles).

The build references assemblies from an installed, BepInEx-initialised copy of the game. **None of them are redistributable, so none are in this repository** — you need your own install, launched at least once with BepInEx so that `BepInEx/interop/` exists.

```bash
git clone https://github.com/<you>/AnotherEdenBegins_HardMode.git
cd AnotherEdenBegins_HardMode
dotnet build -c Release
```

If the game isn't at the default Steam path, point the build at it:

```bash
dotnet build -c Release -p:GameDir="D:\Games\Another Eden Begins"
```

A wrong path fails fast with a message telling you exactly what to set. Output lands in `bin/Release/net6.0/AnotherEdenHardMode.dll`.

---

## Project layout

| File | Purpose |
|---|---|
| `Plugin.cs` | Entry point. Installs patch classes **conditionally**, so any feature can be disabled without its hook ever loading. |
| `Config.cs` | Every setting, its default and its documentation. `TierConfig` per tier, `ModConfig` global. |
| `Patches.cs` | All Harmony patches — 20 hooks across 9 classes. |
| `Tuning.cs` | Tier enum, stat ranges, the SplitMix64 seeded roll, the per-encounter rule parser, and the pointer-keyed scaling registry. |

### How it works

Enemy stats are scaled by post-fixing the `Raw*` getters on `EnemyUnitStatus` — never the public ones, so in-battle buffs apply on top of a boosted base rather than being multiplied themselves. Multipliers are computed once in a prefix on `EnemyUnitStatus.Setup` and stored in a registry keyed by IL2CPP object pointer.

Two rules were learned the hard way and are worth knowing before adding hooks:

- **Never patch FlatSharp-generated master data.** Types carrying a nested `GeneratedSerializer` are deserialised natively during startup; patching one crashes the game with nothing in the log.
- **Being a plain POCO is not sufficient.** *When* a getter runs matters as much as what type owns it — a master-data getter read during construction is just as fatal. Hook the battle logic that *consumes* the data instead.

---

## Feedback

Balance was tuned by hand against real fights, by a small number of people arguing about numbers. That's a small sample, so reports are genuinely useful. Please open an issue with:

- which tier you're on
- which fight, and roughly your level
- whether the problem was that the fight was too **long** or too **dangerous** — those are separate dials, and knowing which saves a lot of guessing

Attaching a log helps enormously. Set `Verbose Logging = true` and grab `BepInEx/LogOutput.log`; it records every enemy's rolled stats and every hit on both sides.

**Extreme is the least tested tier.** It's built to be a genuine step above VeryHard rather than a joke, but it has had far fewer eyes on it than Hard and VeryHard — feedback on it is especially welcome.

---

## Support

Free, and always will be. If you'd like to buy me a coffee:

[![Support me on Ko-fi](https://storage.ko-fi.com/cdn/brandasset/v2/support_me_on_kofi_dark.png)](https://ko-fi.com/tenkicast)

Nothing is gated behind it, and nothing nags you in-game.

---

## License

Released under the [MIT License](LICENSE).

## Credits

Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX).

Credit to Kurein for Playtesting and Bug Fixes.

Not affiliated with or endorsed by the developers or publishers of *Another Eden Begins*.
