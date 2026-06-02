using System;
using UnityEngine;

public class AudioFeedbackManager : MonoBehaviour
{
    private const string MusicEnabledKey = "audio_music_enabled";
    private const string MusicVolumeKey = "audio_music_volume";
    private const string TouchSoundsEnabledKey = "audio_touch_sounds_enabled";
    private const string MusicClipIndexKey = "audio_music_clip_index";
    private const string TouchClipIndexKey = "audio_touch_clip_index";

    private const float DefaultMusicVolume = 0.55f;
    private const float TouchVolume = 0.65f;
    private const float TouchCooldownSeconds = 0.05f;

    private static AudioFeedbackManager instance;

    private AudioSource musicSource;
    private AudioSource touchSource;
    private AudioClip[] musicClips = Array.Empty<AudioClip>();
    private AudioClip[] touchClips = Array.Empty<AudioClip>();
    private float lastTouchSoundTime = -10f;

    public bool IsMusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
    public bool IsTouchSoundsEnabled => PlayerPrefs.GetInt(TouchSoundsEnabledKey, 1) == 1;
    public float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static AudioFeedbackManager EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<AudioFeedbackManager>();
        if (instance != null)
        {
            instance.Initialize();
            return instance;
        }

        GameObject audioObject = new GameObject("AudioFeedbackManager");
        instance = audioObject.AddComponent<AudioFeedbackManager>();
        instance.Initialize();
        return instance;
    }

    public void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMusicState();
    }

    public void SetMusicVolume(float volume)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(volume));
        PlayerPrefs.Save();
        ApplyMusicState();
    }

    public void SetTouchSoundsEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(TouchSoundsEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled && touchSource != null)
        {
            touchSource.Stop();
            lastTouchSoundTime = Time.unscaledTime + TouchCooldownSeconds;
        }
    }

    public void SetMusicClipIndex(int clipIndex)
    {
        PlayerPrefs.SetInt(MusicClipIndexKey, Mathf.Max(0, clipIndex));
        PlayerPrefs.Save();
        ApplyMusicState(true);
    }

    public void SetTouchClipIndex(int clipIndex)
    {
        PlayerPrefs.SetInt(TouchClipIndexKey, Mathf.Max(0, clipIndex));
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Initialize();
    }

    private void Update()
    {
        if (!IsTouchSoundsEnabled || touchClips.Length == 0)
        {
            return;
        }

        if (HasNewTouch())
        {
            PlayTouchSound();
        }
    }

    private void Initialize()
    {
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
        LoadAudioClips();
        ApplyMusicState();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (touchSource == null)
        {
            touchSource = gameObject.AddComponent<AudioSource>();
            touchSource.loop = false;
            touchSource.playOnAwake = false;
        }
    }

    private void LoadAudioClips()
    {
        if (musicClips.Length == 0)
        {
            musicClips = Resources.LoadAll<AudioClip>("Audio/MusicOptions");
            Array.Sort(musicClips, (left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
        }

        if (touchClips.Length == 0)
        {
            touchClips = Resources.LoadAll<AudioClip>("Audio/TouchOptions");
            Array.Sort(touchClips, (left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
        }
    }

    private void ApplyMusicState(bool forceClipRefresh = false)
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.volume = MusicVolume;

        if (!IsMusicEnabled || musicClips.Length == 0)
        {
            musicSource.Stop();
            return;
        }

        int clipIndex = Mathf.Clamp(PlayerPrefs.GetInt(MusicClipIndexKey, 0), 0, musicClips.Length - 1);
        AudioClip selectedClip = musicClips[clipIndex];
        if (forceClipRefresh || musicSource.clip != selectedClip)
        {
            musicSource.clip = selectedClip;
        }

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private bool HasNewTouch()
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                {
                    return true;
                }
            }

            return false;
        }

        return Input.GetMouseButtonDown(0);
    }

    private void PlayTouchSound()
    {
        if (Time.unscaledTime - lastTouchSoundTime < TouchCooldownSeconds)
        {
            return;
        }

        int clipIndex = Mathf.Clamp(PlayerPrefs.GetInt(TouchClipIndexKey, 0), 0, touchClips.Length - 1);
        touchSource.PlayOneShot(touchClips[clipIndex], TouchVolume);
        lastTouchSoundTime = Time.unscaledTime;
    }
}
