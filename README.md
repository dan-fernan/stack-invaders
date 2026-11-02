# Stack Invaders

A programming-themed, pixel-art Space Invaders built in Unity 6 (6000.6, 2D URP) for
**CSCI 381 – Video Game Design and Development, Assignment 1** (Fall 2026).
The player is a developer's ship shooting down a marching formation of literal bugs.

This project was **started from scratch** rather than built on top of the in-class
[Space-Invaders-3D](https://github.com/Oren-Steinberg/Space-Invaders-3D) project, as the assignment
allows. Per the assignment, the code-issues requirement is met by identifying issues in the
instructor's code and documenting how this project handles each one instead (see
[Code issues in the instructor's project](#code-issues-in-the-instructors-project)).

## How to run

1. Open the project folder in Unity **6000.6.0f1** (or a newer Unity 6 version).
2. Open `Assets/Scenes/SampleScene.unity` and press Play.

| Key | Action |
| --- | --- |
| A / D (or ← / →) | Move |
| Space | On the start screen: skip the typing, then pick the highlighted language |
| W / S (or ↑ / ↓), 1–3 | Choose a language on the start screen |
| Space (hold) | Attack with your language's weapon (Java: throw coffee) |
| Shift | Breakpoint parry: catch incoming bits and send them back |
| R | Restart after a win or game over |
| F10 (Editor / development builds only) | Debug: clear the current stage (a wave, or skip a boss) |

## Required features

| Requirement | Status | How it's met | Where |
| --- | --- | --- | --- |
| Remaining aliens speed up as others are destroyed | ✅ | Each formation marches in discrete steps and speeds up as *its own* bugs die. Time between steps is interpolated from 0.45 s (full formation) to 0.04 s (last bug) using the *square* of the fraction remaining, so the speed-up ramps hardest near the end, like the arcade original. | `AlienFormation.CurrentStepInterval()` |
| Detect when the game can no longer be won | ✅ | After every march step of any formation, `GameManager` compares the bottom edge of the **lowest living** bug in *any* active formation to the top of the developer. Once they meet, that bug can no longer be shot before it reaches the player, so the game ends immediately, regardless of lives left. | `GameManager.CheckInvasion()`, `WaveManager.LowestY`, `AlienFormation.LowestY` |
| Prevent the spaceship from moving offscreen | ✅ | The ship's x is clamped to the camera's visible half-width minus the sprite's half-width (from `SpriteRenderer.bounds`), so the whole ship stays visible at any resolution or aspect ratio. | `PlayerController.ClampToScreen()` |
| Find and fix at least three issues in the instructor's code | ✅ | Seven issues documented below, each with the fix used in this project. | [Below](#code-issues-in-the-instructors-project) |

## Optional features (four required)

| Feature | Status | Notes |
| --- | --- | --- |
| Better graphics for aliens and ship | ✅ | Original pixel art. **The developer** (16×20, `PlayerAnimator`): red headphones, green hoodie and glasses, with a breathing idle seen from behind, a 4-frame sideways run (mirrored for left, driven by actual movement, so being pushed by a wall doesn't play the run), and a wind-up / release / follow-through throw where the hand really lets go of the mug. **Java's coffee** is a mug that tumbles through 2 frames, sloshing coffee. Also three bug types (beetle, ladybug, spider) with two-frame walk animations, bullets, and hearts. 16 pixels per unit, point filtering, and a Pixel Perfect Camera at 320×180. |
| Lives with a display | ✅ | Three pixel-art hearts in the top-left (`PlayerHealth`, `HeartsDisplay`). The fleet's bit attack (below) removes one heart per hit, followed by 1 s of invulnerability while the ship blinks. Running out of hearts ends the game. |
| Start and ending screens | ✅ | Terminal-style screens, all in `GameManager`. **Start:** the game loads frozen behind a terminal that types out `Compiling...` and `Build succeeded with 175 warnings (bugs) and 2 errors (bosses)`; Space skips the typing. The log then collapses to one line, and a language select menu appears (W/S or 1–3 to choose, Space to run). It describes the highlighted language: its weapon, passive and ability (for Java: `coffee cup: splash hits 3 bugs`, `JIT: hold fire to throw faster`, `GC: auto-frees bits near you`), plus the breakpoint parry every language shares. **Between waves:** a 2 s terminal break (`$ git pull origin feature/more-bugs …`). **Win** (after the final boss): `BUILD PASSING … recurse(3) returned: call stack unwound … Process exited with code 0`, with the hearts left. **Game over:** a stack trace naming the cause of death (`OutOfHeartsException` or `BugsInProductionException`), then `Process exited with code 1`. R restarts from either end screen. |
| Animations when collisions occur | ✅ | Pixel-art flipbook effects (`OneShotAnimation`), tinted to the color of whatever was hit. **Bug shot:** the bug flattens, bursts into a splat, then scatters into pixels (`BugSquash`). **Bit shot, or bit hits the ship:** a small pixel burst (`BitPop`). **Ship hit:** a 0.2 s camera shake in whole game pixels, so the image stays on the pixel grid (`CameraShake`), plus the invulnerability blink. **Bug rebuilt:** a pop in the bug's color when its bit returns. |
| Additional levels | ✅ | Three waves, each different from the last (`WaveManager`, configured in the Inspector). **Wave 1:** the classic 5×11 formation sweeping the full width. **Wave 2:** two 4×6 formations, back-left and back-right, each bouncing between its screen edge and the midpoint, so they drop about twice as often. **Wave 3:** a 4×9 middle formation in front, with 3×6 back-left and back-right formations behind it. The back formations won't drop onto the middle one until it's cleared, so the player chooses which wave to thin out. Every formation has its own throw manager and speed-up. Hearts carry over between stages; JIT heat and the GC timer reset. The full run is **Wave 1 → Wave 2 → binary search miniboss → Wave 3 → recursion final boss** (see below). |

### Fleet attack: bugs thrown as bits

Bugs are code that doesn't do what it's supposed to, so the fleet's attack is a bug being
recompiled into raw bits and thrown at the developer.

1. **Pickup:** 1.5–2.5 s after the previous throw ends (dropping to 0.5–1 s as the fleet shrinks), a bug with a
   living neighbor is picked. The neighbor flashes white and the bug wobbles for 0.5 s as a warning.
2. **Throw:** the bug splits into a **0** and a **1** in its own color, launched 35° to either
   side of a straight line to the ship.
3. **Homing:** the bits steer toward the ship with a limited turn rate while they're above it.
   Once they pass the ship's row they stop steering, so a late sidestep dodges them.
4. **Return:** bits that leave the screen re-enter from the top and fly back to their bug's slot,
   which keeps marching with the formation. While returning they can't hurt the player.
5. **Resolution:** a bit that hits the ship removes a heart and is used up. When both bits are
   resolved, the bug **rebuilds if at least one bit made it home**. Otherwise (for example, both
   were shot) the bug dies.

Each formation has its own `FleetThrower`, with only one of its throws in the air at a time, so wave 3 can have three throws airborne at once. Back formations throw less often (an interval multiplier per formation). Implementation: `FleetThrower` (timing, picking a bug,
deciding the outcome), `BitProjectile` (homing, returning, hits), `AlienFormation.TryPickThrow()`
(finding a neighbor on the grid), and `Alien.SetAway()`, `Flash()` and `Wobble()`. Player
bullets hit anything that implements `IShootable`, so they handle bugs and bits the same way.

### Breakpoint parry

Every language gets the same native parry: the developer sets a breakpoint.

1. **Shift** opens a 0.2 s window, and a red ● breakpoint dot appears beside the developer.
2. Any enemy projectile (fleet bit or boss split shot) within 0.9 units, or touching the developer, during the window is **paused**: the
   game freezes for 0.06 s and `● paused: 0` pops up.
3. **Step Into:** the caught bit turns developer-green, flies back to its slot and **patches**
   the bug it came from, which is squashed in its slot. A patched bit can't hurt you or be shot.
   A parried boss split shot flies back up and damages the first boss frame it touches.
4. A successful window has no cooldown, so you can catch the 0 and then the 1. A window that
   catches nothing detaches the debugger for 1 s, shown by a gray dot.

This rewards timing over constant movement, and turns the fleet's attack into offense without
making any weapon stronger. Implementation: `BreakpointParry`, `BitProjectile.StepInto()`,
`Alien.Patch()`, and the pop-up text in `FloatingText`. The spec's *Breakpoint* powerup (freeze
all enemies) will need a new name.

### Miniboss: binary search

Between waves 2 and 3. A bug hides in one cell of a 16-cell sorted array, and every cell shows `?`.

- **Each shot is a comparison.** Hit a wrong cell and it shows `>` or `<`, pointing toward the
  bug. Every cell on the wrong side is **eliminated**: grayed out, and shots pass through it,
  just as a binary search discards half its range. Hit the bug's cell and it's **found**. For about
  1.5 s everything else dims and stops firing, the found cell pops to 2× size and blinks red with
  the bug inside, and `found arr[12]!  -27` pops up. Then the bug ducks back in and the array
  re-sorts.
- **Efficient searches hurt more.** The boss has 160 HP. A find deals 15 damage plus 6 for
  every comparison left in the budget (6 per search; an optimal search of 16 cells needs at most
  5). A 4-comparison find deals 27, a 6-comparison find only 15, and the HUD reports it
  (`found in 4 cmp: -27`).
- **The search space is your space.** Gray "discarded memory" walls close in from the sides under
  the eliminated cells, pushing the developer toward the live part of the array, always leaving at
  least 3 units of room. They open again on a re-sort.
- **Off-by-one.** Every probe makes the probed cell's two neighbors fire, so each comparison has a
  price.
- **`IndexOutOfBounds` cannons.** Red `!` boxes sit just outside the array, at `arr[-1]` and
  `arr[16]`. They take turns flashing, then firing a shot aimed at the developer from the edge:
  every 4 s at full health, down to every 1.5 s near death. They can't be shot.
- **`O(n)!` taunt.** The HUD flashes `O(n)!` and the array fires twice as fast until the next
  re-sort. Two things trigger it:
  - **Linear search:** aiming at the cell right next to your previous probe while 3 or more cells
    are still in range, which a binary search never does. With only 2 cells left, probing the
    neighbor is correct and isn't punished.
  - **Brute force:** 4 or more comparisons within about a second, from your shots or Maven chains.
    With only 16 cells, a random search isn't much worse than a binary search, so without this,
    spamming a warm JIT (or Maven) would win. Careful play never triggers it; a single throw never
    does either, even with Maven level 2 (3 comparisons). Parried shots are free and never count.
- **Re-sort.** Running out of comparisons, or making a find, re-sorts the array: the bug moves and
  the hints clear. Running out also makes every live cell fire at once. Live cells drop shots
  meanwhile, faster as the boss weakens.
- **Parrying is a free comparison.** A parried shot flies back up into the cell above you and
  reveals its hint, or the bug, without spending a comparison, triggering the off-by-one shots,
  or counting toward the taunt. Stand under the cell you want tested and parry its shot.
- **Weapons:** every shot that reaches a cell is a comparison, so a warmed-up Java spray burns
  the budget and trips the taunt. This fight rewards a few deliberate throws, and shooting down
  incoming shots works too.
- **Maven chains cut both ways.** A chained splash that lands on a cell is a real comparison: an
  extra hint, or even a lucky find, but it also spends budget and sets off the off-by-one shots.
  A random hop never counts as linear search, but chained comparisons do count toward brute force,
  so a second Maven throw within a second triggers the taunt.

Implementation: `BinarySearchBoss`, `SearchCell`, and movement limits in `PlayerController`.

### Final boss: recursion

After wave 3, the final boss `recurse(3)` appears. It's built around how recursion works:

- **It calls itself.** Every 1.6 s, the root or one of its clones makes a recursive call,
  spawning a child one level deeper: `recurse(2)`, `recurse(1)`, down to the base case
  `recurse(0)`, shown in a different color, which can't call further. Each frame shows its
  argument as a number on its body. A frame's first call always lands directly below it, so a
  column of clones shields the root; other calls branch outward.
- **Unwinding.** Clones have 1 HP and block player shots. Destroying any frame unwinds its whole
  subtree, one depth level at a time. The root has 40 HP, shown in a boss health bar across the
  top of the screen (the fill flashes on hits, a lighter trail shows recent damage, and the bar
  turns gray during a stack overflow). Destroying the root unwinds the entire call stack and wins
  the fight. Cut a branch to open a lane, then hit the root.
- **Stack overflow.** If the tree grows past 12 frames, the boss throws `StackOverflowError`: it
  freezes and flashes for 4 s (the HUD shows the error), then its deep frames unwind.
- **Recursive split shot.** A frame fires a shot that splits in two every 0.45 s, as many times
  as its argument, so the root's shots go 1 → 2 → 4 → 8 while base cases fire single shots.
  Shooting a shot before it splits removes its future children. The boss fires faster as the
  root loses health.
- **Stack-trace beam.** The root marks its column with a blinking red outline for 0.8 s, then
  fires a 1 s beam of scrolling stack-trace lines down it. Move out of the column. It fires every
  9 s at full health, down to every 4 s near death.
- **Return-value rain.** Every frame you unwind "returns" its N: the digit falls straight down,
  so a big unwind above you means a lot of rain. You can shoot the falling digits or parry them
  back up. Frames lost to a stack overflow crashed, so they return nothing.
- **Tail-call dive.** Below half health, a clone blinks and marks a dotted path to where you
  are, then dives along it. If it misses, it leaves the screen and flies back to its slot. A
  diving clone can be shot, or parried, which kills it and unwinds its subtree.

Implementation: `RecursionBoss` (calling, unwinding, overflow, attacks), `BossFrame` (one call
frame, including the dive), `SplitShot` (split shots and return values), `StackTraceBeam`, and
`BossHud` (health bar, shared by both bosses through the `IBoss` interface). The run order lives in
`GameManager.stages`, a list of waves and bosses set in the Inspector.

## Languages (weapon select)

The player is a pixel-art developer, and the language picked on the start screen decides what
they hold and how they fight. This is the spec's core "other idea" (it still needs instructor
approval to count toward the optional items). Java is playable; Python and C++ appear in the
menu as `(coming soon)`, and choosing one prints `error: no compiler found for …`.

| Java mechanic | How it works | Where |
| --- | --- | --- |
| Coffee throw | Holding Space throws coffee cups (9 units/s, slower than a bullet). A cup smashes on the first thing it touches and splashes everything within 0.7 units. Hitting a bug from below, that's the bug plus its left and right neighbors. The splash also destroys bits. | `CoffeeCup`, `JavaWeapon` |
| JIT warmup | The first throws are slow (0.8 s apart). Each consecutive throw adds heat, so after six throws they come every 0.25 s, like the JVM optimizing hot code. Holding Space keeps the JIT warm, even during a slow cold wait. Release it for more than 0.6 s and it cools back down at 0.7 heat per second (fully hot to cold in about 1.4 s). | `JavaWeapon.Heat` |
| Garbage collector | Like the real JVM, it runs when there's garbage to collect, not on a fixed clock. It charges for 10 s (`GC 7s`), then waits (`GC ready`) until a hostile bit comes within 3 units of the developer. Then every hostile bit on screen pops and flies home harmlessly, where its bug rebuilds, and the GC recharges. | `GarbageCollector`, `BitProjectile.Collect()` |
| HUD | Two short lines in the top-right corner, kept narrow so they never reach the centered wave or boss HUD: `JIT [######----]`, and under it `GC 7s`, `GC ready`, or `freed N bits` after a collection. The text is only rebuilt when a value changes (see code issue 1). | `JavaHud` |

The menu is `LanguageSelect` with a list of `LanguageOption`s. Each option has its start-screen
description (weapon, passive, ability), an optional held-item sprite, the scripts to enable while
it's chosen, and an optional HUD. Adding Python or C++ means
writing their scripts and filling in a new entry; `GameManager` doesn't change.

### Boons (floppy disk drops)

Language-specific power-ups that work like Hades boons: permanent for the run, and
picking one you already have levels it up.

- **Drops:** wave *n* drops *n* floppy disks (1 + 2 + 3 = 6 per run), spread evenly over its
  kills. Disks fall from the bug that dropped them and rest on the floor until you walk over them.
  Any still on the floor when the wave ends are collected automatically. Bosses don't drop disks.
- **Install:** picking up a disk pauses on `Floppy disk found!  $ install --dependency`, offering
  up to 3 of your language's boons that aren't maxed yet (W/S or 1–3, Space to install).
- **HUD:** installed boons and levels show under the hearts, e.g. `SB2 JU1 MV1`.

| Java boon | Lv 1 | Lv 2 | Lv 3 |
| --- | --- | --- | --- |
| **Spring Boot**: manages your beans | JIT heats up 1.5× faster | 2× | 3× |
| **JUnit**: tests catch bugs before they ship | Catches 1 hit per stage (green ✓ over your head, `Test passed: bug caught`) | Catches 2 hits per stage | – |
| **Maven**: transitive dependencies | Each splash chains a small splash to 1 more nearby bug it didn't hit | Chains to 2 more | Chained splashes are full size |

JIT heat, the GC charge and JUnit's tests reset at the start of each stage, not when the game
pauses for an install. Implementation: `BoonManager`, `FloppyDisk`, `BoonDefinition` (a language's
pool lives on its `LanguageOption`), `JavaBoons`, and `ChainSplash` (Maven).

## Code issues in the instructor's project

Reviewed at commit `ba66d3e` of
[Oren-Steinberg/Space-Invaders-3D](https://github.com/Oren-Steinberg/Space-Invaders-3D).

### 1. UI text rebuilt every frame (inefficient)

`AlienFleet.Update()` copies the alien count into the counter every frame, and
`AlienCounter.Update()` rebuilds and re-formats the string every frame, even though the count
only changes when an alien dies. Each frame allocates a new string, which creates garbage
for the garbage collector and forces the UI text to be rebuilt.

```csharp
// AlienFleet.cs
void Update() { alienCounter.score = aliens.Count; }
// AlienCounter.cs
void Update() { uiText.text = "Remaining Aliens: " + score.ToString("#,0"); }
```

**Fix:** update the UI only when the value changes, using events. In this project
`Alien.Killed` notifies `AlienFormation`, and `PlayerHealth.Changed` notifies
`HeartsDisplay`. Neither runs any per-frame UI work.

```csharp
// PlayerHealth.cs
public event Action<int, int> Changed;
// HeartsDisplay.cs
private void OnEnable() => health.Changed += Refresh;
```

### 2. Moving dynamic Rigidbodies by setting `transform.position` with per-tick constants (non-idiomatic, buggy)

`Dart`, `AlienBullet` and `Alien` all carry **dynamic, non-kinematic Rigidbodies**
(see their prefabs), but are moved by writing `transform.position` in `FixedUpdate`, using
hard-coded distances per physics tick:

```csharp
// Dart.cs
void FixedUpdate() { Vector3 position = transform.position; position.y += speed; transform.position = position; }
```

- This teleports the bodies instead of letting physics move them, so collision detection can
  miss fast objects (tunneling), and the solver fights the manual position changes.
- Speed depends on the project's Fixed Timestep: `0.3` per tick at `0.02 s` is 15 units/s. If
  the timestep changes, the whole game changes speed.

**Fix:** use kinematic bodies driven by velocity, so speeds are in units per second.
`Bullet` is a kinematic `Rigidbody2D` with continuous collision detection and
`linearVelocity = Vector2.up * speed`. The bug formation is one kinematic body with the bug
colliders as children, rather than many separate bodies moved by hand.

### 3. Redundant `GetComponent` and an unchecked collision target (non-idiomatic, fragile)

```csharp
// Alien.cs
public AlienFleet fleet;
void OnCollisionEnter(Collision coll)
{
    fleet.GetComponent<AlienFleet>().aliens.Remove(gameObject);
    Destroy(gameObject);
    Destroy(coll.gameObject);
}
```

- `fleet` is already typed as `AlienFleet`, so `fleet.GetComponent<AlienFleet>()` does a
  pointless component lookup on every hit. `fleet.aliens` is the same object.
- The alien destroys **whatever** touched it without checking what it was, so anything with
  a collider (another alien, the ship, an alien bullet) gets destroyed.
- The alien reaches into the fleet's public list and edits it directly, which tightly couples
  the two classes.

**Fix:** the bullet checks what it hit with `TryGetComponent<Alien>` and calls `alien.Kill()`.
`Alien` raises a `Killed` event, and `AlienFormation` (which owns its private list) removes it.
A `spent` flag and an `isDead` flag stop double hits when two colliders overlap in the same
physics step.

### 4. Empty `Start()` / `Update()` methods left in (inefficient)

`Spaceship`, `Dart`, `Alien` and `AlienBullet` all keep the template's empty `Start()` and
`Update()`. Unity calls every `Update()` it finds, even empty ones, through a native-to-managed
call on every object, every frame. This matters most for projectiles and aliens, which exist
in large numbers.

**Fix:** remove empty Unity message methods. No script in this project declares a Unity
message it doesn't use.

### 5. Static and public fields used for tuning values (non-idiomatic)

```csharp
// Dart.cs
[Header("Inscribed")]
static float speed = .3f;
// Spaceship.cs
public static float shotCooldown = .05f;
public float lastShot = 0;
```

Static fields are not serialized, so `[Header]` does nothing on them and the values can't be
tuned in the Inspector. They are also shared by every instance. `lastShot` is internal state
but is exposed publicly.

**Fix:** tuning values are `[SerializeField] private` instance fields, with tooltips (for
example `PlayerController.moveSpeed` and `AlienFormation.stepDown`). Runtime state stays private.

### 6. String-based `Invoke` for the downward step (fragile)

```csharp
Invoke("startSidewaysMotion", downTime);
```

The method name is a string, so renaming the method silently breaks the call with no compile
error. The drop distance also depends on how many physics ticks happen during `downTime`,
so it isn't a fixed distance.

**Fix:** the formation moves in explicit discrete steps. Hitting an edge moves it down by a
fixed `stepDown` distance in a single step, with no timers.

### 7. Smaller issues

- **Wrong initial edge values:** `AlienFleet` finds the fleet's extents starting from
  `left = 0, right = 0` instead of the first alien's position or ±infinity. The results are
  only correct because the bounds (±16) happen to lie on both sides of 0.
  *Fix:* `GetHorizontalExtents()` starts at `float.MaxValue` / `float.MinValue` and uses sprite
  bounds rather than pivot points.
- **Instantiating, then moving:** `Instantiate(prefab)` followed by setting
  `transform.position` is two steps. *Fix:* `Instantiate(prefab, position, rotation)`.
- **No `Keyboard.current` null check:** reading `Keyboard.current.rightArrowKey` throws when no
  keyboard is connected. *Fix:* input reads check for null first.
- **Cooldown that does nothing:** a 0.05 s shot cooldown combined with `wasPressedThisFrame`
  never limits anything, because a human can't press twice within 50 ms.

## Project structure

```
Assets/
  Scripts/
    GameManager.cs            win/lose rules, game-over screen, restart
    Player/                   PlayerController (move + clamp), PlayerHealth, BreakpointParry,
                              PlayerShooter + Bullet (generic blaster, not in the scene; base for Python)
    Boss/                     IBoss, BinarySearchBoss, SearchCell, RecursionBoss, BossFrame, SplitShot, StackTraceBeam
    Boons/                    BoonManager, FloppyDisk, BoonDefinition
    Languages/                LanguageSelect, LanguageOption
      Java/                   JavaWeapon (coffee throw + JIT), CoffeeCup, GarbageCollector, JavaBoons, ChainSplash
    Aliens/                   WaveManager (waves of formations), AlienFormation (layout, lane, march, speed-up),
                              Alien, FleetThrower, BitProjectile
    UI/                       HeartsDisplay, JavaHud, WaveHud, BossHud, FloatingText
    Effects/                  OneShotAnimation (pixel flipbooks), CameraShake
    IShootable.cs             anything player bullets can destroy
  Prefabs/                    Formation, Boss/ (BinarySearchBoss, SearchCell, RecursionBoss, BossFrame, SplitShot, StackTraceBeam), Alien, Bullet, Bit, Languages/ (CoffeeCup), Effects/ (BugSquash, BitPop, CoffeeSplash)
  Art/Sprites/                pixel-art sprites (16 PPU, point filtered)
  Art/Fonts/                  VT323 font + license
```

## Credits

- **VT323** font by Peter Hull, licensed under the
  [SIL Open Font License 1.1](Assets/Art/Fonts/VT323-OFL.txt), via Google Fonts.
- All sprites are original pixel art made for this project.
- Built with AI assistance (Claude Code), as permitted by the assignment.
