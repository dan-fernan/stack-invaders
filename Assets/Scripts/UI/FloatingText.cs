using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Short-lived pop-up text (e.g. "● paused: 0") that rises and fades over a world position.
/// Runs on unscaled time so it still animates during hit-stop.
/// </summary>
[RequireComponent(typeof(Text))]
public class FloatingText : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.7f;
    [Tooltip("UI pixels risen over the lifetime.")]
    [SerializeField] private float rise = 10f;

    private Text label;
    private RectTransform rect;
    private Vector2 start;
    private Color color;
    private float age;

    private void Awake()
    {
        label = GetComponent<Text>();
        rect = (RectTransform)transform;
    }

    /// <summary>Spawns a copy of this prefab on the canvas above a world position.</summary>
    public void Spawn(RectTransform canvas, Camera cam, Vector3 worldPosition, string text, Color tint)
    {
        FloatingText popup = Instantiate(this, canvas);
        Vector2 screen = cam.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, cam, out Vector2 local);
        popup.start = local;
        popup.rect.anchoredPosition = local;
        popup.label.text = text;
        popup.color = tint;
        popup.label.color = tint;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = age / lifetime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        rect.anchoredPosition = start + Vector2.up * (rise * t);
        label.color = new Color(color.r, color.g, color.b, 1f - t * t);
    }
}
