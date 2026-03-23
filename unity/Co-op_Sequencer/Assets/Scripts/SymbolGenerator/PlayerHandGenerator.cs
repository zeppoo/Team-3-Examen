using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHandGenerator : MonoBehaviour
{
   [SerializeField] private List<GameObject> playerHand = new List<GameObject>();
   [SerializeField]private List<Image> playerHandSymbols = new List<Image>();

    void Awake()
    {
        FindPlayers();
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
            int randomPlayerIndex = Random.Range(0, playerCount);
            playerSymbolCounts[randomPlayerIndex]++;
            leftovers--;
        }

        List<Sprite> availableSymbols = new List<Sprite>(symbols);

       
        for (int i = 0; i < playerCount; i++)
        {
            GameObject player = playerHand[i];
            int amountToGive = playerSymbolCounts[i];
            int assignedCount = 0;

            Image[] childImages = player.GetComponentsInChildren<Image>(true); 
            
            foreach (Image img in childImages)
            {
                
                if (img.gameObject == player) continue;

                
                if (assignedCount < amountToGive && availableSymbols.Count > 0)
                {
                    int randomIndex = Random.Range(0, availableSymbols.Count);
                    img.sprite = availableSymbols[randomIndex];
                    availableSymbols.RemoveAt(randomIndex);   

                    img.gameObject.SetActive(true); 
                    assignedCount++;
                }
                else
                {
                    
                    img.gameObject.SetActive(false);
                }
            }
        }
    }
}

