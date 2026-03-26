using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerHandGenerator : MonoBehaviour
{
    [SerializeField] private List<GameObject> playerHand = new List<GameObject>();
    [SerializeField] private List<Image> playerHandSymbols = new List<Image>();
    [SerializeField] private PlayerHandReceiver receiver;

    [NonSerialized] public string lastDealtHandsJson;

    void Awake()
    {
        FindPlayers();
        Debug.Log("Awake - players found: " + playerHand.Count);
        Debug.Log("Awake - playerHandSymbols slots: " + playerHandSymbols.Count);
    }

    private void FindPlayers()
    {
        playerHand.Clear();
        playerHandSymbols.Clear();

        playerHand.AddRange(GameObject.FindGameObjectsWithTag("Player"));

        foreach (GameObject hand in playerHand)
        {
            Image[] childImages = hand.GetComponentsInChildren<Image>();

            foreach (Image img in childImages)
            {
                if (img.gameObject == hand) continue;
                playerHandSymbols.Add(img);
            }
        }
    }

    public void DealHandToPlayers(List<Sprite> symbols)
    {
        if (playerHand.Count == 0)
        {
            FindPlayers();
        }

        int playerCount = playerHand.Count;
        if (playerCount == 0) return;

        int totalImages = 10;

        int baseAmount = totalImages / playerCount;
        int leftovers = totalImages % playerCount;

        int[] playerSymbolCounts = new int[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            playerSymbolCounts[i] = baseAmount;
        }

        while (leftovers > 0)
        {
            int randomPlayerIndex = UnityEngine.Random.Range(0, playerCount);
            playerSymbolCounts[randomPlayerIndex]++;
            leftovers--;
        }

        List<Sprite> availableSymbols = new List<Sprite>(symbols);

        // ✅ UPDATED TYPE
        List<PlayerHand> dealtHandsForExport = new List<PlayerHand>();

        for (int i = 0; i < playerCount; i++)
        {
            GameObject player = playerHand[i];
            int amountToGive = playerSymbolCounts[i];
            int assignedCount = 0;

            List<string> thisPlayerHand = new List<string>();

            Image[] childImages = player.GetComponentsInChildren<Image>(true);

            foreach (Image img in childImages)
            {
                if (img.gameObject == player) continue;

                if (assignedCount < amountToGive && availableSymbols.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, availableSymbols.Count);
                    img.sprite = availableSymbols[randomIndex];
                    availableSymbols.RemoveAt(randomIndex);

                    img.gameObject.SetActive(true);
                    assignedCount++;

                    thisPlayerHand.Add(img.sprite != null ? img.sprite.name : string.Empty);
                }
                else
                {
                    img.gameObject.SetActive(false);
                }
            }

            // ✅ WRAP EACH HAND
            dealtHandsForExport.Add(new PlayerHand { cards = thisPlayerHand });
        }

        PlayerHandsJson wrapper = new PlayerHandsJson { playerHandss = dealtHandsForExport };
        lastDealtHandsJson = JsonUtility.ToJson(wrapper);

        Debug.Log("Dealt hands JSON: " + lastDealtHandsJson);

        // ✅ SEND IMMEDIATELY
        if (receiver != null)
        {
            receiver.ReceiveJson(lastDealtHandsJson);
        }
        else
        {
            Debug.LogWarning("No receiver assigned!");
        }
    }
}
