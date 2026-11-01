using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Owns the game flow (start screen -> playing -> win / game over) and the win/lose rules.
///
/// Assignment requirement: "Detect when the aliens have reached a point on the screen
/// that the game is no longer possible to win." Once the lowest bug reaches the top of
/// the ship it can no longer be shot before it collides, so the game ends immediately,
/// regardless of hearts remaining.
/// </summary>
[DefaultExecutionOrder(-100)] // freeze gameplay before anything else starts
public class GameManager : MonoBehaviour
{
    private enum State { Title, Playing, GameOver, Won }

    [SerializeField] private AlienFormation formation;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private SpriteRenderer playerSprite;
    [Tooltip("Gameplay scripts that only run while Playing.")]
    [FormerlySerializedAs("disableOnGameOver")]
    [SerializeField] private MonoBehaviour[] gameplayBehaviours;

    [Header("Terminal screen (start / win / game over)")]
    [FormerlySerializedAs("gameOverPanel")]
    [SerializeField] private GameObject screenPanel;
    [FormerlySerializedAs("gameOverText")]
    [SerializeField] private Text screenText;
    [SerializeField] private Color titleColor = new Color32(0x39, 0xd3, 0x53, 0xff);
    [SerializeField] private Color winColor = new Color32(0x39, 0xd3, 0x53, 0xff);
    [SerializeField] private Color loseColor = new Color32(0xf8, 0x51, 0x49, 0xff);
    [Tooltip("Seconds per character for the start screen's typing effect.")]
    [SerializeField] private float typeDelay = 0.02f;

    private State state = State.Title;
    private Coroutine typing;
    private string fullTitleText;

    private void Awake()
    {
        SetGameplayRunning(false);
    }

    private void OnEnable()
    {
        formation.Stepped += CheckInvasion;
        formation.Cleared += OnFormationCleared;
        playerHealth.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        formation.Stepped -= CheckInvasion;
        formation.Cleared -= OnFormationCleared;
        playerHealth.Died -= OnPlayerDied;
    }

    private void Start()
    {
        fullTitleText =
            "$ ./stack-invaders\n" +
            "Compiling...\n" +
            "  [####################] 100%\n" +
            $"Build succeeded with {formation.AliveCount} warnings (bugs)\n\n" +
            "> press SPACE to run";
        ShowScreen("", titleColor);
        typing = StartCoroutine(TypeOut(fullTitleText));
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        switch (state)
        {
            case State.Title when kb.spaceKey.wasPressedThisFrame:
                if (typing != null) FinishTyping(); // first press skips the animation
                else StartGame();
                break;

            case State.GameOver when kb.rKey.wasPressedThisFrame:
            case State.Won when kb.rKey.wasPressedThisFrame:
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;
        }
    }

    private void StartGame()
    {
        state = State.Playing;
        screenPanel.SetActive(false);
        SetGameplayRunning(true);
    }

    private void CheckInvasion()
    {
        if (state != State.Playing || formation.AliveCount == 0) return;

        if (formation.LowestY <= playerSprite.bounds.max.y)
        {
            GameOver(
                "Unhandled exception: BugsInProductionException",
                "  at Formation.March() reached the deploy line",
                "  at Developer.Ship() 0 tests caught it");
        }
    }

    private void OnPlayerDied()
    {
        if (state != State.Playing) return;

        GameOver(
            "Unhandled exception: OutOfHeartsException",
            "  at Developer.TakeHit() hearts == 0",
            "  at Program.Main() no try/catch left");
    }

    private void OnFormationCleared()
    {
        if (state != State.Playing) return;

        state = State.Won;
        SetGameplayRunning(false);
        ShowScreen(
            "BUILD PASSING\n\n" +
            "All tests passed: every bug fixed\n" +
            $"Unhandled exceptions remaining: {playerHealth.Hearts}/{playerHealth.MaxHearts}\n\n" +
            "Process exited with code 0\n\n" +
            "> press R to run again",
            winColor);
    }

    private void GameOver(params string[] stackTrace)
    {
        state = State.GameOver;
        SetGameplayRunning(false);
        ShowScreen(
            "GAME OVER\n\n" +
            string.Join("\n", stackTrace) +
            "\n\nProcess exited with code 1\n\n> press R to recompile",
            loseColor);
    }

    /// <summary>
    /// Freezes or resumes the game. timeScale stops physics, timers and bits in flight;
    /// disabling the gameplay scripts stops input and the fleet's throw loop.
    /// timeScale is static and survives scene reloads, so every load sets it here.
    /// </summary>
    private void SetGameplayRunning(bool running)
    {
        Time.timeScale = running ? 1f : 0f;
        foreach (MonoBehaviour behaviour in gameplayBehaviours)
            behaviour.enabled = running;
    }

    private void ShowScreen(string text, Color color)
    {
        screenText.text = text;
        screenText.color = color;
        screenPanel.SetActive(true);
    }

    /// <summary>Types the text one character at a time (real time, since the game is frozen).</summary>
    private IEnumerator TypeOut(string text)
    {
        var shown = new StringBuilder(text.Length);
        var wait = new WaitForSecondsRealtime(typeDelay);
        foreach (char c in text)
        {
            shown.Append(c);
            screenText.text = shown.ToString();
            yield return c == '\n' ? new WaitForSecondsRealtime(typeDelay * 10f) : wait;
        }
        typing = null;
    }

    private void FinishTyping()
    {
        StopCoroutine(typing);
        typing = null;
        screenText.text = fullTitleText;
    }
}
