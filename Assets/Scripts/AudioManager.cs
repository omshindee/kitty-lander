using System;
using UnityEngine;

/// <summary>Plays the level's sound effects and the looping thruster rumble.</summary>
public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip pickup;
    [SerializeField] private AudioClip refuel;
    [SerializeField] private AudioClip crash;
    [SerializeField] private AudioClip win;
    [SerializeField] private AudioClip padOpen;
    [SerializeField] private AudioClip thrustLoop;
    [SerializeField, Range(0f, 1f)] private float thrustVolume = 0.35f;

    private AudioSource sfx;
    private AudioSource thrust;
    private Lander lander;

    public void EditorSetup(AudioClip pickup, AudioClip refuel, AudioClip crash, AudioClip win, AudioClip padOpen, AudioClip thrustLoop)
    {
        this.pickup = pickup; this.refuel = refuel; this.crash = crash;
        this.win = win; this.padOpen = padOpen; this.thrustLoop = thrustLoop;
    }

    private void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        thrust = gameObject.AddComponent<AudioSource>();
        thrust.playOnAwake = false;
        thrust.loop = true;
        thrust.clip = thrustLoop;
        thrust.volume = 0f;
    }

    private void Start()
    {
        lander = Lander.Instance;
        Fish.OnFishCollected += OnFish;
        Pad.OnPadShown += OnPad;
        if (lander != null)
        {
            lander.OnRefuel += OnRefuel;
            lander.OnCrash += OnCrash;
            lander.OnLanded += OnLanded;
        }
        if (thrustLoop != null) thrust.Play();
    }

    private void OnDestroy()
    {
        Fish.OnFishCollected -= OnFish;
        Pad.OnPadShown -= OnPad;
        if (lander != null)
        {
            lander.OnRefuel -= OnRefuel;
            lander.OnCrash -= OnCrash;
            lander.OnLanded -= OnLanded;
        }
    }

    private void Update()
    {
        bool on = lander != null && lander.IsThrusting && Time.timeScale > 0f;
        thrust.volume = Mathf.MoveTowards(thrust.volume, on ? thrustVolume : 0f, Time.unscaledDeltaTime * 3f);
        thrust.pitch = Mathf.Lerp(thrust.pitch, on ? 1.1f : 0.9f, Time.unscaledDeltaTime * 4f);
    }

    private void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfx.PlayOneShot(clip, volume);
    }

    private void OnFish(object s, EventArgs e) => Play(pickup, 0.8f);
    private void OnPad(object s, EventArgs e) => Play(padOpen, 0.8f);
    private void OnRefuel(object s, float amount) => Play(refuel, 0.8f);
    private void OnCrash(object s, EventArgs e) => Play(crash);
    private void OnLanded(object s, EventArgs e) => Play(win, 0.8f);
}
