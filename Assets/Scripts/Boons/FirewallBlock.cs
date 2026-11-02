using System.Collections;
using UnityEngine;

/// <summary>
/// Firewall boon: a small static brick barrier above the developer. Enemy projectiles that
/// touch it are absorbed (it takes a hit); player shots pass through. It darkens as it takes
/// hits, disappears when it runs out, and is rebuilt at the start of each stage.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class FirewallBlock : MonoBehaviour
{
    [SerializeField] private int maxHits = 3;
    [SerializeField] private Color fullColor = new Color32(0xf0, 0x88, 0x3e, 0xff);
    [SerializeField] private Color worstColor = new Color32(0x6e, 0x2a, 0x1a, 0xff);
    [SerializeField] private OneShotAnimation breakEffect;

    public bool IsUp => hitsLeft > 0;

    private SpriteRenderer body;
    private Collider2D hitbox;
    private int hitsLeft;

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
        Rebuild();
    }

    /// <summary>Back to full strength (start of each stage).</summary>
    public void Rebuild()
    {
        hitsLeft = maxHits;
        body.enabled = hitbox.enabled = true;
        body.color = fullColor;
    }

    /// <summary>An enemy projectile hit it. Returns true if it was absorbed.</summary>
    public bool Absorb()
    {
        if (!IsUp) return false;
        hitsLeft--;
        StartCoroutine(Flash());
        if (hitsLeft <= 0)
        {
            body.enabled = hitbox.enabled = false;
            if (breakEffect != null) breakEffect.Spawn(transform.position, fullColor);
        }
        return true;
    }

    private IEnumerator Flash()
    {
        body.color = Color.white;
        yield return new WaitForSeconds(0.06f);
        // Darker the more it has taken.
        body.color = Color.Lerp(worstColor, fullColor, (hitsLeft - 1f) / Mathf.Max(1, maxHits - 1));
    }
}
