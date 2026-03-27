using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScoreCounter : MonoBehaviour
{
   [SerializeField] private SymbolSequenceMaker symbolSequenceMaker;
   private Slider hypeBar;
   [SerializeField] private int score;
   [SerializeField] private int goodPoint = 1;
   [SerializeField] private int failPoints = 1;
   internal bool isCalculatingScore = false;
    [SerializeField] private AudioClip scoreUpSound, scoreDownSound;
    private AudioSource audioSource;
    internal bool finishedCalculating = false;

    private void Start() 
    {
        hypeBar = GetComponent<Slider>();
        symbolSequenceMaker = GameObject.Find("SymbolSequenceMaker").GetComponent<SymbolSequenceMaker>();
        audioSource = GetComponent<AudioSource>();
    }

    public void UpdateScore() 
    { 
       
        List<bool> historyToAnimate = new List<bool>(symbolSequenceMaker.clickResults);
        StartCoroutine(CalculateScoreBurst(historyToAnimate));
        
    }

    private IEnumerator CalculateScoreBurst(List<bool> clickHistory)
    {
      
        if (!isCalculatingScore) yield break;
        
       
        finishedCalculating = false; 

        foreach (bool wasGoodHit in clickHistory)
        {
            if (wasGoodHit)
            {
                score += goodPoint;
                hypeBar.value = score;
                audioSource.PlayOneShot(scoreUpSound);
            }
            else
            {
                score -= failPoints;
                hypeBar.value = score;
                audioSource.PlayOneShot(scoreDownSound);
            }

            yield return new WaitForSeconds(1f);
        }

       
        finishedCalculating = true;
    }

    private void Update()
    {
        if (score < 0) score = 0;
        if (score > hypeBar.maxValue) score = (int)hypeBar.maxValue;
    }

}
