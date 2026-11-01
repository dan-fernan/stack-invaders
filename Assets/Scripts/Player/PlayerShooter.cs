using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fires bullets from the ship's nose when Space is pressed, with a cooldown.
/// Kept separate from PlayerController so weapons can be swapped later
/// (Python / Java / C++ from the spec) without touching movement.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [Tooltip("Minimum seconds between shots.")]
    [SerializeField] private float fireCooldown = 0.35f;

    private SpriteRenderer spriteRenderer;
    private float nextFireTime;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.spaceKey.isPressed) return;
        if (Time.time < nextFireTime) return;

        Fire();
    }

    private void Fire()
    {
        nextFireTime = Time.time + fireCooldown;
        Vector3 muzzle = new Vector3(transform.position.x, spriteRenderer.bounds.max.y, 0f);
        Instantiate(bulletPrefab, muzzle, Quaternion.identity);
    }
}
