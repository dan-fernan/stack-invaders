using UnityEngine;

/// <summary>
/// Picks the developer's frame each update:
///   - throwing (just attacked): the throw frames, once
///   - moving: the sideways run cycle, mirrored for left
///   - otherwise: the idle (back view) breathing loop
/// Movement is read from the actual change in position, so being pushed or held
/// by a wall plays the right animation instead of running in place.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private float idleFps = 2f;
    [Tooltip("Running to the right; mirrored for left.")]
    [SerializeField] private Sprite[] runFrames;
    [SerializeField] private float runFps = 10f;
    [SerializeField] private Sprite[] throwFrames;
    [SerializeField] private float throwFps = 16f;
    [Tooltip("Minimum horizontal speed (units/s) that counts as running.")]
    [SerializeField] private float runThreshold = 0.5f;

    private SpriteRenderer spriteRenderer;
    private float lastX;
    private float clock;
    private float throwTime = -1f;
    private bool facingLeft;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        lastX = transform.position.x;
    }

    /// <summary>Plays the throw once (weapons call this when they attack).</summary>
    public void PlayThrow() => throwTime = 0f;

    private void Update() => Tick(Time.deltaTime);

    /// <summary>Advances the animation by dt seconds (separate from Update so it can be stepped in tests).</summary>
    public void Tick(float dt)
    {
        if (dt <= 0f) return; // paused

        float speed = (transform.position.x - lastX) / dt;
        lastX = transform.position.x;
        clock += dt;

        if (throwTime >= 0f)
        {
            int frame = (int)(throwTime * throwFps);
            throwTime += dt;
            if (frame < throwFrames.Length)
            {
                Show(throwFrames[frame], flip: false);
                return;
            }
            throwTime = -1f;
        }

        if (Mathf.Abs(speed) >= runThreshold)
        {
            facingLeft = speed < 0f;
            Show(runFrames[(int)(clock * runFps) % runFrames.Length], flip: facingLeft);
        }
        else
        {
            Show(idleFrames[(int)(clock * idleFps) % idleFrames.Length], flip: false);
        }
    }

    private void Show(Sprite sprite, bool flip)
    {
        spriteRenderer.sprite = sprite;
        spriteRenderer.flipX = flip;
    }
}
