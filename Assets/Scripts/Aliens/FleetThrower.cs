using System.Collections;
using UnityEngine;

/// <summary>
/// The fleet's attack. A bug with a neighbor gets "picked up" (neighbor flashes,
/// bug wobbles), then is molded into a 0 and a 1 and thrown at the ship as two
/// homing bits. When both bits are resolved:
///   - at least one made it home  -> the bug reconstructs in its slot
///   - neither made it home        -> the bug is dead (e.g. the player shot both)
/// Only one throw is in the air at a time; throws come faster as the fleet shrinks.
/// </summary>
public class FleetThrower : MonoBehaviour
{
    [SerializeField] private AlienFormation formation;
    [SerializeField] private Transform player;
    [SerializeField] private BitProjectile bitPrefab;
    [SerializeField] private Sprite zeroSprite;
    [SerializeField] private Sprite oneSprite;

    [Header("Timing")]
    [SerializeField] private float firstThrowDelay = 3f;
    [Tooltip("Random seconds between throws with a full fleet (min, max).")]
    [SerializeField] private Vector2 fullFleetInterval = new Vector2(1.5f, 2.5f);
    [Tooltip("Random seconds between throws with one bug left (min, max).")]
    [SerializeField] private Vector2 lastBugInterval = new Vector2(0.5f, 1f);
    [Tooltip("How long the pickup is telegraphed before the throw.")]
    [SerializeField] private float telegraphTime = 0.5f;

    [Header("Throw")]
    [Tooltip("Each bit leaves this many degrees to either side of a straight line to the ship.")]
    [SerializeField] private float spreadAngle = 35f;
    [SerializeField] private float bitSeparation = 0.2f;

    private Alien thrownAlien;
    private int bitsInFlight;
    private bool anyReturned;

    private void OnEnable() => StartCoroutine(ThrowLoop());

    // Coroutines keep running on a disabled component unless stopped explicitly.
    private void OnDisable() => StopAllCoroutines();

    private IEnumerator ThrowLoop()
    {
        yield return new WaitForSeconds(firstThrowDelay);
        while (true)
        {
            yield return ThrowOnce();
            yield return new WaitForSeconds(NextInterval());
        }
    }

    private float NextInterval()
    {
        float t = formation.FractionRemaining;
        float min = Mathf.Lerp(lastBugInterval.x, fullFleetInterval.x, t);
        float max = Mathf.Lerp(lastBugInterval.y, fullFleetInterval.y, t);
        return Random.Range(min, max);
    }

    private IEnumerator ThrowOnce()
    {
        if (!formation.TryPickThrow(out Alien target, out Alien neighbor)) yield break;

        neighbor.Flash(telegraphTime);
        target.Wobble(telegraphTime);
        yield return new WaitForSeconds(telegraphTime);

        // The player may have shot the target during the telegraph.
        if (target == null || !target.IsPresent) yield break;

        Vector3 origin = target.transform.position;
        Vector2 toPlayer = ((Vector2)(player.position - origin)).normalized;
        target.SetAway(true);

        thrownAlien = target;
        anyReturned = false;
        bitsInFlight = 2;
        SpawnBit(zeroSprite, target, origin + Vector3.left * bitSeparation, Rotate(toPlayer, -spreadAngle));
        SpawnBit(oneSprite, target, origin + Vector3.right * bitSeparation, Rotate(toPlayer, spreadAngle));

        yield return new WaitUntil(() => bitsInFlight == 0);
    }

    private void SpawnBit(Sprite glyph, Alien source, Vector3 position, Vector2 direction)
    {
        BitProjectile bit = Instantiate(bitPrefab, position, Quaternion.identity);
        bit.Launch(glyph, source.Color, direction, player, source.transform);
        bit.Resolved += OnBitResolved;
    }

    private void OnBitResolved(BitProjectile bit, BitProjectile.Outcome outcome)
    {
        bit.Resolved -= OnBitResolved;
        if (outcome == BitProjectile.Outcome.Returned) anyReturned = true;
        if (--bitsInFlight > 0) return;

        if (anyReturned) thrownAlien.SetAway(false);
        else thrownAlien.Kill();
        thrownAlien = null;
    }

    private static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;
}
