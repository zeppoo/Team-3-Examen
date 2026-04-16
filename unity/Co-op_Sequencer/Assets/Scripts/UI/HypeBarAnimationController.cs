using UnityEngine;

public class HypeBarAnimationController : MonoBehaviour
{
    [SerializeField] private Animator hypeBarAnimator;
    [SerializeField] private HypeBar score;

    private void Start()
    {
        hypeBarAnimator = GetComponent<Animator>();
        score = GetComponent<HypeBar>();
    }


    public void Hit()
    {
        hypeBarAnimator.SetTrigger("hit");
    }

    public void HypeBarExplosion()
    {
        hypeBarAnimator.SetTrigger("explode");
        hypeBarAnimator.SetBool("hype", true);
    }

    public void ResetHypeBar()
    {
        hypeBarAnimator.ResetTrigger("explode");
        hypeBarAnimator.SetBool("hype", false);
        
    }

  
}
