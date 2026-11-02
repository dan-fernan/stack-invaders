using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The language select menu shown on the start terminal. Owns the options and the
/// highlighted entry; GameManager owns the screen and decides what happens on pick.
/// </summary>
public class LanguageSelect : MonoBehaviour
{
    [SerializeField] private LanguageOption[] options;
    [Tooltip("Shown under every language's description.")]
    [SerializeField] private string sharedAbility = "all languages: SHIFT = breakpoint parry";

    public LanguageOption[] Options => options;

    private int selected;
    private string error;

    /// <summary>
    /// Handles menu keys. Returns true when an unlocked language is confirmed.
    /// Sets <paramref name="changed"/> when the menu needs to be redrawn.
    /// </summary>
    public bool HandleInput(Keyboard kb, out LanguageOption chosen, out bool changed)
    {
        chosen = null;
        changed = false;

        int move = 0;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) move = -1;
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) move = 1;
        if (move != 0)
        {
            selected = (selected + move + options.Length) % options.Length;
            error = null;
            changed = true;
        }

        // Number keys jump straight to an entry.
        for (int i = 0; i < options.Length && i < 9; i++)
        {
            if (kb[Key.Digit1 + i].wasPressedThisFrame)
            {
                selected = i;
                error = null;
                changed = true;
                return TryChoose(out chosen);
            }
        }

        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
        {
            changed = true;
            return TryChoose(out chosen);
        }
        return false;
    }

    private bool TryChoose(out LanguageOption chosen)
    {
        LanguageOption option = options[selected];
        if (option.locked)
        {
            error = $"error: no compiler found for {option.displayName} (coming soon)";
            chosen = null;
            return false;
        }
        chosen = option;
        return true;
    }

    public string Render()
    {
        var sb = new StringBuilder("Select a language:\n");
        for (int i = 0; i < options.Length; i++)
        {
            LanguageOption o = options[i];
            sb.Append(i == selected ? "> " : "  ");
            sb.Append($"[{i + 1}] {o.displayName,-7} ");
            sb.Append(o.locked ? "(coming soon)" : o.tagline);
            sb.Append('\n');
        }
        sb.Append('\n');
        AppendDescription(sb, options[selected]);
        sb.Append('\n');
        sb.Append(error ?? "W/S to choose, SPACE to run");
        return sb.ToString();
    }

    /// <summary>What the highlighted language plays like: weapon, passive, ability.</summary>
    private void AppendDescription(StringBuilder sb, LanguageOption o)
    {
        if (o.locked)
        {
            sb.Append($"  {o.displayName}: not implemented yet\n\n\n");
        }
        else
        {
            sb.Append($"  weapon   {o.weapon}\n");
            sb.Append($"  passive  {o.passive}\n");
            sb.Append($"  ability  {o.ability}\n");
        }
        sb.Append($"  {sharedAbility}\n");
    }
}
