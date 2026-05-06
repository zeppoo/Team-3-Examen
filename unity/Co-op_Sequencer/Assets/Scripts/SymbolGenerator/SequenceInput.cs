using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles player input for symbol sequence selection.
/// Compares clicked UI symbols against the generated sequence,
/// records results, triggers feedback, and advances progression.
/// </summary>
public class SequenceInput : MonoBehaviour, IPointerClickHandler
{
    private SymbolSequenceMaker symbolSequenceMaker;
    private PlayerInputFeedback playerInputFeedback;
    private SymbollManagerUi symbollManagerUi;
    private RoundManager roundManager;

    private void Start()
    {
        InitializeComponents();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Block input if round system does not allow interaction
        if (!roundManager.allowInput)
        {
            return;
        }

        ProcessInput();
    }

    /// <summary>
    /// Initializes references to required systems and components.
    /// </summary>
    private void InitializeComponents()
    {
        GameObject symbolManager = GameObject.Find("SymbolSequenceMaker");

        symbolSequenceMaker = symbolManager.GetComponent<SymbolSequenceMaker>();
        symbollManagerUi = symbolManager.GetComponent<SymbollManagerUi>();

        roundManager = GameObject.Find("GameManager")
            .GetComponent<RoundManager>();

        playerInputFeedback = GetComponentInParent<PlayerInputFeedback>();
    }

    /// <summary>
    /// Main input processing pipeline:
    /// - Gets selected symbol
    /// - Compares with target
    /// - Records result
    /// - Triggers feedback
    /// - Advances sequence
    /// </summary>
    private void ProcessInput()
    {
        int index = symbolSequenceMaker.nextSymbol;

        Sprite selectedSprite = GetComponent<Image>().sprite;
        Sprite targetSprite = symbolSequenceMaker.availableSymbols[index];

        bool isCorrect = selectedSprite == targetSprite;

        // Store result for round evaluation
        symbolSequenceMaker.clickResults.Add(isCorrect);

        HandleResult(isCorrect, selectedSprite, index);

        AdvanceSequence();
    }

    /// <summary>
    /// Handles correct/incorrect selection result branching.
    /// Applies UI feedback, updates score tracking lists, and triggers animations.
    /// </summary>
    private void HandleResult(bool isCorrect, Sprite selectedSprite, int index)
    {
        // Unified feedback color logic
        Color feedbackColor = isCorrect
            ? Color.gray
            : new Color(1f, 0.4f, 0.4f);

        // Mark symbol visually in UI
        symbollManagerUi.MarkSymbolAsFound(index, feedbackColor);

        if (isCorrect)
        {
            // Correct selection handling
            roundManager.correctSymbols.Add(selectedSprite);
            StartCoroutine(playerInputFeedback.Bounce(symbolSequenceMaker.imagePos[index]));

            Debug.Log("Correct! Clicked");
        }
        else
        {
            // Incorrect selection handling
            roundManager.incorrectSymbols.Add(selectedSprite);
            StartCoroutine(playerInputFeedback.Shake(symbolSequenceMaker.imagePos[index]));

            Debug.Log("Incorrect! Clicked");
        }
    }

    /// <summary>
    /// Advances sequence progression and updates UI highlight.
    /// </summary>
    private void AdvanceSequence()
    {
        symbolSequenceMaker.nextSymbol++;
        symbollManagerUi.HighlightNextSymbol();
    }
}
