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
| R | Restart after a win or game over |

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
| Better graphics for aliens and ship | ✅ | Original pixel art: a developer character who holds their language's weapon (a steaming coffee mug for Java), three bug types (beetle, ladybug, spider) with two-frame walk animations, bullets, and hearts. 16 pixels per unit, point filtering, and a Pixel Perfect Camera at 320×180. |
| Lives with a display | ✅ | Three pixel-art hearts in the top-left (`PlayerHealth`, `HeartsDisplay`). The fleet's bit attack (below) removes one heart per hit, followed by 1 s of invulnerability while the ship blinks. Running out of hearts ends the game. |
| Start and ending screens | ✅ | Terminal-style screens, all in `GameManager`. **Start:** the game loads frozen behind a terminal that types out `Compiling...` and `Build succeeded with 175 warnings (bugs) in 3 waves`; Space skips the typing, then a language select menu appears (W/S or 1–3 to choose, Space to run). **Between waves:** a 2 s terminal break (`$ git pull origin feature/more-bugs …`). **Win** (after wave 3): `BUILD PASSING … Process exited with code 0`, with the hearts left. **Game over:** a stack trace naming the cause of death (`OutOfHeartsException` or `BugsInProductionException`), then `Process exited with code 1`. R restarts from either end screen. |
| Animations when collisions occur | ✅ | Pixel-art flipbook effects (`OneShotAnimation`), tinted to the color of whatever was hit. **Bug shot:** the bug flattens, bursts into a splat, then scatters into pixels (`BugSquash`). **Bit shot, or bit hits the ship:** a small pixel burst (`BitPop`). **Ship hit:** a 0.2 s camera shake in whole game pixels, so the image stays on the pixel grid (`CameraShake`), plus the invulnerability blink. **Bug rebuilt:** a pop in the bug's color when its bit returns. |
| Additional levels | ✅ | Three waves, each different from the last (`WaveManager`, configured in the Inspector). **Wave 1:** the classic 5×11 formation sweeping the full width. **Wave 2:** two 4×6 formations, back-left and back-right, each bouncing between its screen edge and the midpoint, so they drop about twice as often. **Wave 3:** a 4×9 middle formation in front, with 3×6 back-left and back-right formations behind it. The back formations won't drop onto the middle one until it's cleared, so the player chooses which wave to thin out. Every formation has its own throw manager and speed-up. Hearts carry over between waves; JIT heat and the GC timer reset. A miniboss after wave 3 is planned. |

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

## Languages (weapon select)

The player is a pixel-art developer, and the language picked on the start screen decides what
they hold and how they fight. This is the spec's core "other idea" (it still needs instructor
approval to count toward the optional items). Java is playable; Python and C++ appear in the
menu as `(coming soon)`, and choosing one prints `error: no compiler found for …`.

| Java mechanic | How it works | Where |
| --- | --- | --- |
| Coffee throw | Holding Space throws coffee cups (9 units/s, slower than a bullet). A cup smashes on the first thing it touches and splashes everything within 0.7 units. Hitting a bug from below, that's the bug plus its left and right neighbors. The splash also destroys bits. | `CoffeeCup`, `JavaWeapon` |
| JIT warmup | The first throws are slow (0.8 s apart). Each consecutive throw adds heat, so after six throws they come every 0.25 s, like the JVM optimizing hot code. After 0.6 s without throwing, the JIT cools back down. | `JavaWeapon.Heat` |
| Garbage collector | Runs automatically every 10 s, like the JVM's GC. Every hostile bit on screen pops and flies home harmlessly, where its bug rebuilds. | `GarbageCollector`, `BitProjectile.Collect()` |
| HUD | Top-right: `JIT [######----]  GC 7s`, and `System.gc() freed N bits` after a collection. The text is only rebuilt when a value changes (see code issue 1). | `JavaHud` |

The menu is `LanguageSelect` with a list of `LanguageOption`s. Each option names the held-item
sprite, the scripts to enable while it's chosen, and an optional HUD. Adding Python or C++ means
writing their scripts and filling in a new entry; `GameManager` doesn't change.

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
    Player/                   PlayerController (move + clamp), PlayerHealth,
                              PlayerShooter + Bullet (generic blaster, not in the scene; base for Python)
    Languages/                LanguageSelect, LanguageOption
      Java/                   JavaWeapon (coffee throw + JIT), CoffeeCup, GarbageCollector
    Aliens/                   WaveManager (waves of formations), AlienFormation (layout, lane, march, speed-up),
                              Alien, FleetThrower, BitProjectile
    UI/                       HeartsDisplay, JavaHud, WaveHud
    Effects/                  OneShotAnimation (pixel flipbooks), CameraShake
    IShootable.cs             anything player bullets can destroy
  Prefabs/                    Formation, Alien, Bullet, Bit, Languages/ (CoffeeCup), Effects/ (BugSquash, BitPop, CoffeeSplash)
  Art/Sprites/                pixel-art sprites (16 PPU, point filtered)
  Art/Fonts/                  VT323 font + license
```

## Credits

- **VT323** font by Peter Hull, licensed under the
  [SIL Open Font License 1.1](Assets/Art/Fonts/VT323-OFL.txt), via Google Fonts.
- All sprites are original pixel art made for this project.
- Built with AI assistance (Claude Code), as permitted by the assignment.
