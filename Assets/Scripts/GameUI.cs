using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>In-level HUD (fuel, fish, speed) plus the pause, win and crash screens.</summary>
public class GameUI : MonoBehaviour
{
    [SerializeField] private UISkin skin = new UISkin();

    private GameManager gm;
    private Lander lander;

    private RectTransform fuelFill;
    private Image fuelFillImage;
    private Text fishText;
    private Text speedText;
    private Text timeText;
    private Text centerText;
    private Text subText;
    private Text toastText;
    private CanvasGroup hintGroup;

    private GameObject pausePanel;
    private GameObject resultPanel;
    private Text resultTitle;
    private Text resultMessage;
    private Text resultStats;
    private Image[] resultStars;
    private Button resultPrimary;
    private Text resultPrimaryLabel;

    private Coroutine toastRoutine;

    public void EditorSetup(UISkin uiSkin) => skin = uiSkin;

    private void Start()
    {
        gm = GameManager.Instance;
        lander = Lander.Instance;
        UIKit.ClickClip = skin.click;
        Build();

        if (gm != null) gm.OnStateChanged += OnStateChanged;
        if (lander != null)
        {
            lander.OnLaunch += OnLaunch;
            lander.OnRefuel += OnRefuel;
        }
        Fish.OnFishCollected += OnFishCollected;
        Pad.OnPadShown += OnPadShown;
        RefreshFish();
    }

    private void OnDestroy()
    {
        if (gm != null) gm.OnStateChanged -= OnStateChanged;
        if (lander != null)
        {
            lander.OnLaunch -= OnLaunch;
            lander.OnRefuel -= OnRefuel;
        }
        Fish.OnFishCollected -= OnFishCollected;
        Pad.OnPadShown -= OnPadShown;
    }

    // ------------------------------------------------------------- building

    private void Build()
    {
        var canvas = UIKit.CreateCanvas("HUD Canvas");
        Transform root = canvas.transform;
        Vector2 tl = new Vector2(0, 1), tr = new Vector2(1, 1), c = new Vector2(0.5f, 0.5f), bc = new Vector2(0.5f, 0);

        // ---- top-left info panel
        var info = UIKit.Rect(root, "Info", tl, new Vector2(0, 1), new Vector2(24, -24), new Vector2(560, 196));
        UIKit.Image(info, skin.panel, new Color(1, 1, 1, 0.92f));
        string title = gm != null ? $"Level {gm.LevelNumber}: {gm.LevelName}" : "Kitty Lander";
        UIKit.Text(info, "Title", title, 34, UIKit.Ink, TextAnchor.MiddleLeft, tl, new Vector2(0, 1), new Vector2(28, -18), new Vector2(520, 48));

        var fuelIcon = UIKit.Rect(info, "FuelIcon", tl, new Vector2(0, 1), new Vector2(24, -74), new Vector2(52, 52));
        UIKit.Image(fuelIcon, skin.fuelIcon, Color.white);
        var barBg = UIKit.Rect(info, "FuelBar", tl, new Vector2(0, 1), new Vector2(90, -86), new Vector2(430, 30));
        UIKit.Image(barBg, null, UIKit.Ink);
        fuelFill = UIKit.Rect(barBg, "Fill", new Vector2(0, 0), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        fuelFill.anchorMin = new Vector2(0, 0);
        fuelFill.anchorMax = new Vector2(1, 1);
        fuelFill.offsetMin = new Vector2(4, 4);
        fuelFill.offsetMax = new Vector2(-4, -4);
        fuelFillImage = UIKit.Image(fuelFill, null, UIKit.Green);

        var fishIcon = UIKit.Rect(info, "FishIcon", tl, new Vector2(0, 1), new Vector2(22, -134), new Vector2(56, 48));
        UIKit.Image(fishIcon, skin.fishIcon, Color.white);
        fishText = UIKit.Text(info, "Fish", "0 / 0", 34, UIKit.Ink, TextAnchor.MiddleLeft, tl, new Vector2(0, 1),
            new Vector2(90, -134), new Vector2(430, 48));

        // ---- top-right flight panel
        var flight = UIKit.Rect(root, "Flight", tr, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(330, 130));
        UIKit.Image(flight, skin.panel, new Color(1, 1, 1, 0.92f));
        speedText = UIKit.Text(flight, "Speed", "Speed 0.0", 34, UIKit.Ink, TextAnchor.MiddleLeft, tl, new Vector2(0, 1),
            new Vector2(28, -18), new Vector2(290, 46));
        timeText = UIKit.Text(flight, "Time", "Time 0.0s", 30, UIKit.Ink, TextAnchor.MiddleLeft, tl, new Vector2(0, 1),
            new Vector2(28, -68), new Vector2(290, 46));

        // ---- centre messages
        centerText = UIKit.Text(root, "Center", "Press W / Up to launch!", 64, Color.white, TextAnchor.MiddleCenter, c, c,
            new Vector2(0, 140), new Vector2(1500, 100), true);
        subText = UIKit.Text(root, "Sub", gm != null ? gm.LevelHint : "", 36, Color.white, TextAnchor.UpperCenter, c, c,
            new Vector2(0, 50), new Vector2(1300, 120), true);
        toastText = UIKit.Text(root, "Toast", "", 46, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter, c, c,
            new Vector2(0, 260), new Vector2(1200, 80), true);

        // ---- controls hint
        var hint = UIKit.Rect(root, "Hint", bc, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1400, 56));
        hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        UIKit.Text(hint, "Text", "W / Up / Space: thrust     A D / Left Right: steer     R: restart     Esc: pause", 28,
            Color.white, TextAnchor.MiddleCenter, c, c, Vector2.zero, new Vector2(1400, 56), true);

        BuildPausePanel(root);
        BuildResultPanel(root);
    }

