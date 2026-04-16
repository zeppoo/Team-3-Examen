using System;
using UnityEngine;

/// <summary>
/// Manages music playback and provides beat timing for the rhythm game.
/// Other scripts subscribe to OnBeat to act on beat. Use CurrentBeat,
/// TimeSinceLastBeat, and BeatProgress for fine-grained timing.
///
/// Place on a GameObject with an AudioSource. Set BPM to match the track.
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private MusicTrack currentTrack;

    [Header("Audio Analysis")]
    [SerializeField] private int spectrumSamples = 1024;

    // ── Events ──────────────────────────────────────────────────────────

    /// <summary>Fires on every beat. Parameter is the beat number (0-based).</summary>
    public event Action<int> OnBeat;

    /// <summary>Fires on every half-beat (8th notes). Parameter is the sub-beat number.</summary>
    public event Action<int> OnHalfBeat;

    /// <summary>Fires on every quarter-beat (16th notes). Parameter is the sub-beat number.</summary>
    public event Action<int> OnQuarterBeat;

    /// <summary>Fires when the music starts playing.</summary>
    public event Action OnMusicStarted;

    /// <summary>Fires when the music ends.</summary>
    public event Action OnMusicEnded;

    // ── Public Properties ───────────────────────────────────────────────

    /// <summary>Beats per minute of the current track.</summary>
    public float BPM => currentTrack != null ? currentTrack.bpm : 120f;

    /// <summary>Beats per bar (time signature numerator).</summary>
    public int BeatsPerBar => currentTrack != null ? currentTrack.beatsPerBar : 4;

    /// <summary>The current MusicTrack asset.</summary>
    public MusicTrack CurrentTrack => currentTrack;

    /// <summary>Seconds per beat (derived from BPM).</summary>
    public float SecondsPerBeat => _secondsPerBeat;

    /// <summary>The current beat number since music started (0-based).</summary>
    public int CurrentBeat => _currentBeat;

    /// <summary>Seconds elapsed since the last beat.</summary>
    public float TimeSinceLastBeat => _timeSinceLastBeat;

    /// <summary>Progress through the current beat (0.0 = on beat, 1.0 = next beat).</summary>
    public float BeatProgress => _secondsPerBeat > 0 ? _timeSinceLastBeat / _secondsPerBeat : 0f;

    /// <summary>The DSP time when the music started (high precision).</summary>
    public double MusicStartDspTime => _musicStartDspTime;

    /// <summary>True if music is currently playing.</summary>
    public bool IsPlaying => musicSource != null && musicSource.isPlaying;

    /// <summary>Current spectrum data (updated each frame while playing).</summary>
    public float[] SpectrumData => _spectrumData;

    // ── Private State ───────────────────────────────────────────────────

    private float _secondsPerBeat;
    private int _currentBeat = -1;
    private int _currentHalfBeat = -1;
    private int _currentQuarterBeat = -1;
    private float _timeSinceLastBeat;
    private double _musicStartDspTime;
    private bool _playing;
    private float[] _spectrumData;

    // ── Unity Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        if (FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        _secondsPerBeat = 60f / BPM;
        _spectrumData = new float[spectrumSamples];

        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!_playing || musicSource == null) return;

        // Check if music ended
        if (!musicSource.isPlaying)
        {
            _playing = false;
            OnMusicEnded?.Invoke();
            return;
        }

        UpdateBeatTracking();
        UpdateSpectrum();
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Start playing the assigned music clip and begin beat tracking.</summary>
    /// <summary>Start playing the current track.</summary>
    public void Play()
    {
        if (currentTrack != null)
            LoadTrack(currentTrack);
        else
            PlayInternal();
    }

    /// <summary>Load and play a specific MusicTrack.</summary>
    public void LoadTrack(MusicTrack track)
    {
        currentTrack = track;
        _secondsPerBeat = 60f / track.bpm;

        if (musicSource != null)
            musicSource.clip = track.clip;

        PlayInternal();
    }

    private void PlayInternal()
    {
        if (musicSource == null)
        {
            Debug.LogError("[AudioManager] No AudioSource assigned!");
            return;
        }

        if (musicSource.clip == null)
        {
            Debug.LogError("[AudioManager] No AudioClip assigned!");
            return;
        }

        _currentBeat = -1;
        _currentHalfBeat = -1;
        _currentQuarterBeat = -1;
        _timeSinceLastBeat = 0f;

        float offset = currentTrack != null ? currentTrack.startOffset : 0f;
        _musicStartDspTime = AudioSettings.dspTime + offset;
        musicSource.PlayScheduled(_musicStartDspTime);
        _playing = true;

        Debug.Log($"[AudioManager] Playing '{musicSource.clip.name}' at {BPM} BPM ({_secondsPerBeat:F3}s/beat), offset={offset}s");
        OnMusicStarted?.Invoke();
    }

    /// <summary>Stop the music and reset beat tracking.</summary>
    public void Stop()
    {
        if (musicSource != null)
            musicSource.Stop();
        _playing = false;
        _currentBeat = -1;
    }

    /// <summary>Pause the music.</summary>
    public void Pause()
    {
        if (musicSource != null)
            musicSource.Pause();
    }

    /// <summary>Resume paused music.</summary>
    public void Resume()
    {
        if (musicSource != null)
            musicSource.UnPause();
    }

    /// <summary>
    /// Returns how far off the given time is from the nearest beat, in seconds.
    /// Negative = early, positive = late. Useful for hit accuracy scoring.
    /// </summary>
    public float GetBeatOffset()
    {
        float progress = BeatProgress;
        if (progress > 0.5f)
            return -(1f - progress) * _secondsPerBeat; // early for next beat
        return progress * _secondsPerBeat; // late from last beat
    }

    /// <summary>
    /// Returns the volume level of a frequency range (0.0–1.0).
    /// Useful for visual effects reacting to bass, mids, or highs.
    /// </summary>
    public float GetFrequencyBand(int bandIndex, int totalBands = 8)
    {
        if (_spectrumData == null || _spectrumData.Length == 0) return 0f;

        int samplesPerBand = _spectrumData.Length / totalBands;
        int start = bandIndex * samplesPerBand;
        int end = Mathf.Min(start + samplesPerBand, _spectrumData.Length);

        float sum = 0f;
        for (int i = start; i < end; i++)
            sum += _spectrumData[i];

        return sum / samplesPerBand;
    }

    // ── Private Methods ─────────────────────────────────────────────────

    private void UpdateBeatTracking()
    {
        double elapsed = AudioSettings.dspTime - _musicStartDspTime;
        if (elapsed < 0) return; // haven't reached start offset yet

        // Full beats
        int beat = Mathf.FloorToInt((float)(elapsed / _secondsPerBeat));
        _timeSinceLastBeat = (float)(elapsed - beat * _secondsPerBeat);

        if (beat != _currentBeat)
        {
            _currentBeat = beat;
            OnBeat?.Invoke(_currentBeat);
        }

        // Half-beats (8th notes)
        float halfBeatDuration = _secondsPerBeat * 0.5f;
        int halfBeat = Mathf.FloorToInt((float)(elapsed / halfBeatDuration));
        if (halfBeat != _currentHalfBeat)
        {
            _currentHalfBeat = halfBeat;
            OnHalfBeat?.Invoke(_currentHalfBeat);
        }

        // Quarter-beats (16th notes)
        float quarterBeatDuration = _secondsPerBeat * 0.25f;
        int quarterBeat = Mathf.FloorToInt((float)(elapsed / quarterBeatDuration));
        if (quarterBeat != _currentQuarterBeat)
        {
            _currentQuarterBeat = quarterBeat;
            OnQuarterBeat?.Invoke(_currentQuarterBeat);
        }
    }

    private void UpdateSpectrum()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.GetSpectrumData(_spectrumData, 0, FFTWindow.BlackmanHarris);
    }
}
