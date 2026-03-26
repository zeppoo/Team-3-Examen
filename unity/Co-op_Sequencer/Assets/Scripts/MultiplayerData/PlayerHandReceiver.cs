using System.Collections.Generic;
using UnityEngine;

public class PlayerHandReceiver : MonoBehaviour
{
    public void ReceiveJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("No JSON received!");
            return;
        }

        Debug.Log("Incoming JSON: " + json);

        PlayerHandsJson data = JsonUtility.FromJson<PlayerHandsJson>(json);

        if (data == null || data.playerHandss == null)
        {
            Debug.LogError("JSON parsing failed!");
            return;
        }

        Debug.Log("Received JSON!");

        for (int i = 0; i < data.playerHandss.Count; i++)
        {
            List<string> hand = data.playerHandss[i].cards;
            Debug.Log($"Player {i} hand: {string.Join(", ", hand)}");
        }
    }
}
