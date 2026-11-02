using System;
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

    /// <summary>One step of the run: a wave of formations, or a boss.</summary>
    [Serializable]
    public class Stage
    {
        public string name = "Wave 1";
        [Tooltip("Index into the WaveManager's waves, or -1 for a boss stage.")]
        public int waveIndex = 0;
        [Tooltip("Boss stage: a prefab whose root has an IBoss component.")]
        public GameObject bossPrefab;
        public Vector2 bossPosition = new Vector2(0f, 4f);
        [Tooltip("Terminal lines shown on the break before this stage (not used for the first stage).")]
        [TextArea] public string intro;

        public bool IsBoss => waveIndex < 0;
    }

    [SerializeField] private WaveManager waveManager;
    [Tooltip("The run, in order: waves and bosses.")]
    [SerializeField] private Stage[] stages;

    [Header("HUD")]
    [SerializeField] private BossHud bossHud;
    [Tooltip("Hidden while the boss HUD is shown.")]
    [SerializeField] private GameObject waveHud;
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
    private int stageIndex;
    private GameObject bossObject;
    private IBoss boss;

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
        SetGameplayRunning(false);
        StartStage(0); // spawned paused, visible behind the start screen
    }

    private void OnEnable()
    {
        waveManager.Stepped += CheckInvasion;
        waveManager.WaveCleared += OnWaveCleared;
        playerHealth.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        waveManager.Stepped -= CheckInvasion;
        waveManager.WaveCleared -= OnWaveCleared;
        playerHealth.Died -= OnPlayerDied;
    }

    private void Start()
    {
        fullTitleText =
            "$ ./stack-invaders\n" +
            "Compiling...\n" +
            "  [####################] 100%\n" +
            $"Build succeeded with {waveManager.TotalBugs} warnings (bugs) and {BossCount} errors (bosses)\n\n";
        ShowScreen("", titleColor);
        typing = StartCoroutine(TypeOut(fullTitleText));
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Debug: F10 clears the current stage (a wave or a boss).
        if (state == State.Playing && kb.f10Key.wasPressedThisFrame)
        {
            if (stages[stageIndex].IsBoss) StageCleared();
            else waveManager.DebugClearWave();
        }
#endif

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
        // Languages whose item is drawn into the developer's frames (Java's mug) have no held sprite.
        heldItem.sprite = chosen.heldItem;
        heldItem.enabled = chosen.heldItem != null;
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

    private int BossCount
    {
        get
        {
            int count = 0;
            foreach (Stage stage in stages) if (stage.IsBoss) count++;
            return count;
        }
    }

    /// <summary>Spawns stage <paramref name="index"/>, paused until gameplay resumes.</summary>
    private void StartStage(int index)
    {
        stageIndex = index;
        Stage stage = stages[index];

        if (!stage.IsBoss)
        {
            waveManager.StartWave(stage.waveIndex);
            waveHud.SetActive(true);
            bossHud.gameObject.SetActive(false);
            return;
        }

        bossObject = Instantiate(stage.bossPrefab, stage.bossPosition, Quaternion.identity);
        boss = bossObject.GetComponent<IBoss>();
        ((MonoBehaviour)boss).enabled = false; // paused behind the terminal
        boss.SetTarget(playerSprite.transform);
        boss.Defeated += StageCleared;
        waveHud.SetActive(false);
        bossHud.gameObject.SetActive(true);
        bossHud.Bind(boss);
    }

    private void OnWaveCleared(int wave, int total) => StageCleared();

    /// <summary>The current wave or boss is done: break, then the next stage, or the win.</summary>
    private void StageCleared()
    {
        if (state != State.Playing) return;

        if (boss != null)
        {
            boss.Defeated -= StageCleared;
            Destroy(bossObject);
            boss = null;
            bossObject = null;
        }

        string passed = stages[stageIndex].name;
        if (stageIndex + 1 >= stages.Length)
        {
            Win();
            return;
        }

        state = State.BetweenWaves;
        SetGameplayRunning(false);
        StartStage(stageIndex + 1);
        Stage next = stages[stageIndex];
        ShowScreen(
            $"{passed} passed\n\n" +
            next.intro +
            $"\n\n{next.name} incoming...",
            titleColor);
        StartCoroutine(ResumeAfterBreak());
    }

    private IEnumerator ResumeAfterBreak()
    {
        yield return new WaitForSecondsRealtime(betweenWavesTime);
        if (state != State.BetweenWaves) yield break;

        state = State.Playing;
        screenPanel.SetActive(false);
        // Re-enabling the language scripts resets per-stage state (JIT heat, GC timer).
        SetGameplayRunning(true);
    }

    private void Win()
    {
        state = State.Won;
        SetGameplayRunning(false);
        ShowScreen(
            "BUILD PASSING\n\n" +
            $"All tests passed: {waveManager.WaveCount} waves and {BossCount} bosses fixed\n" +
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
        if (boss != null) ((MonoBehaviour)boss).enabled = running;
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
