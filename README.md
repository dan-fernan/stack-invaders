# Stack Invaders

A programming-themed, pixel-art take on Space Invaders, made in Unity 6 (6000.6, 2D URP) for
**CSCI 381 – Video Game Design and Development, Assignment 1** (Fall 2026).

You play a developer holding a mug of coffee. The invaders are literal bugs, they attack by
breaking themselves into bits, and the bosses are a binary search and a recursive function.

I started this version from scratch instead of building on the in-class
[Space-Invaders-3D](https://github.com/Oren-Steinberg/Space-Invaders-3D) project, which the
assignment allows. For the code-issues requirement, I went through the class project's code,
wrote up the problems I found, and explain how this project avoids each one (see
[Code issues in the class project](#code-issues-in-the-class-project)).

## How to play

**In Unity:** open the project in Unity **6000.6.0f1** (or a newer Unity 6), open
`Assets/Scenes/SampleScene.unity` and press Play.

**In a browser:** build with **File → Build Profiles → Web – Desktop – Release**. The output in
`Builds/Web` can be zipped (with `index.html` at the top level) and uploaded to itch.io as an HTML
game at 960 × 540.

| Key | Action |
| --- | --- |
| A / D (or ← / →) | Move |
| Space (hold) | Throw coffee. On the start screen, Space skips the typing, then starts the run |
| Shift | Breakpoint parry: catch an incoming shot and send it back |
| W / S (or ↑ / ↓), 1–3, Space | Pick a boon when you grab a floppy disk |
| R | Restart after a win or a game over |
| F10 (Editor and development builds only) | Debug: skip the current wave or boss |

A run goes **Wave 1 → Wave 2 → `binarySearch()` miniboss → Wave 3 → `recurse(4)` final boss**.

## Assignment checklist

### Required (all four)

| Requirement | Done | How | Where |
| --- | --- | --- | --- |
| Remaining aliens speed up as others are destroyed | ✅ | Each formation marches in steps, and the time between steps shrinks from 0.45 s (full formation) to 0.04 s (last bug). It scales with the *square* of the fraction left, so the last few bugs get frantic, like the arcade original. | `AlienFormation.CurrentStepInterval()` |
| Detect when the game can no longer be won | ✅ | After every march step, `GameManager` checks the lowest living bug in every formation against the top of the developer. Once they meet, there's no way to shoot that bug in time, so the game ends right away (`BugsInProductionException`), no matter how many hearts are left. | `GameManager.CheckInvasion()`, `WaveManager.LowestY` |
| Keep the ship on screen | ✅ | The developer's x is clamped to the camera's half-width minus half the sprite's width, so the whole sprite stays visible at any resolution. (The binary search boss also narrows that range with walls; same clamp, tighter limits.) | `PlayerController.ClampX()` |
| Find and fix at least three issues in the class code | ✅ | Seven issues written up below, each with how this project handles it. | [Code issues](#code-issues-in-the-class-project) |

### Optional (four required, six done)

| Option | Done | Summary |
| --- | --- | --- |
| Better graphics for the aliens and ship | ✅ | All original pixel art at 16 pixels per unit, with a Pixel Perfect Camera at 320×180. The ship is now an animated developer (idle, sideways run, a full throwing motion), the aliens are three kinds of bugs with walk cycles, and the shots are coffee mugs, bits, floppy disks and boss attacks. |
| Lives with a display | ✅ | Pixel hearts in the top-left corner. You start with 3 (up to 5 with the Compute Upgrade boon), lose one per hit, and blink for a second of invulnerability afterwards. |
| Starting and ending screens | ✅ | Terminal-style screens (details below). |
| Animations when collisions occur | ✅ | Pixel flipbooks tinted to whatever was hit: bugs flatten, splat and scatter into pixels; bits and shots pop; coffee splashes; the camera shakes when you're hit; a bug pops back in when it rebuilds. |
| Additional levels | ✅ | Three waves with different layouts plus two boss fights (details below). |
| Balance: harder but still beatable | ✅ | The original has aliens that barely fight back. Here the bugs attack constantly, the run ends in two multi-phase bosses, and you get a parry, a garbage collector and floppy-disk upgrades to keep up. The numbers were tuned over many playtests, and everything below is beatable. |

Related, but not claimed: there's no permanent shield bunker row like the arcade game, but
the **JUnit** boon gives you shields (it blocks hits each stage) and the **Firewall** boon builds
small barriers that absorb enemy shots. The background is a flat dark "terminal" color rather than
a new image, so I'm not counting it.

## Screens

- **Start:** the game loads frozen behind a terminal that types out a compile log
  (`Build succeeded with 175 warnings (bugs) and 2 errors (bosses)`). Space skips the typing. The
  log then turns into a color-coded start page with three sections: `// CONTROLS`,
  `// JAVA RUNTIME` (what the coffee cup, just-in-time compilation and garbage collector each do),
  and `// GOAL`, ending in `> press SPACE to run_`.
- **Between stages:** before a wave, a banner slides across the paused game (`WAVE 2/3`) while the
  new bugs march in from above. Before a boss, the screen dims, red hazard stripes flash around
  `!! WARNING !!` and the boss's name, the camera shakes, and the boss drops into place.
- **Win:** a `$ git push origin main` screen that prints line by line: `BUILD PASSING`,
  `Congratulations, developer: you shipped it!`, then a green `[ OK ]` for each thing you beat,
  your hearts and boons, and your run time.
- **Game over:** a stack trace naming what killed you (`OutOfHeartsException` or
  `BugsInProductionException`), then `Process exited with code 1`.
- R restarts from either end screen.

## Waves

| Wave | Layout |
| --- | --- |
| 1 | The classic 5×11 block sweeping the full width. |
| 2 | Two 4×6 blocks, one on each side, each bouncing between its edge and the middle, so they drop about twice as often. |
| 3 | A 4×9 block in front with two 3×6 blocks behind it. The back blocks won't drop onto the front one until it's cleared, so you choose which side to thin out. |

Every block speeds up on its own as it loses bugs, and has its own attack timer. Hearts carry over
between stages.

### How the bugs attack

A bug is code that doesn't do what it should, so the fleet attacks by recompiling a bug into raw
bits and throwing it at you.

1. Every 1–1.8 s (down to 0.35–0.7 s as the fleet shrinks), a bug next to a living neighbor is
   picked. The neighbor flashes and the bug wobbles for half a second as a warning.
2. The bug splits into a **0** and a **1**, thrown at angles on either side of you.
3. The bits home in on you, but only while they're above you, so a late sidestep dodges them.
4. Bits that miss loop back to their bug's spot. A bit that hits you costs a heart.
5. If at least one bit makes it home, the bug **rebuilds**. Shoot both bits and the bug is gone
   for good.

Each block can have two throws in the air at once, so wave 3 can have six.

## The developer's tools

### Java

Python and C++ were planned but cut for time, so the game is Java-only. The language setup is
data-driven (`LanguageOption`), so adding another one later doesn't touch `GameManager`.

| Ability | What it does |
| --- | --- |
| **Coffee cup** | Hold Space to throw mugs. A mug smashes on the first thing it touches and splashes everything around it: hit a bug from below and its two neighbors get splashed too. Splashes also destroy bits. |
| **Just-in-time compilation** | Your first throws are slow (0.8 s apart). Every throw in a row heats up the JIT, and after six you're throwing every 0.25 s. Let go for more than 0.6 s and it cools back down. The meter in the top-right shows `JIT [######----]`. |
| **Garbage collector** | Charges for 10 s, then waits until a bit gets close to you, and clears every bit on screen at once. Cleared bits fly home harmlessly. |

### Breakpoint parry (Shift)

1. Shift sets a breakpoint for 0.2 s, shown as a red dot beside you.
2. Any parryable shot that reaches you in that window is **paused** for a moment (`● paused: 0`),
   then sent back. A fleet bit flies home harmlessly (so its bug rebuilds, since the parry
   defends but never kills). A boss shot flies back up and hits the boss.
3. Catching something has no cooldown, so you can catch the 0 and then the 1. Missing locks the
   debugger for 1 s (the dot turns gray).
4. **It costs you speed.** Setting a breakpoint makes the JVM throw away its optimized code, so
   each press halves your JIT heat (`JIT deoptimized`). Parrying trades offense for defense.

Parryable: fleet bits, the binary search's shots, and the recursion boss's **purple** and **gold**
shots. Everything else has to be dodged or shot down.

### Boons (floppy disks)

Defeated bugs drop floppy disks, which work like boons in Hades: grab one, the game pauses on
`$ install --dependency`, and you pick one of three upgrades. They last the whole run, and picking
one you already have levels it up.

- Wave *n* drops *n* + 1 disks (2, 3, then 4), and beating the miniboss gives 2 more installs,
  so 11 per run. Disks left on the floor are collected automatically when the wave ends.
- Your installed boons show under the hearts, e.g. `SB2 JU1 MV1`.

| Java boon | Lv 1 | Lv 2 | Lv 3 |
| --- | --- | --- | --- |
| **Spring Boot** | JIT heats up 1.5× faster | 2× | 3× |
| **JUnit** | A test catches 1 hit per stage (`Test passed: bug caught`) | 2 hits per stage | – |
| **Maven** | Each splash chains a small splash to 1 more nearby bug | Chains to 2 | Chained splashes are full size |

| Boon (any language) | Lv 1 | Lv 2 |
| --- | --- | --- |
| **Compute Upgrade** | +1 max heart (arrives full) | +1 more (5 total) |
| **SSD Upgrade** | +20% move speed | +40% |
| **git revert** | Once per stage, your first lost heart comes back after 5 s if you don't get hit again | After 3 s |
| **Multithreading** | Every 3rd throw throws two mugs | Every 2nd throw |
| **Firewall** | Two small brick barriers above you, each absorbing 3 enemy shots (your own shots pass through, and so do beams). Rebuilt every stage | A third barrier in the middle |

JIT heat, the garbage collector, JUnit's tests, the firewalls and git revert all reset at the start
of each stage.

## Bosses

### Miniboss: `binarySearch()`

Comes after wave 2. A bug hides somewhere in a sorted 16-cell array, and every cell shows `?`.

- **Every shot is a comparison.** Hit a wrong cell and it shows `<` or `>` pointing toward the bug.
  Everything on the wrong side is eliminated (grayed out, and shots pass through it), just like a
  binary search throwing away half its range. Hit the bug's cell and it's **found**: the cell
  blinks red with the bug inside for a second and a half, then the array re-sorts.
- **Efficient searches hit harder.** The boss has 160 HP. A find deals 15 damage plus 6 for every
  comparison you had left (you get 6 per search; a perfect search of 16 cells needs at most 5).
- **The search space is your space.** Gray "discarded memory" walls close in from the sides under
  the eliminated cells, squeezing you toward what's left.
- **Off-by-one:** every comparison makes the two cells next to it fire at you.
- **`IndexOutOfBounds` cannons** at `arr[-1]` and `arr[16]` take turns shooting at you, faster as
  the boss weakens. They can't be destroyed.
- **`O(n)!`** If you search like a linear search (probing the cell right next to your last one)
  or brute-force it (4+ comparisons in about a second), the array fires twice as fast until the
  next re-sort. Spamming a hot JIT doesn't win this fight.
- **Parrying is a free comparison.** Stand under a cell and parry its shot: it reveals that cell's
  hint without spending a comparison. If that cell is the bug, it's revealed but not hit; you
  still have to shoot it.
- Running out of comparisons re-sorts the array (the bug moves) and makes every live cell fire.

### Final boss: `recurse(4)`

Comes after wave 3, with 70 HP and three phases.

**The whole fight**
- **It calls itself.** The boss keeps spawning copies of itself one level deeper, down to the base
  case `recurse(0)`, building a tree of up to 35 frames. Each frame shows its argument on its body.
  The first call always lands right below its caller, so a column of clones (2 HP each) shields the
  root.
- **The root only takes damage while it's waiting on its calls.** A root with no children is idle
  (grayed out) and immune, just like a function only stays on the stack while its calls run. You
  have to let it recurse, then cut a lane and hit the root while it's busy. The emptier the tree,
  the faster it calls, so clearing it doesn't buy you much time.
- **Unwinding.** Destroying a frame unwinds everything below it, one level at a time, and each
  frame "returns" its number as a falling digit.
- **Stack overflow.** More than 18 frames throws `StackOverflowError`: the boss freezes for 4 s,
  then the deep frames unwind.
- **Attacks:** shots that split in two up to three times; Fibonacci volleys (1, 1, 2, 3, 5, 8
  purple shots) that can **only be parried**, not shot; and stack-trace beams down marked columns.

**Phase 2 (below 66%)**
- **Memoization:** it remembers where you've been, marks those spots on the floor, then drops gold
  "cache hit" shots on them. They can't be shot, only dodged or parried.
- **Stack growth:** the tree pushes down a row every 9 s.
- **Tail-call dives:** a clone marks a path to you and dives along it.
- Beams fire in three columns, always with a safe lane between them.

**Phase 3 (below 33%): mutual recursion.** The root splits into `ping(3)` and `pong(3)`, which
share what's left of its health. The health bar splits too, with a label over each half
(`ping 22/35 HP`, `pong 35/35 HP`, or `pong returned` once it's beaten). Beat one and the other
calls twice as fast; beat both to win. Beams fire in four columns.

## Code issues in the class project

These are from the in-class
[Space-Invaders-3D](https://github.com/Oren-Steinberg/Space-Invaders-3D) project, reviewed at commit
`ba66d3e`. Since this project was written from scratch, each "fix" is how this codebase handles
the same thing.

### 1. The UI text is rebuilt every frame (inefficient)

`AlienFleet.Update()` copies the alien count into the counter every frame, and
`AlienCounter.Update()` rebuilds the string every frame too, even though the number only changes
when an alien dies. That's a new string allocation per frame for the garbage collector to clean up,
plus a UI rebuild.

```csharp
// AlienFleet.cs
void Update() { alienCounter.score = aliens.Count; }
// AlienCounter.cs
void Update() { uiText.text = "Remaining Aliens: " + score.ToString("#,0"); }
```

**Fix:** update the UI only when the value changes, using events. Here, `PlayerHealth.Changed`
drives `HeartsDisplay`, `Alien.Killed` drives `AlienFormation`, and the Java HUD only redraws when
the JIT meter actually moves a segment. Nothing rebuilds UI text every frame.

```csharp
// PlayerHealth.cs
public event Action<int, int> Changed;
// HeartsDisplay.cs
private void OnEnable() => health.Changed += Refresh;
```

### 2. Dynamic Rigidbodies moved by hand with per-tick distances (non-idiomatic, buggy)

`Dart`, `AlienBullet` and `Alien` all have **dynamic** Rigidbodies, but they're moved by writing
`transform.position` in `FixedUpdate` by a fixed amount per physics tick:

```csharp
// Dart.cs
void FixedUpdate() { Vector3 position = transform.position; position.y += speed; transform.position = position; }
```

- Teleporting bodies around means fast objects can skip past colliders, and the physics engine
  fights the manual moves.
- The speed depends on the physics timestep: `0.3` per tick at `0.02 s` is 15 units/s. Change the
  timestep and the whole game changes speed.

**Fix:** kinematic bodies driven by velocity, so speeds are in units per second. `Bullet` is a
kinematic `Rigidbody2D` with continuous collision detection and
`linearVelocity = Vector2.up * speed`. A bug formation is one body with the bugs as children,
instead of dozens of bodies moved one by one.

### 3. A pointless `GetComponent` and a collision that destroys anything (fragile)

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

- `fleet` is already an `AlienFleet`, so `fleet.GetComponent<AlienFleet>()` looks up something it
  already has, on every hit.
- The alien destroys **whatever** touched it without checking what it was: another alien, the
  ship, an alien bullet.
- The alien edits the fleet's public list directly, which ties the two classes together.

**Fix:** the projectile checks what it hit (`TryGetComponent<IShootable>`) and calls `OnShot()`.
`Alien` raises a `Killed` event, and `AlienFormation` removes it from its own private list. A flag
stops the same bug from being hit twice in one physics step.

### 4. Empty `Start()` and `Update()` methods (inefficient)

`Spaceship`, `Dart`, `Alien` and `AlienBullet` all keep the template's empty `Start()` and
`Update()`. Unity still calls every `Update()` it finds, empty or not, on every object, every
frame, and aliens and bullets exist in large numbers.

**Fix:** no script in this project declares a Unity message it doesn't use.

### 5. Static and public fields for tuning values (non-idiomatic)

```csharp
// Dart.cs
[Header("Inscribed")]
static float speed = .3f;
// Spaceship.cs
public static float shotCooldown = .05f;
public float lastShot = 0;
```

Static fields aren't serialized, so the `[Header]` does nothing and the values can't be tuned in
the Inspector, and they're shared by every instance. `lastShot` is internal state but is public.

**Fix:** tuning values are `[SerializeField] private` fields with tooltips (for example
`PlayerController.moveSpeed`), and runtime state stays private.

### 6. String-based `Invoke` for the step down (fragile)

```csharp
Invoke("startSidewaysMotion", downTime);
```

The method name is a string, so renaming the method breaks this silently with no compile error.
The drop distance also depends on how many physics ticks fit into `downTime`.

**Fix:** formations move in explicit steps. Hitting an edge moves the formation down by a fixed
`stepDown` in one step, with no timers.

### 7. Smaller things

- **Wrong starting values for the fleet's edges:** `AlienFleet` starts its min/max search at
  `left = 0, right = 0`. It only works because the fleet happens to straddle 0.
  *Fix:* start at `float.MaxValue` / `float.MinValue` and use sprite bounds.
- **Instantiate, then move:** `Instantiate(prefab)` followed by setting `transform.position`.
  *Fix:* `Instantiate(prefab, position, rotation)`.
- **No `Keyboard.current` null check:** reading keys throws if no keyboard is connected.
  *Fix:* every input read checks for null first.
- **A cooldown that never applies:** a 0.05 s cooldown on `wasPressedThisFrame` can't limit
  anything, because nobody presses a key twice in 50 ms.

## Project structure

```
Assets/
  Scripts/
    GameManager.cs   stages, win/lose rules, terminal screens, restart
    Player/          PlayerController (move + clamp), PlayerHealth, PlayerAnimator, BreakpointParry,
                     PlayerShooter + Bullet (a plain blaster, not used in the scene)
    Aliens/          WaveManager, AlienFormation (layout, march, speed-up), Alien, FleetThrower, BitProjectile
    Boss/            IBoss, BinarySearchBoss, SearchCell, RecursionBoss, BossFrame, SplitShot, StackTraceBeam
    Boons/           BoonManager, BoonDefinition, FloppyDisk, UniversalBoons, FirewallBlock
    Languages/       LanguageSelect (start page), LanguageOption
      Java/          JavaWeapon (throwing + JIT), CoffeeCup, GarbageCollector, JavaBoons, ChainSplash (Maven)
    UI/              HeartsDisplay, JavaHud, WaveHud, BossHud, StageTransition, FloatingText
    Effects/         OneShotAnimation (pixel flipbooks), CameraShake
    IShootable.cs    anything the player's shots can hit
  Prefabs/           Formation, Alien, Bit, Bullet, Boss/, Boons/, Languages/, Effects/, UI/
  Art/Sprites/       pixel art (16 PPU, point filtered)
  Art/Fonts/         VT323 font + license
```

## Credits

- **VT323** font by Peter Hull, under the
  [SIL Open Font License 1.1](Assets/Art/Fonts/VT323-OFL.txt), via Google Fonts.
- All sprites are original pixel art made for this project.
- Built with AI assistance (Claude Code), which the assignment allows.
