using UnityEngine;

/// <summary>
/// Plays a short flipbook of pixel-art frames once, tinted, then destroys itself.
/// Used for collision effects (bug squash, bit pop, rebuild). Runs on scaled time,
/// so effects freeze along with the game on the end screens.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class OneShotAnimation : MonoBehaviour
{
    [Tooltip("White frames; tinted by Play(color).")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 16f;

    private SpriteRenderer spriteRenderer;
    private float elapsed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = frames[0];
    }

    /// <summary>Spawns this effect at a position with a tint.</summary>
    public void Spawn(Vector3 position, Color color)
    {
        OneShotAnimation effect = Instantiate(this, position, Quaternion.identity);
        effect.spriteRenderer.color = color;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        int frame = (int)(elapsed * framesPerSecond);
        if (frame >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }
        spriteRenderer.sprite = frames[frame];
    }
}
