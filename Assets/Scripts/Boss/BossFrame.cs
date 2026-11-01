using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One call frame of the recursion boss: recurse(N). The root is recurse(maxDepth);
/// every frame it calls sits one depth lower. Destroying a frame unwinds its whole
/// subtree (handled by RecursionBoss). Clone bodies physically block player shots.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class BossFrame : MonoBehaviour, IShootable
{
    [Tooltip("Shows N on the frame's body.")]
    [SerializeField] private SpriteRenderer label;

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
    public bool IsDead { get; private set; }
    public Color Color { get; private set; }

    /// <summary>Which of its parent's call slots this frame occupies.</summary>
    public int SlotInParent { get; set; }

    /// <summary>Child call slots this frame has already used (by slot index).</summary>
    public readonly HashSet<int> UsedSlots = new HashSet<int>();

    private SpriteRenderer body;
    private Collider2D hitbox;
    private Color labelColor;

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
    }

    public void OnShot() => TakeDamage(1);

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
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
    }

    /// <summary>Blink white (used on hits and on stack overflow).</summary>
    public void Flash(float duration) => StartCoroutine(FlashFor(duration));

    private IEnumerator FlashWhite()
    {
        body.color = Color.white;
        label.color = Color;
        yield return new WaitForSeconds(0.06f);
        body.color = Color;
        label.color = labelColor;
    }

    private IEnumerator FlashFor(float duration)
    {
        var wait = new WaitForSeconds(0.1f);
        for (float t = 0f; t < duration; t += 0.1f)
        {
            body.color = body.color == Color ? Color.white : Color;
            yield return wait;
        }
        body.color = Color;
    }
}
