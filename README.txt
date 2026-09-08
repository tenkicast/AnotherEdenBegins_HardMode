===============================================================================
  ANOTHER EDEN BEGINS - HARD MODE
  Version 1.0.0
===============================================================================

The demo is generous. This makes it fight back.

Enemies get more health, more defence and hit harder, on three difficulty tiers.
Bosses are tuned separately from ordinary enemies, and a handful of specific
story fights are hand-tuned on top of that. Optional experience and money
boosts are included if you would rather not grind for the extra difficulty.

No game file is ever modified. Nothing touches your save data.


-------------------------------------------------------------------------------
  WHERE TO DOWNLOAD
-------------------------------------------------------------------------------
Releases are published on Nexus Mods. The GitHub repository holds the source
code only - there are no downloads there.

-------------------------------------------------------------------------------
  DISCLAIMER - DEMO ONLY, UNTESTED ON THE FULL GAME
-------------------------------------------------------------------------------
This mod is built and tested against the DEMO of Another Eden Begins, and only
the demo. It has NOT been tested on the full release and should not be assumed
to work there.

It reads the game's own code, its enemy data and its exact enemy IDs. All three
change between builds, so on the full game this will at best do nothing useful
and at worst behave unpredictably - the hand-tuned boss rules in particular are
keyed to specific demo enemy IDs that may belong to entirely different enemies.

When the full game releases it will need re-testing and re-tuning from scratch.
Please wait for an updated version rather than forcing this one.

Use at your own risk. It modifies no game files and touches no saves, so the
worst realistic outcome is that the game fails to start - but it is offered
with no warranty of any kind. See the LICENSE.


-------------------------------------------------------------------------------
  REQUIREMENTS - THE BEPINEX VERSION MATTERS
-------------------------------------------------------------------------------
You need BepInEx 6 (IL2CPP, x64), BLEEDING EDGE build 6.0.0-be.788 or newer.

  *** DO NOT DOWNLOAD THE "LATEST STABLE" BEPINEX. ***

Stable BepInEx releases only support IL2CPP metadata versions 23 to 31. This
game is Unity 6000.3.4f1 and uses metadata v39, so a stable build will crash on
startup, usually right after "Chainloader initialized" in the log.

This is by far the most common reason the mod appears not to work. If the game
will not start, check this first.

Get a bleeding-edge build from the BepInEx builds page and install it into the
game folder as normal.


-------------------------------------------------------------------------------
  INSTALLING
-------------------------------------------------------------------------------
1. Install BepInEx 6 bleeding edge (see above).

2. Launch the game once, let it reach the title screen, then quit. The first
   launch after installing BepInEx sits on a BLACK SCREEN for several minutes
   while it generates files from the game. This is normal. Do not close it.

3. Drop AnotherEdenHardMode.dll into:

       ...\Another Eden Begins Demo\BepInEx\plugins\

4. Launch. On this first run the mod writes its settings file to:

       ...\BepInEx\config\tenkicast_anotheredenbegins_hardmode.cfg

That is it. The mod starts on HARD, which is the intended starting point.


-------------------------------------------------------------------------------
  DIFFICULTY TIERS
-------------------------------------------------------------------------------
  HARD        The intended experience. Enemies have noticeably more health and
              hit meaningfully harder, but fights stay brisk. Playtested to the
              end of the demo.

  VERYHARD    For people who found Hard comfortable. Roughly half again as much
              enemy health as vanilla, with a real jump in damage. Also
              playtested to the end of the demo.

  EXTREME     Enemies are faster, tougher and hit far harder. This tier has NOT
              been playtested by anyone. It is built to be a genuine step above
              VeryHard rather than a joke, but you are the first. Feedback on it
              is very welcome.

  OFF         Fully vanilla. Disables the entire mod without uninstalling it.


-------------------------------------------------------------------------------
  CHANGING DIFFICULTY
-------------------------------------------------------------------------------
Quit the game, open the settings file:

    ...\BepInEx\config\tenkicast_anotheredenbegins_hardmode.cfg

and edit the single line near the top:

    Difficulty = Hard

to Off, Hard, VeryHard or Extreme. Save, relaunch.

Spelling matters. "VeryHard" is one word, capital H, no space.

Every other number in that file is adjustable too, and each one carries a
comment explaining what it does. Nothing in there can break your save.


-------------------------------------------------------------------------------
  WHAT IT ACTUALLY CHANGES
