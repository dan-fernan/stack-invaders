using UnityEngine;

/// <summary>
/// The recursion boss's attack: a projectile that calls itself. Fired by recurse(N),
/// it splits into two children every splitDelay seconds, N times (1 -> 2 -> 4 -> 8 for
/// N = 3). recurse(0) fires a shot that never splits.
/// Shooting a shot before it splits removes its future children. Parrying sends it
/// back up to hit the first boss body part (IEnemyBody) it touches.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public class SplitShot : MonoBehaviour, IShootable, IParryable
{
    [SerializeField] private float speed = 4.5f;
    [SerializeField] private float splitDelay = 0.45f;
    [Tooltip("Each child turns this many degrees away from its parent's direction.")]
    [SerializeField] private float splitAngle = 22f;
    [SerializeField] private float returnSpeed = 12f;
    [SerializeField] private Color parriedColor = new Color32(0x39, 0xd3, 0x53, 0xff);
    [SerializeField] private OneShotAnimation popEffect;

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Camera cam;
    private BossFrame source;
    private int splitsLeft;
    private float age;
    private bool parried;
    private bool done;
    private string parryLabel;
    private bool shootable = true;

    /// <summary>False for memoized "cache hit" shots: player shots pass through them.</summary>
    public bool CanBeShot => shootable && !parried;

    /// <summary>Makes this shot indestructible to player fire (it can still be dodged or parried).</summary>
    public void MakeUnshootable() => shootable = false;

    private bool parryable = true;

    /// <summary>The breakpoint parry ignores this shot: it must be dodged or shot down.</summary>
    public void MakeUnparryable()
    {
        parryable = false;
        Parryables.Unregister(this);
    }

    public bool IsDangerous => !done && !parried;
    public Vector3 Position => transform.position;
    public string ParryLabel => parryLabel ?? (source != null ? source.name : "return value");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        sprite = GetComponent<SpriteRenderer>();
        cam = Camera.main;
    }

    private void OnEnable()
    {
        if (parryable) Parryables.Register(this);
    }
    private void OnDisable() => Parryables.Unregister(this);

    /// <param name="from">The frame that fired it (parried shots fly back toward it).</param>
    /// <param name="glyph">Optional sprite override (return values fall as their digit).</param>
    public void Launch(BossFrame from, int splits, Vector2 direction, Color color,
                       Sprite glyph = null, float speedScale = 1f, string label = null)
    {
        source = from;
        parryLabel = label;
        splitsLeft = splits;
        sprite.color = color;
        if (glyph != null)
        {
            sprite.sprite = glyph;
            if (TryGetComponent(out BoxCollider2D box)) box.size = glyph.bounds.size;
        }
        rb.linearVelocity = direction.normalized * (speed * speedScale);
    }

    private void Update()
    {
        if (done) return;
        age += Time.deltaTime;

        if (parried)
        {
            // Home on the frame that fired it; if that frame is gone, keep flying up.
            if (source != null && !source.IsDead)
                rb.linearVelocity = ((Vector2)(source.transform.position - transform.position)).normalized * returnSpeed;
        }
        else if (splitsLeft > 0 && age >= splitDelay)
        {
            Split();
            return;
        }

        if (IsOffscreen()) Finish(false);
    }

    private void Split()
    {
        Vector2 v = rb.linearVelocity;
        foreach (float angle in new[] { -splitAngle, splitAngle })
        {
            SplitShot child = Instantiate(this, transform.position, Quaternion.identity);
            child.Launch(source, splitsLeft - 1, Quaternion.Euler(0f, 0f, angle) * v, sprite.color,
                         speedScale: v.magnitude / speed);
            // Runtime flags aren't copied by Instantiate: children inherit them explicitly.
            if (!parryable) child.MakeUnparryable();
            if (!shootable) child.MakeUnshootable();
        }
        Finish(false);
    }

    public bool StepInto()
    {
        if (!IsDangerous || !parryable) return false;
        parried = true;
        splitsLeft = 0;
        sprite.color = parriedColor;
        rb.linearVelocity = Vector2.up * returnSpeed;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (done) return;

        if (parried)
        {
            // Parried: hits the first boss body part it touches (a frame, a search cell...).
            if (other.TryGetComponent(out IEnemyBody body))
            {
                body.OnParriedHit();
                Finish(true);
            }
            return;
        }

        if (other.TryGetComponent(out BreakpointParry parry) && parry.TryCatch(this)) return;
        if (other.TryGetComponent(out FirewallBlock wall) && wall.Absorb()) { Finish(true); return; }
        if (other.TryGetComponent(out PlayerHealth health) && health.TakeHit())
            Finish(true);
    }

    /// <summary>Shot by the player before splitting: its future children never exist.</summary>
    public void OnShot()
    {
        if (parried) return; // it's the developer's now
        Finish(true);
    }

    private bool IsOffscreen()
    {
        Vector2 c = cam.transform.position;
        Vector2 p = transform.position;
        return Mathf.Abs(p.x - c.x) > cam.orthographicSize * cam.aspect + 1f
            || Mathf.Abs(p.y - c.y) > cam.orthographicSize + 1f;
    }

    private void Finish(bool pop)
    {
        if (done) return;
        done = true;
        if (pop && popEffect != null) popEffect.Spawn(transform.position, sprite.color);
        Destroy(gameObject);
    }
}
