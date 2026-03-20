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
    PlayerHandGenerator playerHandGenerator;
    [SerializeField] private List<GameObject> playerHand = new List<GameObject>();
    internal int nextSymbol;

    private int roundCounter = 0;
    private int activeImages = 3;

    private int timer;
    private float currentTime;
    private float timeLimit;
    [SerializeField] private Slider timerSlider;
   
    

    private void Start()
    {
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
        RoundCounter();

        
        List<Sprite> pool = new List<Sprite>(symbols);
        
        
        availableSymbols.Clear(); 
        
        for (int i = 0; i < activeImages; i++)
        {
            int randomIndex = Random.Range(0, pool.Count);
            Sprite chosenSprite = pool[randomIndex];
            
          
            imagePos[i].sprite = chosenSprite;
            
           
            availableSymbols.Add(chosenSprite);
            
            
            pool.RemoveAt(randomIndex);
        }
    }




    private void RoundCounter()
    {
        roundCounter++;
        activeImages = Mathf.Min(10, 3 + (roundCounter - 1) / 3 + playerHand.Count); 
        StartCoroutine(TimerCoroutine());
    }



    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateSequence();
        }
    }

    IEnumerator TimerCoroutine()
    {
       

        timeLimit = 10f - (roundCounter * 0.5f);

        
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
}
