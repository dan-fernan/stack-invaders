using UnityEngine;

/// <summary>
/// Player projectile. Flies straight up, destroys the first IShootable it touches,
/// and cleans itself up once it leaves the top of the screen.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 14f;

    private Camera cam;
    private bool spent;

    private void Awake()
    {
        cam = Camera.main;
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.up * speed;
    }

    private void Update()
    {
        float top = cam.transform.position.y + cam.orthographicSize;
        if (transform.position.y > top + 1f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // A bullet can overlap two targets in the same physics step; only count the first.
        if (spent) return;
        if (!other.TryGetComponent(out IShootable target)) return;

        spent = true;
        target.OnShot();
        Destroy(gameObject);
    }
}
