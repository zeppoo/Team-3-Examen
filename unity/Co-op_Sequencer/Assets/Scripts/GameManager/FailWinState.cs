using UnityEngine;

public class FailWinState : MonoBehaviour
{
    [SerializeField] private GameObject gameOverHolder;
    [SerializeField] private LoseCondition loseCondition;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private HypeBar hypeBar;
    private bool hasFailed = false;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        hypeBar = FindObjectOfType<HypeBar>();
        loseCondition = gameOverHolder.GetComponent<LoseCondition>();
       
    }

    private void Update()
    {
        hasFailed = hypeBar.value <= 0f || (hypeBar.value <= 0.5f && gameManager.rounds.Count >= 11);
        if (hasFailed)
        {
            loseCondition.TriggerLoseCondition();
        }
       
    }
}
