using System.Collections;
using UnityEngine;

/// <summary>
/// The fleet's attack. A bug with a neighbor gets "picked up" (neighbor flashes,
/// bug wobbles), then is molded into a 0 and a 1 and thrown at the developer as two
/// homing bits. When both bits of a throw are resolved:
///   - at least one made it home (on its own, collected, or parried back) -> the bug
///     reconstructs in its slot
///   - neither made it home (e.g. the player shot both)                   -> the bug is dead
/// Up to maxThrowsInFlight throws per formation can be out at once; throws come faster
/// as the formation shrinks.
/// </summary>
public class FleetThrower : MonoBehaviour
{
    /// <summary>One bug in flight as a 0 and a 1.</summary>
    private class Throw
    {
        public Alien alien;
        public Transform aim;
        public int bitsInFlight;
        public bool anyReturned;
    }

    [SerializeField] private AlienFormation formation;
    [SerializeField] private Transform player;
    [SerializeField] private BitProjectile bitPrefab;
    [SerializeField] private Sprite zeroSprite;
    [SerializeField] private Sprite oneSprite;

    [Header("Timing")]
    [SerializeField] private float firstThrowDelay = 3f;
    [Tooltip("Random seconds between throws with a full fleet (min, max).")]
    [SerializeField] private Vector2 fullFleetInterval = new Vector2(1f, 1.8f);
    [Tooltip("Random seconds between throws with one bug left (min, max).")]
    [SerializeField] private Vector2 lastBugInterval = new Vector2(0.35f, 0.7f);
    [Tooltip("How long the pickup is telegraphed before the throw.")]
    [SerializeField] private float telegraphTime = 0.5f;
    [Tooltip("How many throws this formation can have in the air at once.")]
    [SerializeField] private int maxThrowsInFlight = 2;

    [Header("Throw")]
    [Tooltip("Each bit leaves this many degrees to either side of a straight line to the ship.")]
    [SerializeField] private float spreadAngle = 35f;
    [SerializeField] private float bitSeparation = 0.2f;

    private int throwsInFlight;
    private float intervalScale = 1f;

    private void Awake()
    {
        if (formation == null) formation = GetComponent<AlienFormation>();
    }

    /// <summary>
    /// Set by the WaveManager when it spawns this formation.
    /// </summary>
    /// <param name="intervalMultiplier">Scales the delays between throws (2 = half as often).</param>
    public void Init(Transform target, float intervalMultiplier)
    {
        player = target;
        intervalScale = intervalMultiplier;
    }

    private void OnEnable() => StartCoroutine(ThrowLoop());

    // Coroutines keep running on a disabled component unless stopped explicitly.
    private void OnDisable() => StopAllCoroutines();

    private IEnumerator ThrowLoop()
    {
        yield return new WaitForSeconds(firstThrowDelay * intervalScale);
        while (true)
        {
            yield return new WaitUntil(() => throwsInFlight < maxThrowsInFlight);
            yield return ThrowOnce();
            yield return new WaitForSeconds(NextInterval());
        }
    }

    private float NextInterval()
    {
        float t = formation.FractionRemaining;
        float min = Mathf.Lerp(lastBugInterval.x, fullFleetInterval.x, t);
        float max = Mathf.Lerp(lastBugInterval.y, fullFleetInterval.y, t);
        return Random.Range(min, max) * intervalScale;
    }

    /// <summary>Telegraphs and launches one throw; doesn't wait for its bits to land.</summary>
    private IEnumerator ThrowOnce()
    {
        if (!formation.TryPickThrow(out Alien target, out Alien neighbor)) yield break;

        neighbor.Flash(telegraphTime);
        target.Wobble(telegraphTime);
        yield return new WaitForSeconds(telegraphTime);

        // The player may have shot the target during the telegraph.
        if (target == null || !target.IsPresent) yield break;

        // Load balancing: each throw targets a random living instance (the developer or a clone).
        Transform aim = PickInstance();
        Vector3 origin = target.transform.position;
        Vector2 toAim = ((Vector2)(aim.position - origin)).normalized;
        target.SetAway(true);

        var thrown = new Throw { alien = target, bitsInFlight = 2, aim = aim };
        throwsInFlight++;
        SpawnBit(thrown, "0", zeroSprite, origin + Vector3.left * bitSeparation, Rotate(toAim, -spreadAngle));
        SpawnBit(thrown, "1", oneSprite, origin + Vector3.right * bitSeparation, Rotate(toAim, spreadAngle));
    }

    private void SpawnBit(Throw thrown, string value, Sprite glyph, Vector3 position, Vector2 direction)
    {
        BitProjectile bit = Instantiate(bitPrefab, position, Quaternion.identity);
        bit.Launch(value, glyph, thrown.alien.Color, direction, thrown.aim, thrown.alien.transform);
        // Each bit resolves exactly once and is then destroyed, so this handler needs no unsubscribe.
        bit.Resolved += (_, outcome) => OnBitResolved(thrown, outcome);
    }

    private void OnBitResolved(Throw thrown, BitProjectile.Outcome outcome)
    {
        if (outcome == BitProjectile.Outcome.Returned) thrown.anyReturned = true;
        if (--thrown.bitsInFlight > 0) return;

        throwsInFlight--;
        Alien alien = thrown.alien;
        if (alien == null || alien.IsDead) return;
        if (thrown.anyReturned) alien.SetAway(false);
        else alien.Kill();
    }

    private Transform PickInstance()
    {
        int pick = Random.Range(0, PlayerClone.Alive.Count + 1);
        if (pick == 0) return player;
        foreach (PlayerClone clone in PlayerClone.Alive)
            if (--pick == 0) return clone.transform;
        return player;
    }

    private static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;
}
