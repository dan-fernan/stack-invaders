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
    private enum State { Title, Playing, BetweenWaves, GameOver, Won }

    [SerializeField] private WaveManager waveManager;

    [Header("Miniboss (after the last wave)")]
    [SerializeField] private RecursionBoss bossPrefab;
    [Tooltip("World position of the boss's root frame.")]
    [SerializeField] private Vector2 bossPosition = new Vector2(0f, 4f);
    [SerializeField] private BossHud bossHud;
    [Tooltip("Hidden while the boss HUD is shown.")]
    [SerializeField] private GameObject waveHud;
    [TextArea]
    [SerializeField] private string bossIntro = "$ ./recurse 3\nwarning: recursive call without a depth check";
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private SpriteRenderer playerSprite;
    [Tooltip("Gameplay scripts that only run while Playing.")]
    [FormerlySerializedAs("disableOnGameOver")]
    [SerializeField] private MonoBehaviour[] gameplayBehaviours;

    [Header("Language select")]
    [SerializeField] private LanguageSelect languageSelect;
    [Tooltip("Sprite renderer for the item the developer holds.")]
    [SerializeField] private SpriteRenderer heldItem;

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
    [Tooltip("Seconds the between-waves terminal stays up.")]
    [SerializeField] private float betweenWavesTime = 2f;

    private State state = State.Title;
    private Coroutine typing;
    private string fullTitleText;
    private LanguageOption language;
    private RecursionBoss boss;

    private void Awake()
    {
        // Every language's weapon, abilities and HUD stay off until one is picked.
        foreach (LanguageOption option in languageSelect.Options)
        {
            foreach (MonoBehaviour behaviour in option.behaviours)
                behaviour.enabled = false;
            if (option.hud != null) option.hud.SetActive(false);
        }
        heldItem.enabled = false;
        bossHud.gameObject.SetActive(false);
        SetGameplayRunning(false);
        waveManager.StartWave(0); // spawned paused, visible behind the start screen
    }

    private void OnEnable()
    {
        waveManager.Stepped += CheckInvasion;
        waveManager.WaveCleared += OnWaveCleared;
        waveManager.AllWavesCleared += OnAllWavesCleared;
        playerHealth.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        waveManager.Stepped -= CheckInvasion;
        waveManager.WaveCleared -= OnWaveCleared;
        waveManager.AllWavesCleared -= OnAllWavesCleared;
        playerHealth.Died -= OnPlayerDied;
    }

    private void Start()
    {
        fullTitleText =
            "$ ./stack-invaders\n" +
            "Compiling...\n" +
            "  [####################] 100%\n" +
            $"Build succeeded with {waveManager.TotalBugs} warnings (bugs) in {waveManager.WaveCount} waves\n\n";
        ShowScreen("", titleColor);
        typing = StartCoroutine(TypeOut(fullTitleText));
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        switch (state)
        {
            case State.Title when typing != null:
                if (kb.spaceKey.wasPressedThisFrame) FinishTyping(); // skip the animation
                break;

            case State.Title:
                if (languageSelect.HandleInput(kb, out LanguageOption chosen, out bool changed))
                    StartGame(chosen);
                else if (changed)
                    ShowTitleMenu();
                break;

            case State.GameOver when kb.rKey.wasPressedThisFrame:
            case State.Won when kb.rKey.wasPressedThisFrame:
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;
        }
    }

    private void ShowTitleMenu() => screenText.text = fullTitleText + languageSelect.Render();

    private void StartGame(LanguageOption chosen)
    {
        language = chosen;
        heldItem.sprite = chosen.heldItem;
        heldItem.enabled = true;
        // HUD first, so it is subscribed before the weapon publishes its initial state.
        if (chosen.hud != null) chosen.hud.SetActive(true);

        state = State.Playing;
        screenPanel.SetActive(false);
        SetGameplayRunning(true);
    }

    private void CheckInvasion()
    {
        if (state != State.Playing) return;

        // Any formation reaching the developer ends the game (LowestY covers all of them).
        if (waveManager.LowestY <= playerSprite.bounds.max.y)
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

    private void OnWaveCleared(int cleared, int total)
    {
        if (state != State.Playing) return;

        state = State.BetweenWaves;
        SetGameplayRunning(false);
        int next = cleared + 1;
        waveManager.StartWave(next); // spawned paused behind the terminal
        ShowScreen(
            $"Wave {cleared + 1}/{total} passed\n\n" +
            waveManager.Current.intro +
            $"\n\nWave {next + 1}/{total} incoming...",
            titleColor);
        StartCoroutine(ResumeAfterBreak());
    }

    private IEnumerator ResumeAfterBreak()
    {
        yield return new WaitForSecondsRealtime(betweenWavesTime);
        if (state != State.BetweenWaves) yield break;

        state = State.Playing;
        screenPanel.SetActive(false);
        // Re-enabling the language scripts resets per-wave state (JIT heat, GC timer).
        SetGameplayRunning(true);
    }

    /// <summary>All waves cleared: break, then the recursion miniboss.</summary>
    private void OnAllWavesCleared()
    {
        if (state != State.Playing) return;

        state = State.BetweenWaves;
        SetGameplayRunning(false);

        boss = Instantiate(bossPrefab, bossPosition, Quaternion.identity);
        boss.enabled = false; // paused behind the terminal
        boss.Defeated += OnBossDefeated;
        waveHud.SetActive(false);
        bossHud.gameObject.SetActive(true);
        bossHud.Bind(boss);

        ShowScreen(
            $"All {waveManager.WaveCount}/{waveManager.WaveCount} waves passed\n\n" +
            bossIntro +
            "\n\nMiniboss incoming...",
            titleColor);
        StartCoroutine(ResumeAfterBreak());
    }

    private void OnBossDefeated()
    {
        if (state != State.Playing) return;

        state = State.Won;
        SetGameplayRunning(false);
        ShowScreen(
            "BUILD PASSING\n\n" +
            $"All tests passed: {waveManager.WaveCount} waves of bugs fixed\n" +
            "recurse(3) returned: call stack unwound\n" +
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
        waveManager.SetRunning(running);
        if (boss != null) boss.enabled = running;
        if (language != null)
            foreach (MonoBehaviour behaviour in language.behaviours)
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
        ShowTitleMenu();
    }

    private void FinishTyping()
    {
        StopCoroutine(typing);
        typing = null;
        ShowTitleMenu();
    }
}
