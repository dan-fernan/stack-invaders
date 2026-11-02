using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A "0" or "1" that a thrown alien was molded into.
///   Homing    – steers toward the player while above them (dodgeable: limited turn rate).
///   Coasting  – once it drops past the player's row it stops steering and flies on.
///   Returning – after leaving the screen it re-enters from the top and flies back to
///               its alien's slot (harmless to the player, still shootable).
///   Reflected – caught by the player's breakpoint parry: flies straight back to its slot
///               (harmless, can't be shot) and counts as returned, so its bug rebuilds.
/// Reports exactly one Outcome, then destroys itself.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public class BitProjectile : MonoBehaviour, IShootable, IParryable
{
    public enum Outcome { Returned, HitPlayer, Shot }

    private enum State { Homing, Coasting, Returning, Reflected }

    [SerializeField] private float homingSpeed = 6f;
    [Tooltip("Max degrees per second the bit can turn toward the player. Higher = harder to dodge.")]
    [SerializeField] private float turnRate = 90f;
    [Tooltip("Stops homing once the bit is this far above the player's position or lower.")]
    [SerializeField] private float homingCutoff = 0.5f;
    [SerializeField] private float returnSpeed = 7f;
    [Tooltip("Speed when flying home after being parried.")]
    [FormerlySerializedAs("patchSpeed")]
    [SerializeField] private float reflectSpeed = 12f;
    [Tooltip("Tint once parried, so it reads as deflected.")]
    [FormerlySerializedAs("patchColor")]
    [SerializeField] private Color reflectColor = new Color32(0x39, 0xd3, 0x53, 0xff);
    [SerializeField] private float offscreenMargin = 0.5f;
    [Tooltip("Played when the bit is shot or hits the ship.")]
    [SerializeField] private OneShotAnimation popEffect;

    public event Action<BitProjectile, Outcome> Resolved;

    /// <summary>"0" or "1".</summary>
    public string Value { get; private set; }

    public Vector3 Position => transform.position;
    public string ParryLabel => Value;

    /// <summary>Still a threat to the player (not heading home, parried or resolved).</summary>
    public bool IsDangerous => !resolved && (state == State.Homing || state == State.Coasting);

    /// <summary>Every bit currently in play (lets Java's garbage collector find them without a scene search).</summary>
    public static IReadOnlyCollection<BitProjectile> Active => active;
    private static readonly HashSet<BitProjectile> active = new HashSet<BitProjectile>();

    private Rigidbody2D rb;
    private Camera cam;
    private Transform target;
    private Transform home;
    private State state;
    private bool resolved;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        cam = Camera.main;
    }

    private void OnEnable()
    {
        active.Add(this);
        Parryables.Register(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        Parryables.Unregister(this);
    }

    /// <summary>
    /// Garbage-collected: pops and flies straight home from where it is, harmless.
    /// Returns false if the bit was already heading home or resolved.
    /// </summary>
    public bool Collect()
    {
        if (!IsDangerous) return false;
        popEffect.Spawn(transform.position, GetComponent<SpriteRenderer>().color);
        state = State.Returning;
        return true;
    }

    /// <summary>
    /// Caught by the breakpoint parry: turn green and fly straight home, harmless; its bug rebuilds.
    /// Returns false if the bit was no longer a threat.
    /// </summary>
    public bool StepInto()
    {
        if (!IsDangerous) return false;
        GetComponent<SpriteRenderer>().color = reflectColor;
        state = State.Reflected;
        return true;
    }

    /// <param name="homeSlot">Transform of the alien this bit came from (it keeps marching while hidden).</param>
    public void Launch(string value, Sprite glyph, Color color, Vector2 direction, Transform player, Transform homeSlot)
    {
        Value = value;
        var sr = GetComponent<SpriteRenderer>();
        sr.sprite = glyph;
        sr.color = color;
        target = player;
        home = homeSlot;
        state = State.Homing;
        rb.linearVelocity = direction.normalized * homingSpeed;
    }

    private void FixedUpdate()
    {
        Vector2 pos = rb.position;

        switch (state)
        {
            case State.Homing:
                if (pos.y > target.position.y + homingCutoff)
                    SteerToward(target.position);
                else
                    state = State.Coasting; // passed the player: commit to the current line
                break;

            case State.Returning:
                FlyHome(pos, returnSpeed, Outcome.Returned);
                return;

            case State.Reflected:
                FlyHome(pos, reflectSpeed, Outcome.Returned);
                return;
        }

        if (IsOffscreen(pos))
            BeginReturn();
    }

    private void FlyHome(Vector2 pos, float speed, Outcome onArrival)
    {
        // The slot is gone if its formation was cleared while this bit was out.
        if (home == null)
        {
            Resolve(onArrival);
            return;
        }

        Vector2 toHome = (Vector2)home.position - pos;
        if (toHome.magnitude <= speed * Time.fixedDeltaTime)
            Resolve(onArrival);
        else
            rb.linearVelocity = toHome.normalized * speed;
    }

    private void SteerToward(Vector2 point)
    {
        Vector2 velocity = rb.linearVelocity;
        Vector2 desired = point - rb.position;
        float angle = Vector2.SignedAngle(velocity, desired);
        float maxTurn = turnRate * Time.fixedDeltaTime;
        velocity = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -maxTurn, maxTurn)) * velocity;
        rb.linearVelocity = velocity;
    }

    private bool IsOffscreen(Vector2 pos)
    {
        Vector2 center = cam.transform.position;
        float halfH = cam.orthographicSize + offscreenMargin;
        float halfW = cam.orthographicSize * cam.aspect + offscreenMargin;
        return Mathf.Abs(pos.x - center.x) > halfW || Mathf.Abs(pos.y - center.y) > halfH;
    }

    /// <summary>Re-enter from just above the top edge and head home.</summary>
    private void BeginReturn()
    {
        state = State.Returning;
        Vector2 center = cam.transform.position;
        float halfW = cam.orthographicSize * cam.aspect;
        float x = Mathf.Clamp(rb.position.x, center.x - halfW, center.x + halfW);
        rb.position = new Vector2(x, center.y + cam.orthographicSize + offscreenMargin);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsDangerous) return; // harmless on the way home
        if (other.TryGetComponent(out BreakpointParry parry) && parry.TryCatch(this)) return;
        if (other.TryGetComponent(out PlayerHealth health) && health.TakeHit())
            Resolve(Outcome.HitPlayer);
    }

    public void OnShot()
    {
        if (state == State.Reflected) return; // deflected, on its way home
        Resolve(Outcome.Shot);
    }

    private void Resolve(Outcome outcome)
    {
        if (resolved) return;
        resolved = true;
        if (outcome != Outcome.Returned)
            popEffect.Spawn(transform.position, GetComponent<SpriteRenderer>().color);
        Resolved?.Invoke(this, outcome);
        Destroy(gameObject);
    }
}
