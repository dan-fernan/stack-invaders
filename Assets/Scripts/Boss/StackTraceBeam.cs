using System.Collections;
using UnityEngine;

/// <summary>
/// The root's stack-trace beam: first a blinking outline marks the column (telegraph),
/// then a beam of scrolling "stack trace" lines fills it and hurts the developer.
/// Destroys itself when done.
/// </summary>
public class StackTraceBeam : MonoBehaviour
{
    [SerializeField] private SpriteRenderer outlineLeft;
    [SerializeField] private SpriteRenderer outlineRight;
    [SerializeField] private SpriteRenderer fill;
    [Tooltip("Tiled 'text lines' renderer; its sprite cycles through scrollFrames.")]
    [SerializeField] private SpriteRenderer lines;
    [SerializeField] private Sprite[] scrollFrames;
    [SerializeField] private BoxCollider2D hitbox;
    [SerializeField] private float telegraphTime = 0.8f;
    [SerializeField] private float fireTime = 1f;
    [SerializeField] private float width = 1f;
    [SerializeField] private float scrollFps = 20f;
    [SerializeField] private Color telegraphColor = new Color32(0xf8, 0x51, 0x49, 0xff);
    [SerializeField] private Color beamColor = new Color32(0x58, 0xa6, 0xff, 0xff);

    /// <summary>Starts the attack in a column at x, from top down to bottom.</summary>
    public void Fire(float x, float top, float bottom)
    {
        float height = top - bottom;
        transform.position = new Vector3(x, (top + bottom) / 2f, 0f);

        float half = width / 2f;
        outlineLeft.transform.localPosition = new Vector3(-half, 0f, 0f);
        outlineRight.transform.localPosition = new Vector3(half, 0f, 0f);
        outlineLeft.size = outlineRight.size = new Vector2(outlineLeft.size.x, height);
        fill.size = new Vector2(width, height);
        lines.size = new Vector2(width, height);
        hitbox.size = new Vector2(width, height);

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // Telegraph: blinking outline, harmless.
        hitbox.enabled = false;
        fill.enabled = lines.enabled = false;
        outlineLeft.color = outlineRight.color = telegraphColor;
        for (float t = 0f; t < telegraphTime; t += 0.1f)
        {
            outlineLeft.enabled = outlineRight.enabled = !outlineLeft.enabled;
            yield return new WaitForSeconds(0.1f);
        }

        // Fire: the beam fills the column.
        outlineLeft.enabled = outlineRight.enabled = false;
        fill.enabled = lines.enabled = true;
        fill.color = new Color(beamColor.r, beamColor.g, beamColor.b, 0.35f);
        lines.color = beamColor;
        hitbox.enabled = true;

        for (float t = 0f; t < fireTime; t += Time.deltaTime)
        {
            int frame = (int)(t * scrollFps) % scrollFrames.Length;
            lines.sprite = scrollFrames[frame];
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // PlayerHealth's invulnerability window keeps this to one heart per beam.
        if (!hitbox.enabled) return;
        if (other.TryGetComponent(out PlayerClone clone)) clone.TakeHit();
        else if (other.TryGetComponent(out PlayerHealth health)) health.TakeHit();
    }
}
