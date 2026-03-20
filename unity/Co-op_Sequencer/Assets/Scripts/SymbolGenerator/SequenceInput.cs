using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SequenceInput : MonoBehaviour
{

    private SymbolSequenceMaker symbolSequenceMaker;
    private Sprite currentSprite;
    



    void Start()
    {
        symbolSequenceMaker = GameObject.Find("SymbolSequenceMaker").GetComponent<SymbolSequenceMaker>();

        StartCoroutine(GetImage());

    }

   IEnumerator GetImage()
    {
        yield return new WaitForSeconds(0.1f);
        currentSprite = GetComponent<Image>().sprite;
    }

    public void IconPressed()
    {
        
        Sprite targetSprite = symbolSequenceMaker.availableSymbols[symbolSequenceMaker.nextSymbol];
        Debug.Log("Target Sprite: " + targetSprite.name);

        if (currentSprite == targetSprite) 
        {
            Debug.Log("Good sprite");
                symbolSequenceMaker.nextSymbol++;
        }
        else
        {
            Debug.Log("IncorrectSprite");
        }
      
        
    }
   
}
