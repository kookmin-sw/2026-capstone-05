using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerAnimator animator;

    [Header("Settings")]
    public bool is1PModel;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInParent<PlayerAnimator>();
        }
    }

    public void OnActionExecute()
    {
        if (animator != null)
        {
            animator.OnActionExecuteEvent(is1PModel);
        }
    }
}
