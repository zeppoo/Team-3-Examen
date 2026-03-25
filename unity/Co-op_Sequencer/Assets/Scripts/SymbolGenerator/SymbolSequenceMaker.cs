using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SymbolSequenceMaker : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] public List<Sprite> symbols;
    [SerializeField] private List<Image> imagePos;
    [SerializeField] public List<Sprite> availableSymbols = new List<Sprite>();
    [SerializeField] private List<GameObject> playerHand = new List<GameObject>();
    [SerializeField] private Slider timerSlider;
    #endregion

    #region Private Fields
    private List<GameObject> players = new List<GameObject>();
    private PlayerHandGenerator playerHandGenerator;
    private ScoreCounter scoreCounter;

    private int roundCounter = 0;
    private int activeImages = 3;

    private int timer;
    private float currentTime;
    private float timeLimit;

    private bool canGoToNextRound = false;
    private bool finishedRound = false;
    #endregion

    #region Public / Internal State
    internal int nextSymbol;

    internal List<Sprite> correctSymbols = new List<Sprite>();
    internal List<Sprite> incorrectSymbols = new List<Sprite>();
    internal List<bool> clickResults = new List<bool>();

    // JSON representation of the currently active symbol sequence (by sprite name)
    [System.NonSerialized] public string currentSequenceJson;

    public bool allowInput = true;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        scoreCounter = FindAnyObjectByType<ScoreCounter>();

        imagePos = new List<Image>();
        foreach (Image img in GetComponentsInChildren<Image>())
        {
            if (img.gameObject == this.gameObject) continue;
            imagePos.Add(img);
        }

        playerHandGenerator = GetComponent<PlayerHandGenerator>();
        playerHandGenerator.DealHandToPlayers(symbols);

        playerHand.AddRange(GameObject.FindGameObjectsWithTag("Player"));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateSequence();
        }

        if (nextSymbol >= activeImages && canGoToNextRound)
        {
            GenerateSequence();
        }
    }
    #endregion

    #region Core Gameplay Logic
    private void GenerateSequence()
    {
        if (roundCounter > 0 && roundCounter % 5 == 0 && roundCounter <= 20 && !finishedRound)
        {
            allowInput = false;
            canGoToNextRound = false;
            finishedRound = true;

            scoreCounter.isCalculatingScore = true;
            StartCoroutine(ListenToMusic());
            return;
        }

        if (finishedRound)
        {
            clickResults.Clear();
            finishedRound = false;
        }

        allowInput = true;
        canGoToNextRound = true;

        RoundCounter();
        nextSymbol = 0;

        List<Sprite> pool = new List<Sprite>(symbols);

        availableSymbols.Clear();

        for (int i = 0; i < activeImages; i++)
        {
            int randomIndex = Random.Range(0, pool.Count);
            Sprite chosenSprite = pool[randomIndex];

            imagePos[i].sprite = chosenSprite;
            availableSymbols.Add(chosenSprite);

            imagePos[i].color = Color.white;
            imagePos[i].enabled = true;

            pool.RemoveAt(randomIndex);
        }

        // Build a simple name list and serialize the current sequence to JSON
        List<string> sequenceNames = new List<string>();
        foreach (var sprite in availableSymbols)
        {
            sequenceNames.Add(sprite != null ? sprite.name : string.Empty);
        }

        currentSequenceJson = JsonUtility.ToJson(new SequenceJsonWrapper { sequence = sequenceNames });
        Debug.Log("Current sequence JSON: " + currentSequenceJson);
    }

    private void RoundCounter()
    {
        roundCounter++;

        incorrectSymbols.Clear();
        correctSymbols.Clear();

        activeImages = Mathf.Min(10, 3 + (roundCounter - 1) / 3 + playerHand.Count);

        allowInput = true;

        StartCoroutine(TimerCoroutine());
    }
    #endregion

    #region Player Interaction
    public void MarkSymbolAsFound(int symbolIndex, Color newColor)
    {
        if (symbolIndex < imagePos.Count)
        {
            imagePos[symbolIndex].color = newColor;
        }
    }
    #endregion

    #region Coroutines
    IEnumerator TimerCoroutine()
    {
        timeLimit = 5f - (roundCounter * 0.5f);
        timeLimit = Mathf.Max(timeLimit, 5f);

        currentTime = timeLimit;

        while (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            timerSlider.value = currentTime / timeLimit;
            yield return null;
        }

        for (int i = nextSymbol; i < activeImages && i < availableSymbols.Count; i++)
        {
            incorrectSymbols.Add(availableSymbols[i]);
        }

        GenerateSequence();
    }

    public IEnumerator ListenToMusic()
    {
        scoreCounter.UpdateScore();

        yield return new WaitUntil(() => scoreCounter.finishedCalculating == true);

        foreach (var img in imagePos)
        {
            img.enabled = false;
        }

        yield return new WaitForSeconds(5f);

        scoreCounter.isCalculatingScore = false;
        scoreCounter.finishedCalculating = false;

        GenerateSequence();
    }

    [System.Serializable]
    private class SequenceJsonWrapper
    {
        public List<string> sequence;
    }
    #endregion
}
