using UnityEngine;

/// <summary>
/// Java's boons:
///   spring - Spring Boot manages your beans: the JIT heats up faster.
///   junit  - a test catches hits before they ship: per-stage shield charges.
///   maven  - transitive dependencies: each splash pulls in extra, chained splashes.
/// </summary>
public class JavaBoons : MonoBehaviour, IBoonReceiver
{
    [SerializeField] private JavaWeapon weapon;
    [SerializeField] private PlayerHealth health;

    [Header("Spring Boot")]
    [Tooltip("JIT heat-gain multiplier per level.")]
    [SerializeField] private float[] springHeatMultiplier = { 1.5f, 2f, 3f };

    [Header("JUnit")]
    [Tooltip("Hits caught per stage, per level.")]
    [SerializeField] private int[] junitCatches = { 1, 2 };
    [Tooltip("Green check over the developer's head while a test is armed.")]
    [SerializeField] private SpriteRenderer junitCheck;
    [SerializeField] private FloatingText popupPrefab;
    [SerializeField] private RectTransform popupCanvas;
    [SerializeField] private Color junitColor = new Color32(0x39, 0xd3, 0x53, 0xff);

    [Header("Maven")]
    [Tooltip("Chained splashes per smash, per level.")]
    [SerializeField] private int[] mavenChains = { 1, 2, 2 };
    [Tooltip("Radius of each chained splash, per level (0.7 = a full splash).")]
    [SerializeField] private float[] mavenChainRadius = { 0.35f, 0.35f, 0.7f };

    private void OnEnable()
    {
        health.ShieldsChanged += OnShieldsChanged;
        health.HitBlocked += OnHitBlocked;
        OnShieldsChanged(health.Shields);
    }

    private void OnDisable()
    {
        health.ShieldsChanged -= OnShieldsChanged;
        health.HitBlocked -= OnHitBlocked;
    }

    private void OnShieldsChanged(int shields)
    {
        if (junitCheck != null) junitCheck.enabled = shields > 0;
    }

    private void OnHitBlocked()
    {
        if (popupPrefab != null && popupCanvas != null)
            popupPrefab.Spawn(popupCanvas, Camera.main, health.transform.position + Vector3.up * 1.2f,
                              "Test passed: bug caught", junitColor, 1.2f);
    }

    public void ApplyBoon(string id, int level)
    {
        int i = level - 1;
        switch (id)
        {
            case "spring":
                weapon.HeatGainMultiplier = springHeatMultiplier[Mathf.Min(i, springHeatMultiplier.Length - 1)];
                break;
            case "junit":
                health.ShieldsPerStage = junitCatches[Mathf.Min(i, junitCatches.Length - 1)];
                health.RefillShields();
                break;
            case "maven":
                weapon.ChainCount = mavenChains[Mathf.Min(i, mavenChains.Length - 1)];
                weapon.ChainRadius = mavenChainRadius[Mathf.Min(i, mavenChainRadius.Length - 1)];
                break;
            default:
                Debug.LogWarning($"JavaBoons: unknown boon '{id}'");
                break;
        }
    }
}
