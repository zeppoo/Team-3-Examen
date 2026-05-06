using UnityEngine;

public class SymbollManagerUi : MonoBehaviour
{

    private SymbolSequenceMaker symbolSequenceMaker;

    private void Start()
    {
        symbolSequenceMaker = GetComponent<SymbolSequenceMaker>();
    }
    /// <summary>
    /// Highlights the next symbol in the sequence by increasing its size.
    /// </summary>
    public void HighlightNextSymbol()
    {
        for (int i = 0; i < symbolSequenceMaker.imagePos.Count; i++)
        {
            RectTransform rect = symbolSequenceMaker.imagePos[i].rectTransform;

            if (i == symbolSequenceMaker.nextSymbol)
            {
                rect.sizeDelta = new Vector2(120, 120);
            }
            else
            {
                rect.sizeDelta = new Vector2(100, 100);
            }
        }
    }

    /// <summary>
    /// Marks a symbol as found by changing its color.
    /// </summary>
    public void MarkSymbolAsFound(int symbolIndex, Color newColor)
    {
        if (symbolIndex < symbolSequenceMaker.imagePos.Count)
        {
            symbolSequenceMaker.imagePos[symbolIndex].color = newColor;
        }
    }

}
