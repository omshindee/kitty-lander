using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// End-to-end smoke tests: every level in the build loads, the HUD appears,
/// collecting all fish opens the pad, a gentle landing wins, and crashing loses.
/// Hazards are switched off for the playthrough (their placement is checked by
/// Kitty Lander > Validate Levels); these tests cover the game loop itself.
/// Note: completing levels writes to this project's editor PlayerPrefs; they are reset afterwards.
/// </summary>
public class LevelPlaythroughTests
{
    private static IEnumerable<int> Levels()
    {
        // Build settings: 0 = main menu, 1..N = levels. Scene count isn't known at
        // test-discovery time in every context, so enumerate a generous range and skip extras.
        for (int i = 1; i <= 11; i++) yield return i;
    }

    [OneTimeTearDown]
    public void ResetProgress()
    {
        Progress.ResetAll();
        Time.timeScale = 1f;
    }

    private static IEnumerator Load(int buildIndex)
    {
        SceneManager.LoadScene(buildIndex);
        yield return null;
        yield return null;
    }

    private static IEnumerator Fixed(int n)
    {
        for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
    }

    private static void Teleport(Lander lander, Vector2 pos, Vector2 velocity)
    {
        var rb = lander.GetComponent<Rigidbody2D>();
        rb.position = pos;
        rb.rotation = 0f;
        rb.linearVelocity = velocity;
        rb.angularVelocity = 0f;
        lander.transform.position = pos;
    }

    [UnityTest]
    public IEnumerator Level_CanBeCompleted([ValueSource(nameof(Levels))] int level)
    {
        if (level >= SceneManager.sceneCountInBuildSettings) Assert.Ignore("No level " + level);
        yield return Load(level);

        var gm = GameManager.Instance;
        var lander = Lander.Instance;
        Assert.IsNotNull(gm, "GameManager missing");
        Assert.IsNotNull(lander, "Lander missing");
        Assert.IsNotNull(Pad.Instance, "Pad missing");
        Assert.IsNotNull(GameObject.Find("HUD Canvas"), "HUD not built");
        Assert.AreEqual(level, gm.LevelNumber);
        Assert.AreEqual(Lander.State.Ready, lander.CurrentState);
        Assert.Greater(gm.TotalFish, 0, "level has no fish");
        Assert.IsFalse(Pad.Instance.gameObject.activeSelf, "pad should be hidden until all fish are collected");
        Assert.IsTrue(gm.LevelBounds.Contains(lander.transform.position), "lander starts outside the level bounds");

        foreach (var h in Object.FindObjectsByType<Hazard>(FindObjectsInactive.Exclude)) h.gameObject.SetActive(false);

        lander.Launch();
        Assert.AreEqual(Lander.State.Flying, lander.CurrentState);

        var fish = Object.FindObjectsByType<Fish>(FindObjectsInactive.Exclude).ToList();
        foreach (var f in fish)
        {
            if (f == null) continue; // already picked up on the way
            Vector2 fishPos = f.transform.position;
            Teleport(lander, fishPos, Vector2.zero);
            yield return Fixed(3);
            Assert.AreEqual(Lander.State.Flying, lander.CurrentState, $"crashed while collecting a fish at {fishPos}: {lander.CrashReason}");
        }
        Assert.AreEqual(gm.TotalFish, gm.CollectedFish, "not every fish was collected");
        Assert.IsTrue(Pad.Instance.gameObject.activeSelf, "pad did not open");
        // Hover above the pad while it pops in (pad top is ~0.565 above its root at full size),
        // then drop in gently.
        Vector2 above = (Vector2)Pad.Instance.transform.position + new Vector2(0f, 0.565f + 1.27f + 0.5f);
        for (float w = 0f; w < 1.2f; w += Time.fixedDeltaTime)
        {
            Teleport(lander, above, Vector2.zero);
            yield return new WaitForFixedUpdate();
        }
        Assert.AreEqual(Lander.State.Flying, lander.CurrentState, "crashed while waiting above the pad: " + lander.CrashReason);
        Teleport(lander, above, new Vector2(0f, -1f));
        float t = 0f;
        while (lander.CurrentState == Lander.State.Flying && t < 5f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        Assert.AreEqual(Lander.State.Landed, lander.CurrentState, "landing failed: " + lander.CrashReason);

        yield return new WaitForSecondsRealtime(1.2f);
        Assert.AreEqual(GameManager.Phase.Won, gm.CurrentPhase);
        Assert.IsTrue(gm.StarsEarned >= 1 && gm.StarsEarned <= 3);
        var result = GameObject.Find("HUD Canvas").transform.Find("Result");
        Assert.IsTrue(result.gameObject.activeSelf, "result screen not shown");
        Assert.GreaterOrEqual(Progress.HighestUnlocked, Mathf.Min(level + 1, Progress.LevelCount));
    }

    [UnityTest]
    public IEnumerator HittingTheGround_Crashes()
    {
        yield return Load(2);
        var gm = GameManager.Instance;
        var lander = Lander.Instance;
        lander.Launch();
        Teleport(lander, lander.transform.position, new Vector2(0f, -25f));
        float t = 0f;
        while (lander.CurrentState == Lander.State.Flying && t < 5f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        Assert.AreEqual(Lander.State.Crashed, lander.CurrentState);
        Assert.IsFalse(lander.gameObject.activeSelf, "lander should explode");
        yield return new WaitForSecondsRealtime(1.6f);
        Assert.AreEqual(GameManager.Phase.Lost, gm.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator LeavingTheLevel_Crashes()
    {
        yield return Load(2);
        var lander = Lander.Instance;
        lander.Launch();
        Teleport(lander, GameManager.Instance.LevelBounds.max + new Vector2(5f, 5f), Vector2.zero);
        yield return null;
        yield return null;
        Assert.AreEqual(Lander.State.Crashed, lander.CurrentState);
    }

    [UnityTest]
    public IEnumerator FuelRunsDown_AndCatFoodRefuels()
    {
        yield return Load(2);
        var lander = Lander.Instance;
        lander.Launch();
        float full = lander.Fuel;
        lander.Refuel(-full * 0.5f); // drain half
        Assert.Less(lander.Fuel, full);
        var food = Object.FindAnyObjectByType<CatFood>();
        Assert.IsNotNull(food, "level 2 should contain cat food");
        float before = lander.Fuel;
        Teleport(lander, food.transform.position, Vector2.zero);
        yield return Fixed(3);
        Assert.Greater(lander.Fuel, before, "cat food did not refuel");
    }

    [UnityTest]
    public IEnumerator PauseStopsTime()
    {
        yield return Load(3);
        var gm = GameManager.Instance;
        gm.SetPaused(true);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(GameObject.Find("HUD Canvas").transform.Find("Pause").gameObject.activeSelf);
        gm.SetPaused(false);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator MainMenu_ListsAllLevels()
    {
        yield return Load(0);
        var canvas = GameObject.Find("Menu Canvas");
        Assert.IsNotNull(canvas, "menu not built");
        var levelButtons = canvas.transform.Find("Levels").GetComponentsInChildren<Button>(true)
            .Where(b => b.name.StartsWith("Button") && b.name != "Button Back" && b.name != "Button Reset Progress")
            .ToList();
        Assert.AreEqual(SceneManager.sceneCountInBuildSettings - 1, levelButtons.Count);
        Assert.GreaterOrEqual(levelButtons.Count, 11);
    }
}
