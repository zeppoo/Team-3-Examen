using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles generation of symbol sequences, round progression, and basic sequence state.
/// This script is responsible for building the active symbol set and coordinating round flow.
/// </summary>
public class SymbolSequenceMaker : MonoBehaviour
{
    [SerializeField] public List<Sprite> symbols;
    [SerializeField] public List<Image> imagePos;

    // Runtime-generated sequence data
    [SerializeField] public List<Sprite> availableSymbols = new List<Sprite>();

    // Player-related symbols (hand system)
    [SerializeField] public List<GameObject> playerHand = new List<GameObject>();

    private PlayerHandGenerator playerHandGenerator;

    public int activeImages = 3;

    // Round flow control flags
    private bool canGoToNextRound = false;
    private bool finishedRound = false;

    // Tracks current input progress through sequence
    internal int nextSymbol;

    // Stores correctness results for the current sequence attempt
    internal List<bool> clickResults = new List<bool>();

    // JSON representation of current sequence (used for debugging / external systems)
    [System.NonSerialized] public string currentSequenceJson;

    private RoundManager roundManager;
    private ScoreCounter scoreSystem;

    private void Start()
    {
        InitializeManagers();
        InitializeImagePositions();
        InitializePlayerHand();
    }

    private void Update()
    {
        // Allows manual testing (space) or auto-advance when sequence is completed
        if (Input.GetKeyDown(KeyCode.Space) ||
            (nextSymbol >= activeImages && canGoToNextRound))
        {
            GenerateSequence();
        }
    }

    /// <summary>
    /// Main entry point for creating a new symbol sequence.
    /// Handles round checks, resets, generation, and serialization.
    /// </summary>
    public void GenerateSequence()
    {
        // Special scoring round logic (every 5 rounds up to 20)
        if (HandleScoreRound())
        {
            return;
        }

        ResetFinishedRound();
        StartNewRound();
        PopulateSequence();
        SerializeCurrentSequence();
    }

    /// <summary>
    /// Finds and stores references to core systems.
    /// </summary>
    private void InitializeManagers()
    {
        roundManager = GameObject.Find("GameManager")
            .GetComponent<RoundManager>();

        scoreSystem = GameObject.Find("HypeBar")
            .GetComponent<ScoreCounter>();
    }

    /// <summary>
    /// Collects UI image slots used for displaying the sequence.
    /// </summary>
    private void InitializeImagePositions()
    {
        imagePos = new List<Image>();

        foreach (Image img in GetComponentsInChildren<Image>())
        {
            if (img.gameObject == gameObject)
            {
                continue;
            }

            imagePos.Add(img);
        }
    }

    /// <summary>
    /// Initializes player hand system and assigns starting symbols.
    /// </summary>
    private void InitializePlayerHand()
    {
        playerHandGenerator = GetComponent<PlayerHandGenerator>();

        playerHandGenerator.DealHandToPlayers(symbols);

        playerHand.AddRange(GameObject.FindGameObjectsWithTag("Player"));
    }

    /// <summary>
    /// Handles special scoring rounds and blocks normal sequence flow if active.
    /// </summary>
    private bool HandleScoreRound()
    {
        bool isScoreRound =
            roundManager.roundCounter > 0 &&
            roundManager.roundCounter % 5 == 0 &&
            roundManager.roundCounter <= 20 &&
            !finishedRound;

        if (!isScoreRound)
        {
            return false;
        }

        roundManager.allowInput = false;
        canGoToNextRound = false;
        finishedRound = true;

        scoreSystem.isCalculatingScore = true;
        StartCoroutine(scoreSystem.CalculateScore());

        return true;
    }

    /// <summary>
    /// Clears state after a completed round if needed.
    /// </summary>
    private void ResetFinishedRound()
    {
        if (!finishedRound)
        {
            return;
        }

        clickResults.Clear();
        finishedRound = false;
    }

    /// <summary>
    /// Prepares variables for a new round.
    /// </summary>
    private void StartNewRound()
    {
        roundManager.allowInput = true;
        canGoToNextRound = true;

        roundManager.RoundCounter();

        nextSymbol = 0;
    }

    /// <summary>
    /// Generates a random sequence of symbols for the current round.
    /// </summary>
    private void PopulateSequence()
    {
        List<Sprite> pool = new List<Sprite>(symbols);

        availableSymbols.Clear();

        for (int i = 0; i < activeImages; i++)
        {
            AssignRandomSymbolToPosition(pool, i);
        }
    }

    /// <summary>
    /// Assigns a randomly selected sprite to a UI slot.
    /// </summary>
    private void AssignRandomSymbolToPosition(List<Sprite> pool, int index)
    {
        int randomIndex = Random.Range(0, pool.Count);

        Sprite chosenSprite = pool[randomIndex];

        imagePos[index].sprite = chosenSprite;

        availableSymbols.Add(chosenSprite);

        ResetImageState(index);

        pool.RemoveAt(randomIndex);
    }

    /// <summary>
    /// Resets visual state of a UI image slot.
    /// </summary>
    private void ResetImageState(int index)
    {
        imagePos[index].color = Color.white;
        imagePos[index].enabled = true;
    }

    /// <summary>
    /// Converts current sequence into JSON format for debugging or external use.
    /// </summary>
    private void SerializeCurrentSequence()
    {
        List<string> sequenceNames = new List<string>();

        foreach (Sprite sprite in availableSymbols)
        {
            sequenceNames.Add(
                sprite != null ? sprite.name : string.Empty
            );
        }

        currentSequenceJson = JsonUtility.ToJson(
            new SequenceJsonWrapper
            {
                sequence = sequenceNames
            }
        );

        Debug.Log("Current sequence JSON: " + currentSequenceJson);
    }

    [System.Serializable]
    private class SequenceJsonWrapper
    {
        public List<string> sequence;
    }
}
