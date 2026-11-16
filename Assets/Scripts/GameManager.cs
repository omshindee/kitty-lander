using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private int totalFish;
    private int collectedFish;

    private void Awake()
    {
        Instance = this;
    }
   
    private void Start()
    {
        totalFish = FindObjectsByType<Fish>().Length;
        Debug.Log(totalFish);
        Fish.OnFishCollected += Fish_OnFishCollected;
    }

    private void Fish_OnFishCollected(object sender, System.EventArgs e)
    {
        Debug.Log(totalFish);
        collectedFish++;


        if (collectedFish >= totalFish)
        {
            Pad.Instance.Show();
        }
    }



}