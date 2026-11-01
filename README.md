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
| Space (hold) | Shoot |
| R | Restart after game over |

## Required features

| Requirement | Status | How it's met | Where |
| --- | --- | --- | --- |
| Remaining aliens speed up as others are destroyed | ✅ | The formation marches in discrete steps. Time between steps is interpolated from 0.45 s (full formation) to 0.04 s (last bug) using the *square* of the fraction remaining, so the speed-up ramps hardest near the end, like the arcade original. | `AlienFormation.CurrentStepInterval()` |
| Detect when the game can no longer be won | ✅ | After every march step, `GameManager` compares the bottom edge of the **lowest living** bug to the top of the ship. Once they meet, that bug can no longer be shot before it reaches the player, so the game ends immediately, regardless of lives left. | `GameManager.CheckInvasion()`, `AlienFormation.LowestY` |
| Prevent the spaceship from moving offscreen | ✅ | The ship's x is clamped to the camera's visible half-width minus the sprite's half-width (from `SpriteRenderer.bounds`), so the whole ship stays visible at any resolution or aspect ratio. | `PlayerController.ClampToScreen()` |
| Find and fix at least three issues in the instructor's code | ✅ | Seven issues documented below, each with the fix used in this project. | [Below](#code-issues-in-the-instructors-project) |

## Optional features (four required)

| Feature | Status | Notes |
| --- | --- | --- |
| Better graphics for aliens and ship | ✅ | Original pixel art: a ship, three bug types (beetle, ladybug, spider) with two-frame walk animations, bullets, and hearts. 16 pixels per unit, point filtering, and a Pixel Perfect Camera at 320×180. |
| Lives with a display | ✅ | Three pixel-art hearts in the top-left (`PlayerHealth`, `HeartsDisplay`). Running out of hearts ends the game. |
| Start and ending screens | 🚧 In progress | The game-over screen is done: a terminal-style stack trace naming the cause of death (`OutOfHeartsException`, `BugsInProductionException`). The start screen and win screen are still to do. |
| _TBD_ | ⬜ | |

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
    Player/                   PlayerController (move + clamp), PlayerShooter, Bullet, PlayerHealth
    Aliens/                   AlienFormation (spawn, march, speed-up), Alien
    UI/                       HeartsDisplay
  Prefabs/                    Alien, Bullet
  Art/Sprites/                pixel-art sprites (16 PPU, point filtered)
  Art/Fonts/                  VT323 font + license
```

## Credits

- **VT323** font by Peter Hull, licensed under the
  [SIL Open Font License 1.1](Assets/Art/Fonts/VT323-OFL.txt), via Google Fonts.
- All sprites are original pixel art made for this project.
- Built with AI assistance (Claude Code), as permitted by the assignment.
