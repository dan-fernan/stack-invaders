using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One call frame of the recursion boss: recurse(N). The root is recurse(maxDepth);
/// every frame it calls sits one depth lower. Destroying a frame unwinds its whole
/// subtree (handled by RecursionBoss). Clone bodies physically block player shots.
/// A clone can also make a tail-call dive at the developer (shootable and parryable
/// while diving), then fly back to its slot if it misses.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class BossFrame : MonoBehaviour, IEnemyBody, IParryable
{
    [Tooltip("Shows N on the frame's body.")]
    [SerializeField] private SpriteRenderer label;
    [Tooltip("A root with no calls to wait on is dimmed toward this color and can't be damaged.")]
    [SerializeField] private Color idleTint = new Color32(0x48, 0x4f, 0x58, 0xff);

    public RecursionBoss Boss { get; private set; }
    public BossFrame Parent { get; private set; }
    public readonly List<BossFrame> Children = new List<BossFrame>();

    public int Depth { get; private set; }
    /// <summary>Argument of this call: recurse(N). Base case is N == 0.</summary>
    public int N { get; private set; }
    /// <summary>-1 = branch grows left, +1 = right, 0 = the center column under the root.</summary>
    public int Direction { get; private set; }
    public int Hp { get; private set; }
    public int MaxHp { get; private set; }
    public bool IsRoot => Parent == null;

    /// <summary>
    /// A root that isn't waiting on any calls (no children) is idle: it can't be damaged,
    /// because a call only stays on the stack while its own calls are running.
    /// </summary>
    public bool IsIdle => IsRoot && Children.Count == 0;
    public bool IsDead { get; private set; }
    public Color Color { get; private set; }

    /// <summary>Its place in the tree, relative to the boss (it returns here after a dive).</summary>
    public Vector3 SlotLocalPosition { get; set; }

    /// <summary>Out of formation on a tail-call dive (including the trip back).</summary>
    public bool IsDiving { get; private set; }

    // IParryable: only a frame diving at the developer can be caught.
    public bool IsDangerous => IsDiving && !returning && !IsDead;
    public Vector3 Position => transform.position;
    public string ParryLabel => name;

    /// <summary>Which of its parent's call slots this frame occupies.</summary>
    public int SlotInParent { get; set; }

    /// <summary>Child call slots this frame has already used (by slot index).</summary>
    public readonly HashSet<int> UsedSlots = new HashSet<int>();

    private SpriteRenderer body;
    private Collider2D hitbox;
    private Color labelColor;
    private bool returning;

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
    }

    public void Init(RecursionBoss boss, BossFrame parent, int depth, int n, int direction, int hp,
                     Sprite sprite, Sprite digit, Color color, Color digitColor)
    {
        Boss = boss;
        Parent = parent;
        Depth = depth;
        N = n;
        Direction = direction;
        Hp = MaxHp = hp;
        Color = color;
        body.sprite = sprite;
        body.color = color;
        if (hitbox is BoxCollider2D box) box.size = sprite.bounds.size;
        // The root's sprite is drawn at 2x; scale its digit by a whole number to match.
        label.transform.localScale = Vector3.one * Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height / 9f));
        label.sprite = digit;
        label.color = labelColor = digitColor;
        name = $"recurse({n})";
        parent?.Children.Add(this);
        parent?.RefreshTint();
        RefreshTint();
    }

    public void OnShot() => TakeDamage(1);
    public void OnParriedHit() => TakeDamage(1);
    public void OnChainedHit() => TakeDamage(1);

    public void TakeDamage(int amount)
    {
        if (IsDead || IsIdle) return; // an idle root shrugs hits off (shots still stop on it)
        Hp -= amount;
        StartCoroutine(FlashWhite());
        Boss.OnFrameDamaged(this);
        if (Hp <= 0) Boss.Unwind(this);
    }

    /// <summary>Marks the frame dead immediately so it stops blocking shots; removal is animated by the boss.</summary>
    public void MarkDead()
    {
        IsDead = true;
        hitbox.enabled = false;
        Parent?.Children.Remove(this);
        Parent?.RefreshTint();
        Parryables.Unregister(this);
    }

    private void OnDestroy() => Parryables.Unregister(this);

    // ---------- Tail-call dive ----------

    /// <summary>Dive in a straight line at <paramref name="target"/>; if it misses, come back home.</summary>
    public void StartDive(Vector2 target, float speed, float returnSpeed, Camera cam)
    {
        if (IsDead || IsDiving) return;
        StartCoroutine(Dive(target, speed, returnSpeed, cam));
    }

    private IEnumerator Dive(Vector2 target, float speed, float returnSpeed, Camera cam)
    {
        IsDiving = true;
        returning = false;
        Parryables.Register(this);

        Vector3 dir = ((Vector3)target - transform.position).normalized;
        while (OnScreen(cam))
        {
            transform.position += dir * (speed * Time.deltaTime);
            yield return null;
        }

        // Missed: re-enter from the top and fly back to the slot (harmless on the way).
        Parryables.Unregister(this);
        returning = true;
        Vector3 c = cam.transform.position;
        float halfW = cam.orthographicSize * cam.aspect;
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, c.x - halfW, c.x + halfW),
                                         c.y + cam.orthographicSize + 1f, 0f);
        while (true)
        {
            Vector3 home = transform.parent.TransformPoint(SlotLocalPosition);
            transform.position = Vector3.MoveTowards(transform.position, home, returnSpeed * Time.deltaTime);
            if (transform.position == home) break;
            yield return null;
        }

        transform.localPosition = SlotLocalPosition;
        returning = false;
        IsDiving = false;
    }

    private bool OnScreen(Camera cam)
    {
        Vector3 c = cam.transform.position;
        Vector3 p = transform.position;
        return Mathf.Abs(p.x - c.x) < cam.orthographicSize * cam.aspect + 1f
            && Mathf.Abs(p.y - c.y) < cam.orthographicSize + 1f;
    }

    /// <summary>Caught by the breakpoint parry mid-dive: the call is cut short (it dies and unwinds).</summary>
    public bool StepInto()
    {
        if (!IsDangerous) return false;
        TakeDamage(Hp);
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsDangerous) return;
        if (other.TryGetComponent(out BreakpointParry parry) && parry.TryCatch(this)) return;
        if (other.TryGetComponent(out PlayerHealth health)) health.TakeHit();
    }

    /// <summary>The color the body returns to after flashes: dimmed while the root is idle.</summary>
    private Color RestingColor => IsIdle ? Color.Lerp(Color, idleTint, 0.7f) : Color;

    /// <summary>Re-applies the resting color (called when this root gains or loses calls).</summary>
    public void RefreshTint()
    {
        if (body != null && !IsDead) body.color = RestingColor;
    }

    /// <summary>Blink white (used on hits and on stack overflow).</summary>
    public void Flash(float duration) => StartCoroutine(FlashFor(duration));

    private IEnumerator FlashWhite()
    {
        body.color = Color.white;
        label.color = Color;
        yield return new WaitForSeconds(0.06f);
        body.color = RestingColor;
        label.color = labelColor;
    }

    private IEnumerator FlashFor(float duration)
    {
        var wait = new WaitForSeconds(0.1f);
        for (float t = 0f; t < duration; t += 0.1f)
        {
            body.color = body.color == Color.white ? RestingColor : Color.white;
            yield return wait;
        }
        body.color = RestingColor;
    }
}
