using UnityEngine;

/// <summary>
/// One cell of the binary search miniboss's array. Shooting it is a comparison
/// against the hidden bug's index; the boss decides what the cell then shows.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class SearchCell : MonoBehaviour, IEnemyBody
{
    [Tooltip("The cell's contents: ?, <, > or the bug.")]
    [SerializeField] private SpriteRenderer glyph;

    public int Index { get; private set; }

    /// <summary>Still in the search range (can be probed, can fire).</summary>
    public bool IsLive { get; private set; } = true;

    private BinarySearchBoss boss;
    private SpriteRenderer frame;
    private Collider2D hitbox;

    private void Awake()
    {
        frame = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
    }

    public void Init(BinarySearchBoss owner, int index)
    {
        boss = owner;
        Index = index;
        name = $"arr[{index}]";
    }

    /// <summary>Sets what the cell shows and whether it's still in the search range.</summary>
    public void Show(Sprite content, Color color, bool live)
    {
        glyph.sprite = content;
        glyph.color = color;
        frame.color = color;
        IsLive = live;
        hitbox.enabled = live; // eliminated cells let shots pass through
    }

    public void OnShot() => boss.Probe(this);
}
