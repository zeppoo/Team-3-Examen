using System;
using UnityEngine;

public class BeatManager : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Detection Settings")]
    public float sensitivity = 1.5f;
    public float minThreshold = 0.1f;

    public static event Action OnBeat;

    private float[] spectrum = new float[64];
    private float previousEnergy;

    void Update()
    {
        if (!audioSource.isPlaying) return;

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.Blackman);

        float energy = 0f;

        // Focus on low frequencies (bass/kick)
        for (int i = 0; i < 10; i++)
        {
            energy += spectrum[i];
        }

        // Detect beat spike
        if (energy > previousEnergy * sensitivity && energy > minThreshold)
        {
            OnBeat?.Invoke();
        }

        previousEnergy = Mathf.Lerp(previousEnergy, energy, Time.deltaTime * 10f);
    }
}