-------------------------------------------------------------------------------
  * Enemy max HP, attack, defence, and the final damage they deal.
    Each ordinary enemy rolls its own values from its ID, so a given enemy is
    always the same - consistent across encounters, reloads and saves.

  * Bosses are scaled separately, and never randomly. Boss fights are decided
    by play, not by a lucky roll.

  * Scaling grows with enemy level, so early enemies are not punished with
    numbers meant for late ones.

  * Out-levelling content no longer trivialises it. The vanilla damage bonus for
    being above an enemy's level is removed - but the penalty for being UNDER
    its level is kept, so fighting above your weight is still genuinely harder.

  * Fleeing a battle always succeeds. A failed escape costs your whole party a
    turn AND leaves you in the fight you were trying to leave. Battles the game
    does not let you flee at all are unaffected.

  * A few specific story fights are individually tuned, because they are harder
    for reasons raw numbers cannot see - for example, fights where healing is
    not available to you.

WHAT IT DOES NOT CHANGE
    Your damage. Critical hits. Elemental weakness bonuses. Chain Skills. Shop
    prices. Encounter rates. Experience or money, unless you turn that on
    yourself. The difficulty comes entirely from the enemies.


-------------------------------------------------------------------------------
  OPTIONAL - EXPERIENCE AND MONEY
-------------------------------------------------------------------------------
Tougher enemies take longer to kill, which can quietly turn a difficulty mod
into a grind mod. If that happens to you, there are two dials under
[06 - Rewards] in the settings file:

    Experience Multiplier = 1
    Money Multiplier = 1

Both are 1 (vanilla) BY DEFAULT and do nothing until you change them - this is a
difficulty mod, not a cheat, so it does not hand out extra rewards uninvited.
Set them to 1.5 for half again as much. The hook is not even loaded while they
are at 1.

Experience is boosted BEFORE the game's own level correction, so farming enemies
far below your level stays as unrewarding as the game intends. Shop and
equipment prices are never touched, so your income rises without your costs
rising to match.


-------------------------------------------------------------------------------
  IF SOMETHING GOES WRONG
-------------------------------------------------------------------------------
GAME WILL NOT START
    Almost always the BepInEx version. See REQUIREMENTS above.

    If you are sure BepInEx is right, open the settings file and set:
        Enable Damage Hook = false
    then try again. If it starts, please report it - that tells me exactly which
    part is at fault.

SOMETHING FEELS WRONG BUT THE GAME RUNS
    Set  Difficulty = Off  to confirm whether the mod is involved at all.

TO REMOVE IT COMPLETELY
    Delete AnotherEdenHardMode.dll from BepInEx\plugins\.
    Your save is untouched and the game returns to vanilla immediately.

TWO SETTINGS TO LEAVE ALONE
    Disable Reserve Party HP Regen = false
    Disable Reserve Party MP Regen = false
    These two crash the game. They are off by default and carry warnings in the
    settings file itself. They are kept only so the feature can be finished
    later.


-------------------------------------------------------------------------------
  FEEDBACK
-------------------------------------------------------------------------------
This was tuned by two people playing through the demo and arguing about the
numbers. That is a small sample, and balance is personal - so if something feels
wrong, please say so.

The most useful reports include:
  * which tier you are on
  * which fight, and roughly what level you were
  * whether the problem was that a fight was too LONG or too DANGEROUS
    (those are separate dials, and knowing which one saves a lot of guessing)

If you want to include a log, turn on:
    Verbose Logging = true
in the settings file. It records every enemy's stats and every hit, and it is
the single most useful thing you can attach. The log lives at:
    ...\BepInEx\LogOutput.log

Post in the comments, or open a bug report on the Nexus page.

The mod is open source under the MIT licence - read exactly what it does, or
open an issue, at:
    https://github.com/tenkicast/AnotherEdenBegins_HardMode

EXTREME especially needs eyes on it. Nobody has finished the demo on that tier.


-------------------------------------------------------------------------------
  SUPPORT THE MOD
-------------------------------------------------------------------------------
This is free and always will be. If you enjoyed it and want to throw a coffee my
way, it is genuinely appreciated:

    https://ko-fi.com/tenkicast

No part of the mod is gated behind it, and nothing nags you in-game.


-------------------------------------------------------------------------------
  CREDITS AND SAFETY
-------------------------------------------------------------------------------
Built with BepInEx and HarmonyX.

The mod works entirely through in-memory hooks. It does not write to your game
files, your asset bundles, your master data or your saves. Deleting the plugin
restores the vanilla game exactly as it was.
