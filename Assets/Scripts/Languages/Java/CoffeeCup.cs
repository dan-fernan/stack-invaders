using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Java's projectile: a thrown coffee cup. Slower than a bullet, but it smashes on the
/// first thing it touches and splashes everything shootable within a radius.
/// Hitting a bug from below, that's the bug plus its left and right neighbors.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class CoffeeCup : MonoBehaviour
{
    [SerializeField] private float speed = 9f;
    [Tooltip("Splash radius in world units (0.7 hits a bug plus its left/right neighbors).")]
    [SerializeField] private float splashRadius = 0.7f;
    [SerializeField] private OneShotAnimation splashEffect;
    [SerializeField] private Color splashColor = new Color32(0xc0, 0x8a, 0x4f, 0xff);
    [Tooltip("Frames cycled while the cup tumbles through the air.")]
    [SerializeField] private Sprite[] tumbleFrames;
    [SerializeField] private float tumbleFps = 12f;

    // Reused between smashes to avoid allocating a new results list each time.
    private static readonly List<Collider2D> Hits = new List<Collider2D>();

    private Camera cam;
    private SpriteRenderer spriteRenderer;
    private float age;
    private bool smashed;

    private void Awake()
    {
        cam = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.up * speed;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (tumbleFrames != null && tumbleFrames.Length > 0)
            spriteRenderer.sprite = tumbleFrames[(int)(age * tumbleFps) % tumbleFrames.Length];

        float top = cam.transform.position.y + cam.orthographicSize;
        if (transform.position.y > top + 1f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (smashed || !other.TryGetComponent(out IShootable _)) return;
        Smash();
    }

    private void Smash()
    {
        smashed = true;
        Vector2 center = transform.position;

        Hits.Clear();
        Physics2D.OverlapCircle(center, splashRadius, ContactFilter2D.noFilter, Hits);
        foreach (Collider2D hit in Hits)
        {
            if (hit.TryGetComponent(out IShootable target))
                target.OnShot();
        }

        splashEffect.Spawn(center, splashColor);
        Destroy(gameObject);
    }
}
