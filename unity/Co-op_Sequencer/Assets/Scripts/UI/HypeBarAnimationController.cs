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
        hypeBarAnimator.SetTrigger("Hit");
    }

    public void HypeBarExplosion()
    {
        hypeBarAnimator.SetTrigger("explode");
        hypeBarAnimator.SetBool("hype", true);
    }

    public void ResetHypeBar()
    {
        hypeBarAnimator.SetBool("hype", false);
    }
}
