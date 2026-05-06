using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles score calculation, visual hype bar updates, and feedback audio.
/// Processes click results over time and animates score changes sequentially.
/// </summary>
public class ScoreCounter : MonoBehaviour
{
    [SerializeField] private SymbolSequenceMaker symbolSequenceMaker;

    // UI reference for score display
    private Slider hypeBar;

    [SerializeField] private int score;
    [SerializeField] private int goodPoint = 1;
    [SerializeField] private int failPoints = 1;

    // State flags for score calculation coroutine flow
    internal bool isCalculatingScore = false;
    internal bool finishedCalculating = false;

    [SerializeField] private AudioClip scoreUpSound, scoreDownSound;
    private AudioSource audioSource;

    private void Start()
    {
        hypeBar = GetComponent<Slider>();

        symbolSequenceMaker = GameObject.Find("SymbolSequenceMaker")
            .GetComponent<SymbolSequenceMaker>();

        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Main score calculation pipeline.
    /// Waits for burst animation to finish, disables UI, then resets round state.
    /// </summary>
    public IEnumerator CalculateScore()
    {
        UpdateScore();

        // Wait until burst animation finishes processing all click results
        yield return new WaitUntil(() => finishedCalculating == true);

        // Hide sequence UI after scoring phase
        foreach (var img in symbolSequenceMaker.imagePos)
        {
            img.enabled = false;
        }

        // Short delay before restarting next sequence
        yield return new WaitForSeconds(5f);

        isCalculatingScore = false;
        finishedCalculating = false;

        // Trigger next round
        symbolSequenceMaker.GenerateSequence();
    }

    /// <summary>
    /// Starts score processing using recorded click history.
    /// Creates a copy of results to avoid mutation during processing.
    /// </summary>
    public void UpdateScore()
    {
        List<bool> historyToAnimate = new List<bool>(symbolSequenceMaker.clickResults);

        isCalculatingScore = true;

        StartCoroutine(CalculateScoreBurst(historyToAnimate));
    }

    /// <summary>
    /// Processes each click result sequentially to animate score changes.
    /// Plays sound feedback and updates UI per entry.
    /// </summary>
    private IEnumerator CalculateScoreBurst(List<bool> clickHistory)
    {
        if (!isCalculatingScore)
        {
            yield break;
        }

        finishedCalculating = false;

        foreach (bool wasGoodHit in clickHistory)
        {
            // Increase or decrease score based on correctness
            if (wasGoodHit)
            {
                score += goodPoint;
                audioSource.PlayOneShot(scoreUpSound);
            }
            else
            {
                score -= failPoints;
                audioSource.PlayOneShot(scoreDownSound);
            }

            // Clamp score to valid UI range
            score = Mathf.Clamp(score, 0, (int)hypeBar.maxValue);

            // Update UI immediately
            hypeBar.value = score;

            // Delay between each score step for animation effect
            yield return new WaitForSeconds(1f);
        }

        finishedCalculating = true;
    }

    private void Update()
    {
        // Safety clamp in case score is modified externally
        score = Mathf.Clamp(score, 0, (int)hypeBar.maxValue);
        hypeBar.value = score;
    }
}
