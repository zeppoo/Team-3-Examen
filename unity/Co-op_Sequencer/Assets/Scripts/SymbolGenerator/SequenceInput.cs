using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SequenceInput : MonoBehaviour, IPointerClickHandler
{
    private SymbolSequenceMaker symbolSequenceMaker;

    void Start()
    {
        symbolSequenceMaker = GameObject.Find("SymbolSequenceMaker").GetComponent<SymbolSequenceMaker>();
    }


    public void OnPointerClick(PointerEventData eventData)
    {

        if (symbolSequenceMaker.allowInput)
        {
            Sprite targetSprite = symbolSequenceMaker.availableSymbols[symbolSequenceMaker.nextSymbol];
            Sprite currentSprite = GetComponent<Image>().sprite;

            bool isCorrect = (currentSprite == targetSprite);


            symbolSequenceMaker.clickResults.Add(isCorrect);

            if (isCorrect)
            {
                symbolSequenceMaker.MarkSymbolAsFound(symbolSequenceMaker.nextSymbol, Color.darkGray);
                symbolSequenceMaker.correctSymbols.Add(currentSprite);
            }
            else
            {
                symbolSequenceMaker.MarkSymbolAsFound(symbolSequenceMaker.nextSymbol, Color.indianRed);
                symbolSequenceMaker.incorrectSymbols.Add(currentSprite);
            }

            symbolSequenceMaker.nextSymbol++;
        }

       
    }
}
