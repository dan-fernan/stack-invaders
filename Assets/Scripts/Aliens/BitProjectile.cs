using System;
using UnityEngine;

/// <summary>
/// A "0" or "1" that a thrown alien was molded into.
///   Homing    – steers toward the player while above them (dodgeable: limited turn rate).
///   Coasting  – once it drops past the player's row it stops steering and flies on.
///   Returning – after leaving the screen it re-enters from the top and flies back to
///               its alien's slot (harmless to the player, still shootable).
/// Reports exactly one Outcome, then destroys itself.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public class BitProjectile : MonoBehaviour, IShootable
{
    public enum Outcome { Returned, HitPlayer, Shot }

    private enum State { Homing, Coasting, Returning }

    [SerializeField] private float homingSpeed = 6f;
    [Tooltip("Max degrees per second the bit can turn toward the player. Higher = harder to dodge.")]
    [SerializeField] private float turnRate = 200f;
    [Tooltip("Stops homing once the bit is this far above the player's position or lower.")]
    [SerializeField] private float homingCutoff = 0.5f;
    [SerializeField] private float returnSpeed = 7f;
    [SerializeField] private float offscreenMargin = 0.5f;

    public event Action<BitProjectile, Outcome> Resolved;

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

    /// <param name="homeSlot">Transform of the alien this bit came from (it keeps marching while hidden).</param>
    public void Launch(Sprite glyph, Color color, Vector2 direction, Transform player, Transform homeSlot)
    {
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
                Vector2 toHome = (Vector2)home.position - pos;
                if (toHome.magnitude <= returnSpeed * Time.fixedDeltaTime)
                    Resolve(Outcome.Returned);
                else
                    rb.linearVelocity = toHome.normalized * returnSpeed;
                return;
        }

        if (IsOffscreen(pos))
            BeginReturn();
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
        if (state == State.Returning) return; // harmless on the way home
        if (other.TryGetComponent(out PlayerHealth health) && health.TakeHit())
            Resolve(Outcome.HitPlayer);
    }

    public void OnShot() => Resolve(Outcome.Shot);

    private void Resolve(Outcome outcome)
    {
        if (resolved) return;
        resolved = true;
        Resolved?.Invoke(this, outcome);
        Destroy(gameObject);
    }
}
