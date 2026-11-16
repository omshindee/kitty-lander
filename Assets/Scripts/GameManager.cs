using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs one level: counts fish, opens the pad, decides win/lose,
/// awards stars and saves progress. Per-level settings live here.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level")]
    [SerializeField] private string levelName = "First Flight";
    [SerializeField, TextArea] private string levelHint = "";
    [Tooltip("Seconds of main-thruster burn available at the start.")]
    [SerializeField] private float startFuel = 30f;
    [SerializeField] private float gravityScale = 0.7f;
    [Tooltip("Leaving this area counts as a crash.")]
    [SerializeField] private Rect levelBounds = new Rect(-14f, -14f, 28f, 22f);

    [Header("Stars (fraction of fuel left)")]
    [SerializeField, Range(0f, 1f)] private float threeStarFuel = 0.35f;
    [SerializeField, Range(0f, 1f)] private float twoStarFuel = 0.12f;

    public event EventHandler OnStateChanged;

    public enum Phase { Playing, Paused, Won, Lost }

    public Phase CurrentPhase { get; private set; } = Phase.Playing;
    public string LevelName => levelName;
    public string LevelHint => levelHint;
    public int LevelNumber { get; private set; }
    public bool IsLastLevel => LevelNumber >= Progress.LevelCount;
    public int TotalFish { get; private set; }
    public int CollectedFish { get; private set; }
    public float ElapsedTime { get; private set; }
    public int StarsEarned { get; private set; }
    public Rect LevelBounds => levelBounds;
    public string ResultMessage { get; private set; } = "";

    private Lander lander;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        LevelNumber = SceneManager.GetActiveScene().buildIndex;
    }

    private void Start()
    {
        lander = Lander.Instance;
        TotalFish = FindObjectsByType<Fish>(FindObjectsInactive.Exclude).Length;
        Fish.OnFishCollected += Fish_OnFishCollected;

        if (lander != null)
        {
            lander.Configure(startFuel, gravityScale);
            lander.OnCrash += Lander_OnCrash;
            lander.OnLanded += Lander_OnLanded;
        }

        if (TotalFish == 0 && Pad.Instance != null)
        {
            Pad.Instance.Show();
        }
    }

    private void OnDestroy()
    {
        Fish.OnFishCollected -= Fish_OnFishCollected;
        if (lander != null)
        {
            lander.OnCrash -= Lander_OnCrash;
            lander.OnLanded -= Lander_OnLanded;
        }
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        var kb = Keyboard.current;

        if (CurrentPhase == Phase.Playing && lander != null && lander.CurrentState == Lander.State.Flying)
        {
            ElapsedTime += Time.deltaTime;
            if (!levelBounds.Contains(lander.transform.position))
            {
                lander.ForceCrash("Lost in space! Stay inside the level.");
            }
        }

        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
        {
            if (CurrentPhase == Phase.Playing) SetPaused(true);
            else if (CurrentPhase == Phase.Paused) SetPaused(false);
        }
        if (kb.rKey.wasPressedThisFrame && CurrentPhase != Phase.Won)
        {
            Restart();
        }
        // Enter/Space activate the selected result-screen button via the UI, N is a shortcut.
        if (CurrentPhase == Phase.Won && kb.nKey.wasPressedThisFrame)
        {
            NextLevel();
        }
    }

    private void Fish_OnFishCollected(object sender, EventArgs e)
    {
        CollectedFish++;
        OnStateChanged?.Invoke(this, EventArgs.Empty);

        if (CollectedFish >= TotalFish && Pad.Instance != null)
        {
            Pad.Instance.Show();
        }
    }

    private void Lander_OnLanded(object sender, EventArgs e)
    {
        float fuelLeft = lander.FuelNormalized;
        StarsEarned = fuelLeft >= threeStarFuel ? 3 : fuelLeft >= twoStarFuel ? 2 : 1;
        Progress.CompleteLevel(LevelNumber, StarsEarned);
        ResultMessage = IsLastLevel
            ? "You landed every kitty! Thanks for playing."
            : "Purrfect landing!";
        StartCoroutine(SetPhaseDelayed(Phase.Won, 0.8f));
    }

    private void Lander_OnCrash(object sender, EventArgs e)
    {
        ResultMessage = lander.CrashReason;
        StartCoroutine(SetPhaseDelayed(Phase.Lost, 1.3f));
    }

    private IEnumerator SetPhaseDelayed(Phase phase, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        CurrentPhase = phase;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPaused(bool paused)
    {
        if (CurrentPhase != Phase.Playing && CurrentPhase != Phase.Paused) return;
        CurrentPhase = paused ? Phase.Paused : Phase.Playing;
        Time.timeScale = paused ? 0f : 1f;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Restart() => Progress.LoadLevel(LevelNumber);

    public void NextLevel()
    {
        if (IsLastLevel) Progress.LoadMenu();
        else Progress.LoadLevel(LevelNumber + 1);
    }

    public void MainMenu() => Progress.LoadMenu();

#if UNITY_EDITOR
    public void EditorSetup(string name, string hint, float fuel, float gravity, Rect bounds)
    {
        levelName = name;
        levelHint = hint;
        startFuel = fuel;
        gravityScale = gravity;
        levelBounds = bounds;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(levelBounds.center, levelBounds.size);
    }
#endif
}
