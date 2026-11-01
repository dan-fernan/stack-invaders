using System;
using UnityEngine;

/// <summary>
/// A single bug in the formation. Swaps between two sprite frames each time the
/// formation steps (classic Space Invaders leg wiggle) and reports its death.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Alien : MonoBehaviour
{
    public int Points { get; private set; }
    public Bounds Bounds => spriteRenderer.bounds;

    /// <summary>Raised when this alien is destroyed, so the formation can react.</summary>
    public event Action<Alien> Killed;

    private SpriteRenderer spriteRenderer;
    private Sprite frameA;
    private Sprite frameB;
    private bool showingA = true;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(Sprite a, Sprite b, int points)
    {
        frameA = a;
        frameB = b;
        Points = points;
        showingA = true;
        spriteRenderer.sprite = frameA;
    }

    /// <summary>Called by the formation on every march step.</summary>
    public void AdvanceFrame()
    {
        showingA = !showingA;
        spriteRenderer.sprite = showingA ? frameA : frameB;
    }

    /// <summary>Destroy this alien (bullets will call this).</summary>
    public void Kill()
    {
        Killed?.Invoke(this);
        Destroy(gameObject);
    }
}
