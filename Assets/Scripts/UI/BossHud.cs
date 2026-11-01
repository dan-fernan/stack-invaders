using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "recurse(3)  [##########--]" at the top of the screen during the miniboss, or
/// "StackOverflowError!" while it's stunned. Redrawn only when HP or the stun changes.
/// </summary>
public class BossHud : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private int barSegments = 12;

    private RecursionBoss boss;
    private string hpText = "";
    private bool overflowing;

    public void Bind(RecursionBoss target)
    {
        Unbind();
        boss = target;
        boss.RootHpChanged += OnHp;
        boss.OverflowChanged += OnOverflow;
        OnHp(boss.Root.Hp, boss.Root.MaxHp);
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
        int filled = Mathf.CeilToInt((float)hp / max * barSegments);
        hpText = $"recurse({boss.Root.N})  [" + new string('#', filled) + new string('-', barSegments - filled) + "]";
        Redraw();
    }

    private void OnOverflow(bool active)
    {
        overflowing = active;
        Redraw();
    }

    private void Redraw() => label.text = overflowing ? "StackOverflowError!" : hpText;
}
