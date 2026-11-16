using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Title screen with Play, a level-select grid with stars, and progress reset.</summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private UISkin skin = new UISkin();
    [SerializeField] private string[] levelNames = new string[0];

    private Transform root;
    private GameObject mainPage;
    private GameObject levelsPage;

    public void EditorSetup(UISkin uiSkin, string[] names)
    {
        skin = uiSkin;
        levelNames = names;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        UIKit.ClickClip = skin.click;
        root = UIKit.CreateCanvas("Menu Canvas").transform;
        BuildMain();
        BuildLevels();
        ShowMain();
    }

    private void BuildMain()
    {
        var page = UIKit.Stretch(root, "Main");
        mainPage = page.gameObject;
        Vector2 c = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f);

        UIKit.Text(page, "Title", "KITTY LANDER", 150, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, top, top,
            new Vector2(0, -90), new Vector2(1600, 200), true).GetComponent<Outline>().effectDistance = new Vector2(6, -6);
        UIKit.Text(page, "Subtitle", "Grab every fish, then land the kitty gently on the pad.", 40, Color.white,
            TextAnchor.MiddleCenter, top, top, new Vector2(0, -270), new Vector2(1600, 60), true);

        int next = Mathf.Clamp(Progress.HighestUnlocked, 1, Mathf.Max(1, Progress.LevelCount));
        string playLabel = Progress.GetStars(1) > 0 ? $"Continue (Level {next})" : "Play";
        var play = UIKit.Button(page, skin, playLabel, UIKit.Green, c, new Vector2(0, -20), new Vector2(560, 120),
            () => Progress.LoadLevel(next), 52);
        UIKit.Button(page, skin, "Levels", UIKit.Orange, c, new Vector2(0, -160), new Vector2(560, 110), ShowLevels, 48);
        UIKit.Button(page, skin, "Quit", UIKit.Blue, c, new Vector2(0, -290), new Vector2(560, 100), Quit, 44);

        // star total
        var starRow = UIKit.Rect(page, "Stars", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(330, 100));
        UIKit.Image(starRow, skin.panel, new Color(1, 1, 1, 0.92f));
        var starIcon = UIKit.Rect(starRow, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(72, 72));
        UIKit.Image(starIcon, skin.star, Color.white);
        UIKit.Text(starRow, "Count", $"{Progress.TotalStars} / {Progress.LevelCount * 3}", 44, UIKit.Ink, TextAnchor.MiddleLeft,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(104, 0), new Vector2(220, 80));

        UIKit.Text(page, "Footer", "W / Up / Space: thrust     A D / Left Right: steer     R: restart     Esc: pause", 28,
            Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1600, 50), true);

        mainPage.SetActive(false);
        play.gameObject.name = "PlayButton";
    }

    private void BuildLevels()
    {
        var page = UIKit.Stretch(root, "Levels");
        levelsPage = page.gameObject;
        Vector2 c = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f);
        UIKit.Text(page, "Title", "Choose a Level", 96, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, top, top,
            new Vector2(0, -60), new Vector2(1400, 130), true);

        int count = Progress.LevelCount;
        const int cols = 6;
        Vector2 cell = new Vector2(250, 250);
        int rows = Mathf.CeilToInt(count / (float)cols);
        for (int i = 0; i < count; i++)
        {
            int level = i + 1;
            int row = i / cols, col = i % cols;
            int inRow = Mathf.Min(cols, count - row * cols);
            float x = (col - (inRow - 1) * 0.5f) * (cell.x + 24);
            float y = ((rows - 1) * 0.5f - row) * (cell.y + 30) + 10;
            bool unlocked = Progress.IsUnlocked(level);
            var btn = UIKit.Button(page, skin, unlocked ? level.ToString() : "", unlocked ? UIKit.Orange : UIKit.Grey, c,
                new Vector2(x, y), cell, () => Progress.LoadLevel(level), 90);
            btn.interactable = unlocked;
            var label = btn.GetComponentInChildren<Text>();
            label.rectTransform.anchoredPosition = new Vector2(0, 26);

            string name = i < levelNames.Length ? levelNames[i] : "";
            UIKit.Text(btn.transform, "Name", name, 24, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 76), new Vector2(230, 56), true);

            if (!unlocked)
            {
                var paw = UIKit.Rect(btn.transform, "Lock", c, c, new Vector2(0, 28), new Vector2(96, 96));
                UIKit.Image(paw, skin.paw, new Color(1, 1, 1, 0.75f));
                continue;
            }
            int stars = Progress.GetStars(level);
            for (int s = 0; s < 3; s++)
            {
                var st = UIKit.Rect(btn.transform, "Star" + s, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2((s - 1) * 58, 16), new Vector2(54, 54));
                UIKit.Image(st, s < stars ? skin.star : skin.starEmpty, Color.white);
            }
        }

        UIKit.Button(page, skin, "Back", UIKit.Blue, new Vector2(0.5f, 0), new Vector2(-190, 80), new Vector2(340, 96), ShowMain, 42);
        UIKit.Button(page, skin, "Reset Progress", UIKit.Grey, new Vector2(0.5f, 0), new Vector2(190, 80), new Vector2(340, 96), () =>
        {
            Progress.ResetAll();
            Progress.LoadMenu();
        }, 34);
        levelsPage.SetActive(false);
    }

    private void ShowMain()
    {
        levelsPage.SetActive(false);
        mainPage.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(mainPage.transform.Find("PlayButton")?.gameObject);
    }

    private void ShowLevels()
    {
        mainPage.SetActive(false);
        levelsPage.SetActive(true);
        var first = levelsPage.GetComponentInChildren<Button>();
        EventSystem.current?.SetSelectedGameObject(first != null ? first.gameObject : null);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
