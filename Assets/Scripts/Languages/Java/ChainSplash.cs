using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maven's transitive dependencies: after a coffee splash, a smaller splash hops to a
/// random nearby bug that wasn't already hit, then (at higher levels) hops again.
/// Lives on its own short-lived object because the cup that started it is destroyed.
/// </summary>
public class ChainSplash : MonoBehaviour
{
    private static readonly List<Collider2D> Found = new List<Collider2D>();

    /// <param name="alreadyHit">Targets the original splash hit (never chained to).</param>
    public static void Begin(Vector2 from, HashSet<IShootable> alreadyHit, int chains, float radius,
                             float searchRadius, float delay, OneShotAnimation effect, Color color)
    {
        var runner = new GameObject("MavenChain").AddComponent<ChainSplash>();
        runner.StartCoroutine(runner.Run(from, alreadyHit, chains, radius, searchRadius, delay, effect, color));
    }

    private IEnumerator Run(Vector2 from, HashSet<IShootable> hit, int chains, float radius,
                            float searchRadius, float delay, OneShotAnimation effect, Color color)
    {
        Vector2 center = from;
        for (int c = 0; c < chains; c++)
        {
            yield return new WaitForSeconds(delay);
            if (!TryPickTarget(center, searchRadius, hit, out Vector2 next)) break;
            center = next;

            Found.Clear();
            Physics2D.OverlapCircle(center, radius, ContactFilter2D.noFilter, Found);
            foreach (Collider2D col in Found)
            {
                if (IsChainTarget(col, out IShootable target) && hit.Add(target))
                    target.OnShot();
            }
            if (effect != null) effect.Spawn(center, color);
        }
        Destroy(gameObject);
    }

    /// <summary>A random bug (or boss part) near <paramref name="center"/> that wasn't hit yet.</summary>
    private static bool TryPickTarget(Vector2 center, float searchRadius, HashSet<IShootable> hit, out Vector2 position)
    {
        Found.Clear();
        Physics2D.OverlapCircle(center, searchRadius, ContactFilter2D.noFilter, Found);
        var options = new List<Vector2>();
        foreach (Collider2D col in Found)
            if (IsChainTarget(col, out IShootable target) && !hit.Contains(target))
                options.Add(col.transform.position);

        position = default;
        if (options.Count == 0) return false;
        position = options[Random.Range(0, options.Count)];
        return true;
    }

    /// <summary>Chains only to enemies (bugs and boss parts), never to projectiles.</summary>
    private static bool IsChainTarget(Collider2D col, out IShootable target)
    {
        target = null;
        if (col.TryGetComponent(out Alien alien) && alien.IsPresent) { target = alien; return true; }
        if (col.TryGetComponent(out IEnemyBody body)) { target = body; return true; }
        return false;
    }
}
