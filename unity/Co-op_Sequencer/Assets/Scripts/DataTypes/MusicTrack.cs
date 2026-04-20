using UnityEngine;

[CreateAssetMenu(fileName = "NewTrack", menuName = "Scriptable Objects/MusicTrack")]
public class MusicTrack : ScriptableObject
{
    public string trackName;
    public AudioClip clip;
    public float bpm = 120f;

    [Tooltip("Seconds before the first beat (intro/pickup)")]
    public float startOffset = 0f;

    [Tooltip("Time signature numerator (e.g. 4 for 4/4)")]
    public int beatsPerBar = 4;
}
