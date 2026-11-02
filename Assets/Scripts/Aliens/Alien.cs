using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// A single bug in the formation. Swaps between two sprite frames each time the
/// formation steps (classic Space Invaders leg wiggle) and reports its death.
/// Can be temporarily "away" while it has been thrown at the player as bits.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Alien : MonoBehaviour, IShootable
{
    public int Points { get; private set; }
    public int Row { get; private set; }
    public int Column { get; private set; }
    public Color Color { get; private set; }
    public Bounds Bounds => spriteRenderer.bounds;

    /// <summary>Destroyed (or about to be this frame).</summary>
    public bool IsDead { get; private set; }

    /// <summary>Thrown at the player as bits; its slot keeps marching with the formation.</summary>
    public bool IsAway { get; private set; }

    /// <summary>In the formation, alive and not thrown.</summary>
    public bool IsPresent => !IsDead && !IsAway;

    /// <summary>Raised when this alien is destroyed, so the formation can react.</summary>
    public event Action<Alien> Killed;

    [Header("Effects")]
    [Tooltip("Played where the bug is shot.")]
    [SerializeField] private OneShotAnimation squashEffect;
    [Tooltip("Played when the bug reassembles from its returning bits.")]
    [SerializeField] private OneShotAnimation rebuildEffect;

    private const float WobblePixel = 1f / 16f;

    private SpriteRenderer spriteRenderer;
    private Sprite frameA, frameB, flashA, flashB;
    private bool showingA = true;
    private bool flashing;
    private Vector3 slotLocalPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(AlienFormation.AlienRow type, int row, int column)
    {
        frameA = type.frameA;
        frameB = type.frameB;
        flashA = type.flashA;
        flashB = type.flashB;
        Points = type.points;
        Color = type.color;
        Row = row;
        Column = column;
        slotLocalPosition = transform.localPosition;
        RefreshSprite();
    }

    /// <summary>Called by the formation on every march step.</summary>
    public void AdvanceFrame()
    {
        showingA = !showingA;
        RefreshSprite();
    }

    public void OnShot() => Kill();

    /// <summary>Destroy this alien (shot by the player, or both of its bits were destroyed).</summary>
    public void Kill()
    {
        if (IsDead) return; // two bullets can hit in the same frame
        IsDead = true;
        // A thrown bug dies off-screen (its bits already popped), so only squash it in the formation.
        if (!IsAway) squashEffect.Spawn(transform.position, Color);
        Killed?.Invoke(this);
        Destroy(gameObject);
    }

    /// <summary>Hide the alien while its bits are in flight, or bring it back.</summary>
    public void SetAway(bool away)
    {
        StopAllCoroutines();
        flashing = false;
        transform.localPosition = slotLocalPosition;
        bool rebuilding = IsAway && !away;
        IsAway = away;
        gameObject.SetActive(!away);
        if (!away) RefreshSprite();
        if (rebuilding) rebuildEffect.Spawn(transform.position, Color);
    }

    /// <summary>Telegraph for the neighbor doing the throwing: blink white.</summary>
    public void Flash(float duration) => StartCoroutine(FlashRoutine(duration));

    /// <summary>Telegraph for the alien about to be thrown: shake in place.</summary>
    public void Wobble(float duration) => StartCoroutine(WobbleRoutine(duration));

    private IEnumerator FlashRoutine(float duration)
    {
        var wait = new WaitForSeconds(0.08f);
        for (float t = 0f; t < duration; t += 0.08f)
        {
            flashing = !flashing;
            RefreshSprite();
            yield return wait;
        }
        flashing = false;
        RefreshSprite();
    }

    private IEnumerator WobbleRoutine(float duration)
    {
        var wait = new WaitForSeconds(0.05f);
        float offset = WobblePixel;
        for (float t = 0f; t < duration; t += 0.05f)
        {
            transform.localPosition = slotLocalPosition + new Vector3(offset, 0f, 0f);
            offset = -offset;
            yield return wait;
        }
        transform.localPosition = slotLocalPosition;
    }

    private void RefreshSprite()
    {
        if (flashing)
            spriteRenderer.sprite = showingA ? flashA : flashB;
        else
            spriteRenderer.sprite = showingA ? frameA : frameB;
    }
}
