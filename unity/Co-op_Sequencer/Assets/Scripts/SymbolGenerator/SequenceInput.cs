using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SequenceInput : MonoBehaviour, IPointerClickHandler
{
    private SymbolSequenceMaker symbolSequenceMaker;
    private PlayerInputFeedback playerInputFeedback;

    void Start()
    {
        symbolSequenceMaker = GameObject.Find("SymbolSequenceMaker").GetComponent<SymbolSequenceMaker>();
        playerInputFeedback = GetComponentInParent<PlayerInputFeedback>();
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

                Image targetImage = symbolSequenceMaker.imagePos[symbolSequenceMaker.nextSymbol];
                StartCoroutine(playerInputFeedback.Bounce(targetImage));

                Debug.Log("Correct! Clicked");
            }
            else
            {
                symbolSequenceMaker.MarkSymbolAsFound(symbolSequenceMaker.nextSymbol, Color.indianRed);
                symbolSequenceMaker.incorrectSymbols.Add(currentSprite);

                Image targetImage = symbolSequenceMaker.imagePos[symbolSequenceMaker.nextSymbol];
                StartCoroutine(playerInputFeedback.Shake(targetImage));

                Debug.Log("Incorrect! Clicked");
            }

            symbolSequenceMaker.nextSymbol++;
            symbolSequenceMaker.HighlightNextSymbol();
        }


       
    }
}
