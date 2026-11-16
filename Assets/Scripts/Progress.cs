using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saved player progress. Levels are identified by build index:
/// index 0 is the main menu, index 1..N are the levels.
/// </summary>
public static class Progress
{
    private const string UnlockedKey = "KL_Unlocked";
    private const string StarsKey = "KL_Stars_";

    public static int LevelCount => Mathf.Max(0, SceneManager.sceneCountInBuildSettings - 1);

    public static int HighestUnlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Mathf.Max(1, LevelCount));

    public static bool IsUnlocked(int level) => level <= HighestUnlocked;

    public static int GetStars(int level) => PlayerPrefs.GetInt(StarsKey + level, 0);

    public static int TotalStars
    {
        get
        {
            int total = 0;
            for (int i = 1; i <= LevelCount; i++) total += GetStars(i);
            return total;
        }
    }

    public static void CompleteLevel(int level, int stars)
    {
        if (stars > GetStars(level)) PlayerPrefs.SetInt(StarsKey + level, stars);
        if (level + 1 > PlayerPrefs.GetInt(UnlockedKey, 1)) PlayerPrefs.SetInt(UnlockedKey, level + 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(UnlockedKey);
        for (int i = 1; i <= 64; i++) PlayerPrefs.DeleteKey(StarsKey + i);
        PlayerPrefs.Save();
    }

    public static void LoadLevel(int level)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(Mathf.Clamp(level, 0, LevelCount));
    }

    public static void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}
