===============================================================================
  ANOTHER EDEN BEGINS - HARD MODE
  Version 1.0.2
===============================================================================

Another Eden Begins is generous. This makes it fight back.

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
  BEFORE YOU INSTALL
-------------------------------------------------------------------------------
The BepInEx version matters more than anything else here. You need a BLEEDING
EDGE BepInEx 6 build - the stable release crashes this game on startup. See
REQUIREMENTS below. This is by far the most common cause of "it doesn't work".

IF YOUR SYSTEM USES A COMMA AS THE DECIMAL SEPARATOR, USE 1.0.2 OR NEWER.
Earlier versions read the per-encounter rules using your regional settings, so
"0.5" was taken as 5. On the affected encounters that produced wildly inflated
stats - one boss measured at ATK x16 with a x108 damage multiplier. This is
fixed in 1.0.2 and needs no config change.

Always write decimals with a DOT in the settings file, whatever your system
uses. Every number is now read the same way on every machine.

Balance is tuned by hand and is a matter of taste. If a fight feels wrong, it
probably is - please say so rather than assuming it is intended.

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

       ...\Another Eden Begins\BepInEx\plugins\

4. Launch. On this first run the mod writes its settings file to:

       ...\BepInEx\config\tenkicast_anotheredenbegins_hardmode.cfg

That is it. The mod starts on HARD, which is the intended starting point.


-------------------------------------------------------------------------------
  DIFFICULTY TIERS
-------------------------------------------------------------------------------
  HARD        The intended experience. Enemies have noticeably more health and
              hit meaningfully harder, but fights stay brisk. This is the most
              played tier.

  VERYHARD    For people who found Hard comfortable. Roughly half again as much
              enemy health as vanilla, with a real jump in damage.

  EXTREME     Enemies are faster, tougher and hit far harder. This is the LEAST
              TESTED tier by some distance. It is built to be a genuine step
              above VeryHard rather than a joke, but it has had far fewer eyes
              on it. Feedback on it is especially welcome.

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
  OPTIONAL - FREE OR UNLIMITED SHOP ITEMS
-------------------------------------------------------------------------------
Under [08 - Shop] you can make chosen items cost nothing, or stop them running
out of stock. Everything here is OFF by default and an item you do not list is
left exactly as the game has it.

Items are targeted by ID, because the game gives them no category of their own -
an ability-reset potion is an ordinary consumable like any herb. To find an id:

1. Set   Dump Shop Contents = true
2. Launch, open the shop, quit.
3. The log has one line per item, for example:
       SHOP shopId=204018831 itemId=241911050 price=5 stock=10 bought=2 left=8
   Match yours by the PRICE the shop showed you on screen.
4. Put the id into either or both settings, then set the dump back to false:
       Free Item Ids            = 241911050
       Unlimited Stock Item Ids = 241911050

The two lists are independent - an item can be free without being unlimited, or
unlimited without being free. Several ids are separated by commas.


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
This was tuned by hand against real fights, by a small number of people arguing
about the numbers. That is a small sample, and balance is personal - so if
something feels wrong, please say so.

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

EXTREME especially needs eyes on it - it is the least tested tier by far.


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

Credit to Kurein for Playtesting and Bug Fixes.

The mod works entirely through in-memory hooks. It does not write to your game
files, your asset bundles, your master data or your saves. Deleting the plugin
restores the vanilla game exactly as it was.
