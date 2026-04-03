using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInputFeedback : MonoBehaviour
{
    public IEnumerator Bounce(Image img)
    {
        RectTransform rect = img.rectTransform;
        Vector3 startPos = rect.anchoredPosition;

        float duration = 0.4f;
        float height = 20f;
        float time = 0f;

        while (time < duration)
        {
            float yOffset = Mathf.Sin(time * Mathf.PI * 4) * height;
            rect.anchoredPosition = startPos + new Vector3(0, yOffset, 0);

            time += Time.deltaTime;
            yield return null;
        }

        rect.anchoredPosition = startPos;
    }

    public IEnumerator Shake(Image img)
    {
        RectTransform rect = img.rectTransform;
        Vector3 startPos = rect.anchoredPosition;

        float duration = 0.3f;
        float strength = 15f;
        float time = 0f;

        while (time < duration)
        {
            float xOffset = Random.Range(-strength, strength);
            rect.anchoredPosition = startPos + new Vector3(xOffset, 0, 0);

            time += Time.deltaTime;
            yield return null;
        }

        rect.anchoredPosition = startPos;
    }


   

}
