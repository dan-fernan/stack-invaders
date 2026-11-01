using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves the player ship left/right with A/D (or arrow keys) and keeps it fully on screen.
/// Covers the assignment requirement: "Prevent the spaceship from moving offscreen."
/// Bosses can narrow the allowed range further (e.g. binary search's closing walls).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("Horizontal speed in world units per second.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Extra gap (world units) kept between the ship and the screen edge.")]
    [SerializeField] private float edgePadding = 0.1f;

    private Camera cam;
    private SpriteRenderer spriteRenderer;
    private bool limited;
    private float limitMin, limitMax;

    /// <summary>Confine the ship (its whole body) to world x between min and max, on top of the screen clamp.</summary>
    public void SetMovementLimits(float min, float max)
    {
        limited = true;
        limitMin = min;
        limitMax = max;
    }

    public void ClearMovementLimits() => limited = false;

    private void Awake()
    {
        cam = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        float input = ReadHorizontalInput();

        Vector3 pos = transform.position;
        pos.x += input * moveSpeed * Time.deltaTime;
        pos.x = ClampToScreen(pos.x);
        transform.position = pos;
    }

    private static float ReadHorizontalInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return 0f;

        float x = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        return x;
    }

    /// <summary>
    /// Clamp using the camera's visible area and the sprite's half-width, so the
    /// whole ship stays visible regardless of resolution or aspect ratio.
    /// </summary>
    private float ClampToScreen(float x)
    {
        float halfViewWidth = cam.orthographicSize * cam.aspect;
        float halfShipWidth = spriteRenderer.bounds.extents.x;
        float limit = halfViewWidth - halfShipWidth - edgePadding;

        float camX = cam.transform.position.x;
        x = Mathf.Clamp(x, camX - limit, camX + limit);

        if (limited)
        {
            float min = limitMin + halfShipWidth, max = limitMax - halfShipWidth;
            x = min <= max ? Mathf.Clamp(x, min, max) : (limitMin + limitMax) / 2f;
        }
        return x;
    }
}
