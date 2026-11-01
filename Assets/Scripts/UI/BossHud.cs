using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss health bar across the top of the screen: the boss's label (e.g. "recurse(3)") over
/// a pixel bar whose fill drains on hits (flashing white), with a lighter "recent damage"
/// trail that catches up after a moment. Turns gray while the boss is stunned.
/// Works with any IBoss.
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

    private IBoss boss;
    private float fraction = 1f;
    private float trailFraction = 1f;
    private float trailWait;
    private float flashUntil;
    private bool overflowing;

    public void Bind(IBoss target)
    {
        Unbind();
        boss = target;
        boss.HpChanged += OnHp;
        boss.LabelChanged += OnLabel;
        boss.StunnedChanged += OnStunned;
        fraction = trailFraction = (float)boss.Hp / boss.MaxHp;
        overflowing = false;
        label.text = boss.Label;
        SetWidth(fill, fraction);
        SetWidth(trail, trailFraction);
        fillImage.color = fillColor;
        enabled = false;
    }

    private void OnDestroy() => Unbind();

    private void Unbind()
    {
        if (boss == null) return;
        boss.HpChanged -= OnHp;
        boss.LabelChanged -= OnLabel;
        boss.StunnedChanged -= OnStunned;
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

    private void OnLabel(string text) => label.text = text;

    private void OnStunned(bool active)
    {
        overflowing = active;
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
