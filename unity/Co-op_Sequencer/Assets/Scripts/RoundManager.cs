using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls round progression, difficulty scaling, and timer logic.
/// Also determines failed symbols when time runs out.
/// </summary>
public class RoundManager : MonoBehaviour
{
    private SymbolSequenceMaker symbolSequenceMaker;

    public int roundCounter = 0;

    internal List<Sprite> correctSymbols = new List<Sprite>();
    internal List<Sprite> incorrectSymbols = new List<Sprite>();

    public bool allowInput = true;

    private float currentTime;
    private float timeLimit;

    [SerializeField] private Slider timerSlider;

    private void Start()
    {
        symbolSequenceMaker = GameObject.Find("SymbolSequenceMaker")
            .GetComponent<SymbolSequenceMaker>();
    }

    /// <summary>
    /// Advances to the next round and recalculates difficulty + resets state.
    /// </summary>
    public void RoundCounter()
    {
        roundCounter++;

        ResetSymbolLists();
        UpdateDifficulty();
        EnableInput();

        StartCoroutine(TimerCoroutine());
    }

    /// <summary>
    /// Clears previous round results.
    /// </summary>
    private void ResetSymbolLists()
    {
        incorrectSymbols.Clear();
        correctSymbols.Clear();
    }

    /// <summary>
    /// Scales difficulty based on round number and player hand size.
    /// </summary>
    private void UpdateDifficulty()
    {
        symbolSequenceMaker.activeImages = Mathf.Min(
            10,
            3 + (roundCounter - 1) / 3 + symbolSequenceMaker.playerHand.Count
        );
    }

    /// <summary>
    /// Re-enables input for the new round.
    /// </summary>
    private void EnableInput()
    {
        allowInput = true;
    }

    /// <summary>
    /// Handles countdown timer for the round.
    /// When time runs out, remaining symbols are marked incorrect.
    /// </summary>
    private IEnumerator TimerCoroutine()
    {
        SetTimeLimit();
        currentTime = timeLimit;

        while (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            UpdateTimerUI();
            yield return null;
        }

        HandleTimeoutFailure();
        symbolSequenceMaker.GenerateSequence();
    }

    /// <summary>
    /// Calculates time limit with difficulty scaling.
    /// </summary>
    private void SetTimeLimit()
    {
        timeLimit = Mathf.Max(5f - (roundCounter * 0.5f), 5f);
    }

    /// <summary>
    /// Updates UI slider based on remaining time.
    /// </summary>
    private void UpdateTimerUI()
    {
        timerSlider.value = currentTime / timeLimit;
    }

    /// <summary>
    /// Marks remaining unselected symbols as incorrect when time runs out.
    /// </summary>
    private void HandleTimeoutFailure()
    {
        for (
            int i = symbolSequenceMaker.nextSymbol;
            i < symbolSequenceMaker.activeImages &&
            i < symbolSequenceMaker.availableSymbols.Count;
            i++
        )
        {
            incorrectSymbols.Add(symbolSequenceMaker.availableSymbols[i]);
        }
    }
}
