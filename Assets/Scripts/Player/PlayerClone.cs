using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A horizontal-scaling instance of the developer: a translucent shadow that copies the
/// developer's current frame, has one heart, and draws enemy fire (load balancing).
/// Killed clones come back at the start of the next stage.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class PlayerClone : MonoBehaviour
{
    [SerializeField] private Color shadowTint = new Color(0.55f, 0.7f, 1f, 0.6f);
    [SerializeField] private OneShotAnimation popEffect;

    /// <summary>Living clones; enemy throws pick their target among these and the developer.</summary>
    public static IReadOnlyCollection<PlayerClone> Alive => alive;
    private static readonly HashSet<PlayerClone> alive = new HashSet<PlayerClone>();

    public bool IsAlive { get; private set; }

    private SpriteRenderer body;
    private Collider2D hitbox;
    private SpriteRenderer source;

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
    }

    private void OnDisable() => alive.Remove(this);

    /// <summary>Copies the developer's sprite every frame.</summary>
    public void Init(SpriteRenderer developer) => source = developer;

    /// <summary>Back with its one heart (start of each stage).</summary>
    public void Respawn()
    {
        IsAlive = true;
        body.enabled = true;
        hitbox.enabled = true;
        alive.Add(this);
    }

    /// <summary>Takes a hit: clones have one heart, so this destroys it until the next stage.</summary>
    public void TakeHit()
    {
        if (!IsAlive) return;
        IsAlive = false;
        body.enabled = false;
        hitbox.enabled = false;
        alive.Remove(this);
        if (popEffect != null) popEffect.Spawn(transform.position, shadowTint);
    }

    private void LateUpdate()
    {
        if (!IsAlive || source == null) return;
        body.sprite = source.sprite;
        body.flipX = source.flipX;
        body.color = shadowTint;
    }
}
