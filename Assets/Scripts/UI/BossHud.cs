using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss health bar across the top of the screen: a "recurse(3)" label over a pixel bar
/// whose fill drains on hits (flashing white), with a lighter "recent damage" trail that
/// catches up after a moment. Turns gray during StackOverflowError.
/// Only animates (Update enabled) while the fill flash or trail is still moving.
/// </summary>
public class BossHud : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private RectTransform fill;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform trail;
    [Tooltip("Full bar width in UI pixels (1 UI unit = 1 game pixel).")]
    [SerializeField] private float barWidth = 160f;
    [SerializeField] private Color fillColor = new Color32(0xf8, 0x51, 0x49, 0xff);
    [SerializeField] private Color stunnedColor = new Color32(0x8b, 0x94, 0x9e, 0xff);
    [SerializeField] private float flashTime = 0.06f;
    [Tooltip("Seconds the damage trail waits before catching up.")]
    [SerializeField] private float trailDelay = 0.4f;
    [Tooltip("Fraction of the bar per second the trail drains.")]
    [SerializeField] private float trailSpeed = 0.6f;

    private RecursionBoss boss;
    private float fraction = 1f;
    private float trailFraction = 1f;
    private float trailWait;
    private float flashUntil;
    private bool overflowing;

    public void Bind(RecursionBoss target)
    {
        Unbind();
        boss = target;
        boss.RootHpChanged += OnHp;
        boss.OverflowChanged += OnOverflow;
        fraction = trailFraction = (float)boss.Root.Hp / boss.Root.MaxHp;
        label.text = $"recurse({boss.Root.N})";
        SetWidth(fill, fraction);
        SetWidth(trail, trailFraction);
        fillImage.color = fillColor;
        enabled = false;
    }

    private void OnDestroy() => Unbind();

    private void Unbind()
    {
        if (boss == null) return;
        boss.RootHpChanged -= OnHp;
        boss.OverflowChanged -= OnOverflow;
        boss = null;
    }

    private void OnHp(int hp, int max)
    {
        fraction = Mathf.Clamp01((float)hp / max);
        SetWidth(fill, fraction);
        trailWait = trailDelay;
        flashUntil = Time.unscaledTime + flashTime;
        fillImage.color = Color.white;
        enabled = true; // animate the flash and trail
    }

    private void OnOverflow(bool active)
    {
        overflowing = active;
        label.text = active ? "StackOverflowError!" : $"recurse({boss.Root.N})";
        if (!enabled) fillImage.color = active ? stunnedColor : fillColor;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool flashing = Time.unscaledTime < flashUntil;
        fillImage.color = flashing ? Color.white : overflowing ? stunnedColor : fillColor;

        if (trailWait > 0f) trailWait -= dt;
        else trailFraction = Mathf.MoveTowards(trailFraction, fraction, trailSpeed * dt);
        SetWidth(trail, trailFraction);

        if (!flashing && Mathf.Approximately(trailFraction, fraction)) enabled = false;
    }

    private void SetWidth(RectTransform bar, float f)
    {
        // Whole pixels, so the bar edge stays crisp.
        bar.sizeDelta = new Vector2(Mathf.Round(f * barWidth), bar.sizeDelta.y);
    }
}
