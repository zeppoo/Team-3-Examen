using UnityEngine;

public class HypeBarAnimationController : MonoBehaviour
{
    [SerializeField] private Animator hypeBarAnimator;

    private void Start()
    {
        hypeBarAnimator = GetComponent<Animator>();
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
