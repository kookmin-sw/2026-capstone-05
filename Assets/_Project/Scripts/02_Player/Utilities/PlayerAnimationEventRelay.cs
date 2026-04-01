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

    public void OnUnarmedAttack()
    {
        if (animator != null)
        {
            animator.OnUnarmedAttackEvent(is1PModel);
        }
    }

    public void OnMeleeAttack()
    {
        if (animator != null)
        {
            animator.OnMeleeAttackEvent(is1PModel);
        }
    }
}
