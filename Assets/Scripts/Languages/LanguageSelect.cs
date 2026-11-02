using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The start page shown on the terminal once the compile log has typed out: controls,
/// what the language's native abilities do, and how a run works. Java is the only
/// language for now, so there is no picker: SPACE or ENTER runs the first option.
/// (More options can be added to the array later and a picker brought back.)
/// GameManager owns the screen and decides what happens on start.
/// </summary>
public class LanguageSelect : MonoBehaviour
{
    [SerializeField] private LanguageOption[] options;

    [Header("Terminal colors (rich text)")]
    [SerializeField] private Color headingColor = new Color32(0x39, 0xd3, 0x53, 0xff);
    [SerializeField] private Color keyColor = new Color32(0xe3, 0xb3, 0x41, 0xff);
    [SerializeField] private Color textColor = new Color32(0xc9, 0xd1, 0xd9, 0xff);
    [SerializeField] private Color dimColor = new Color32(0x8b, 0x94, 0x9e, 0xff);

    public LanguageOption[] Options => options;
    public LanguageOption Language => options[0];

    /// <summary>True when the player starts the run (SPACE or ENTER).</summary>
    public bool HandleInput(Keyboard kb, out LanguageOption chosen)
    {
        chosen = null;
        if (!kb.spaceKey.wasPressedThisFrame && !kb.enterKey.wasPressedThisFrame) return false;
        chosen = Language;
        return true;
    }

    /// <summary>The start page, as rich text. <paramref name="header"/> is the one-line build summary.</summary>
    public string Render(string header)
    {
        LanguageOption java = Language;
        var sb = new StringBuilder();
        sb.Append(Color(dimColor, $"$ ./stack-invaders --lang={java.displayName.ToLowerInvariant()}")).Append('\n');
        sb.Append(Color(textColor, header)).Append("\n\n");

        Heading(sb, "// CONTROLS");
        Row(sb, "A / D", "move left / right");
        Row(sb, "SPACE", "throw coffee (hold to keep throwing)");
        Row(sb, "SHIFT", "breakpoint: parry an incoming shot back");
        sb.Append('\n');

        Heading(sb, $"// {java.displayName.ToUpperInvariant()} RUNTIME  ({java.tagline})");
        Row(sb, java.weaponName, java.weapon);
        Row(sb, java.passiveName, java.passive);
        Row(sb, java.abilityName, java.ability);
        sb.Append('\n');

        Heading(sb, "// GOAL");
        Line(sb, "clear 3 waves + 2 bosses; grab floppy disks for boons");
        sb.Append('\n');

        sb.Append(Color(headingColor, "> press ")).Append(Color(keyColor, "SPACE")).Append(Color(headingColor, " to run_"));
        return sb.ToString();
    }

    private void Heading(StringBuilder sb, string text) => sb.Append(Color(headingColor, text)).Append('\n');

    private void Row(StringBuilder sb, string key, string what) =>
        sb.Append("  ").Append(Color(keyColor, key.PadRight(11))).Append(Color(textColor, what)).Append('\n');

    private void Line(StringBuilder sb, string text) => sb.Append("  ").Append(Color(textColor, text)).Append('\n');

    private static string Color(Color c, string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";
}