    private RectTransform Overlay(Transform root, string name, out GameObject go)
    {
        var dim = UIKit.Stretch(root, name);
        UIKit.Image(dim, null, new Color(0.05f, 0.04f, 0.1f, 0.55f));
        go = dim.gameObject;
        var panel = UIKit.Rect(dim, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 600));
        UIKit.Image(panel, skin.panel, Color.white);
        go.SetActive(false);
        return panel;
    }

    private void BuildPausePanel(Transform root)
    {
        var p = Overlay(root, "Pause", out pausePanel);
        Vector2 top = new Vector2(0.5f, 1), mid = new Vector2(0.5f, 0.5f);
        UIKit.Text(p, "Title", "Paused", 72, UIKit.Orange, TextAnchor.MiddleCenter, top, top, new Vector2(0, -40), new Vector2(700, 100), true);
        UIKit.Button(p, skin, "Resume", UIKit.Green, mid, new Vector2(0, 60), new Vector2(420, 96), () => gm.SetPaused(false));
        UIKit.Button(p, skin, "Restart", UIKit.Orange, mid, new Vector2(0, -50), new Vector2(420, 96), () => gm.Restart());
        UIKit.Button(p, skin, "Main Menu", UIKit.Blue, mid, new Vector2(0, -160), new Vector2(420, 96), () => gm.MainMenu());
    }

    private void BuildResultPanel(Transform root)
    {
        var p = Overlay(root, "Result", out resultPanel);
        Vector2 top = new Vector2(0.5f, 1), mid = new Vector2(0.5f, 0.5f), bot = new Vector2(0.5f, 0);
        resultTitle = UIKit.Text(p, "Title", "", 68, UIKit.Orange, TextAnchor.MiddleCenter, top, top, new Vector2(0, -36), new Vector2(720, 96), true);
        resultMessage = UIKit.Text(p, "Message", "", 34, UIKit.Ink, TextAnchor.MiddleCenter, top, top, new Vector2(0, -130), new Vector2(680, 90));

        resultStars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var s = UIKit.Rect(p, "Star" + i, mid, mid, new Vector2((i - 1) * 130, i == 1 ? 50 : 30), new Vector2(120, 120));
            resultStars[i] = UIKit.Image(s, skin.starEmpty, Color.white);
        }
        resultStats = UIKit.Text(p, "Stats", "", 30, UIKit.Ink, TextAnchor.MiddleCenter, mid, mid, new Vector2(0, -50), new Vector2(680, 50));

        resultPrimary = UIKit.Button(p, skin, "Next Level", UIKit.Green, bot, new Vector2(0, 170), new Vector2(460, 96), OnPrimary);
        resultPrimaryLabel = resultPrimary.GetComponentInChildren<Text>();
        UIKit.Button(p, skin, "Retry", UIKit.Orange, bot, new Vector2(-120, 64), new Vector2(220, 84), () => gm.Restart(), 34);
        UIKit.Button(p, skin, "Menu", UIKit.Blue, bot, new Vector2(120, 64), new Vector2(220, 84), () => gm.MainMenu(), 34);
    }

    // --------------------------------------------------------------- update

    private void Update()
    {
        if (lander != null)
        {
            float f = lander.FuelNormalized;
            fuelFill.anchorMax = new Vector2(Mathf.Max(0.001f, f), 1);
            fuelFillImage.color = f > 0.5f ? UIKit.Green : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(1f, 0.8f, 0.2f), f * 2f);

            float spd = lander.CurrentState == Lander.State.Flying ? lander.Speed : 0f;
            bool safe = spd <= lander.MaxLandingSpeed;
            speedText.text = $"Speed {spd:0.0}";
            speedText.color = safe ? new Color(0.2f, 0.55f, 0.2f) : new Color(0.85f, 0.2f, 0.15f);

            if (lander.CurrentState == Lander.State.Flying && lander.Fuel <= 0f)
            {
                centerText.text = "Out of fuel!";
            }
        }
        if (gm != null) timeText.text = $"Time {gm.ElapsedTime:0.0}s";
        RefreshFish();
    }

    // --------------------------------------------------------------- events

    private void OnLaunch(object sender, EventArgs e)
    {
        centerText.text = "";
        subText.text = "";
        StartCoroutine(FadeHint());
    }

    private IEnumerator FadeHint()
    {
        yield return new WaitForSeconds(4f);
        for (float t = 1f; t > 0f; t -= Time.deltaTime)
        {
            hintGroup.alpha = t;
            yield return null;
        }
        hintGroup.alpha = 0f;
    }

    private void OnFishCollected(object sender, EventArgs e)
    {
        RefreshFish();
        if (gm != null && gm.CollectedFish < gm.TotalFish)
        {
            int left = gm.TotalFish - gm.CollectedFish;
            Toast(left == 1 ? "Yum! 1 fish to go" : $"Yum! {left} fish to go");
        }
    }

    private void OnPadShown(object sender, EventArgs e)
    {
        if (gm != null && gm.TotalFish > 0) Toast("All fish collected! The landing pad is open!", 3f);
    }

    private void OnRefuel(object sender, float amount) => Toast("Fuel refilled!");

    private void RefreshFish()
    {
        if (gm == null) return;
        fishText.text = $"{gm.CollectedFish} / {gm.TotalFish}";
    }

    private void Toast(string msg, float duration = 1.6f)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(msg, duration));
    }

    private IEnumerator ToastRoutine(string msg, float duration)
    {
        toastText.text = msg;
        var col = toastText.color;
        col.a = 1f;
        toastText.color = col;
        yield return new WaitForSeconds(duration);
        for (float t = 0.4f; t > 0f; t -= Time.deltaTime)
        {
            col.a = t / 0.4f;
            toastText.color = col;
            yield return null;
        }
        toastText.text = "";
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        RefreshFish();
        pausePanel.SetActive(gm.CurrentPhase == GameManager.Phase.Paused);
        if (pausePanel.activeSelf) Select(pausePanel.GetComponentInChildren<Button>());

        bool won = gm.CurrentPhase == GameManager.Phase.Won;
        bool lost = gm.CurrentPhase == GameManager.Phase.Lost;
        resultPanel.SetActive(won || lost);
        if (!won && !lost) return;

        centerText.text = "";
        resultTitle.text = won ? (gm.IsLastLevel ? "You did it!" : "Purrfect!") : "Crashed!";
        resultTitle.color = won ? UIKit.Green : new Color(0.88f, 0.3f, 0.25f);
        resultMessage.text = gm.ResultMessage;
        for (int i = 0; i < 3; i++)
        {
            resultStars[i].gameObject.SetActive(won);
            resultStars[i].sprite = i < gm.StarsEarned ? skin.star : skin.starEmpty;
        }
        resultStats.text = won
            ? $"Fuel left {Mathf.RoundToInt(lander.FuelNormalized * 100)}%     Time {gm.ElapsedTime:0.0}s"
            : $"Fish collected {gm.CollectedFish} / {gm.TotalFish}";
        resultPrimaryLabel.text = won ? (gm.IsLastLevel ? "Main Menu" : "Next Level") : "Try Again";
        Select(resultPrimary);
        if (won) StartCoroutine(PopStars());
    }

    private IEnumerator PopStars()
    {
        foreach (var s in resultStars) s.transform.localScale = Vector3.zero;
        for (int i = 0; i < 3; i++)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 5f)
            {
                float k = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
                resultStars[i].transform.localScale = Vector3.one * k;
                yield return null;
            }
            resultStars[i].transform.localScale = Vector3.one;
        }
    }

    private void OnPrimary()
    {
        if (gm.CurrentPhase == GameManager.Phase.Won) gm.NextLevel();
        else gm.Restart();
    }

    private static void Select(Selectable s)
    {
        if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}
