using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SymbolSequenceMaker : MonoBehaviour
{
    [SerializeField] public List<Sprite> symbols;
    [SerializeField] private List<Image> imagePos;
    [SerializeField] public List<Sprite> availableSymbols = new List<Sprite>();
    private List<GameObject> players = new List<GameObject>();
    private PlayerHandGenerator playerHandGenerator;
    [SerializeField] private List<GameObject> playerHand = new List<GameObject>();
    internal int nextSymbol;

    private int roundCounter = 0;
    private int activeImages = 3;

    private int timer;
    private float currentTime;
    private float timeLimit;
    [SerializeField] private Slider timerSlider;

    private ScoreCounter scoreCounter;
    internal List<Sprite> correctSymbols = new List<Sprite>();
    internal List<Sprite> incorrectSymbols = new List<Sprite>();
    internal List<bool> clickResults = new List<bool>();
    private bool canGoToNextRound = false;

    private bool finishedRound = false;
    public bool allowInput = true; 


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
            
            pool.RemoveAt(randomIndex);
        }
    }

    public void MarkSymbolAsFound(int symbolIndex, Color newColor)
    {
        if(symbolIndex < imagePos.Count)
        {
            imagePos[symbolIndex].color = newColor;
        }
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

   

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateSequence();
        }
        if(nextSymbol >= activeImages && canGoToNextRound)
        {
            GenerateSequence();
        }
        Debug.Log(allowInput);

    }

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
        
     
        GenerateSequence();
    }

    public IEnumerator ListenToMusic() 
    { 
  
        scoreCounter.UpdateScore();
        
     
        yield return new WaitUntil(() => scoreCounter.finishedCalculating == true);

       
        yield return new WaitForSeconds(5f);
        
       
        scoreCounter.isCalculatingScore = false;
        scoreCounter.finishedCalculating = false;
        GenerateSequence();
    }
}
