using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;

    [Header("Animators")]
    [SerializeField] private Animator animator1P;
    [SerializeField] private Animator animator3P;

    [Header("Animation Parameters")]
    public float speedDampTime = 0.1f;

    private readonly int hashIsGrounded = Animator.StringToHash("IsGrounded");
    private readonly int hashIsCrouching = Animator.StringToHash("IsCrouching");
    private readonly int hashIsSprinting = Animator.StringToHash("IsSprinting");
    private readonly int hashOnHit = Animator.StringToHash("OnHit");
    private readonly int hashOnAction = Animator.StringToHash("OnAction");
    private readonly int hashActionID = Animator.StringToHash("ActionID");

    private Dictionary<ItemUseAnimationType, int> itemUseAnimTriggers = new Dictionary<ItemUseAnimationType, int>
    {
        { ItemUseAnimationType.None, 0 }, // Unarmed Attack
        { ItemUseAnimationType.MeleeAttack, 1 }, // Use Melee
        { ItemUseAnimationType.FirearmShoot, 2 }, // Use Firearm
        { ItemUseAnimationType.Eat, 3 }, // Eat
        { ItemUseAnimationType.Drink, 4 }, // Drink
        { ItemUseAnimationType.Throw, 5 }, // Throw
        { ItemUseAnimationType.ToolUse, 6 } // Use Tool
    };

    public void SetGrounded(bool isGrounded)
    {
        if (animator1P != null)
        {
            animator1P.SetBool(hashIsGrounded, isGrounded);
        }
        if (animator3P != null)
        {
            animator3P.SetBool(hashIsGrounded, isGrounded);
        }
    }

    public void UpdateMovement(Vector2 moveInput) // Normalized move (0 to 1)
    {
        if (animator1P != null)
        {
            animator1P.SetFloat("MoveX", moveInput.x, speedDampTime, Time.deltaTime);
            animator1P.SetFloat("MoveY", moveInput.y, speedDampTime, Time.deltaTime);
        }
        if (animator3P != null)
        {
            animator3P.SetFloat("MoveX", moveInput.x, speedDampTime, Time.deltaTime);
            animator3P.SetFloat("MoveY", moveInput.y, speedDampTime, Time.deltaTime);
        }
    }

    public void SetCrouching(bool isCrouching)
    {
        if (animator1P != null)
        {
            animator1P.SetBool(hashIsCrouching, isCrouching);
        }
        if (animator3P != null)
        {
            animator3P.SetBool(hashIsCrouching, isCrouching);
        }
    }

    public void SetSprinting(bool isSprinting)
    {
        if (animator1P != null)
        {
            animator1P.SetBool(hashIsSprinting, isSprinting);
        }
        if (animator3P != null)
        {
            animator3P.SetBool(hashIsSprinting, isSprinting);
        }
    }

    public void SetHitTrigger(float damageAmount)
    {
        if (animator1P != null)
        {
            animator1P.SetTrigger(hashOnHit);
        }
        if (animator3P != null)
        {
            animator3P.SetTrigger(hashOnHit);
        }
    }

    /// <summary>
    /// 아이템 데이터에 등록된 이름표(String)를 받아 해당 애니메이션 트리거를 작동시킵니다.
    /// </summary>
    public void PlayUseItemAnimation(ItemUseAnimationType animType)
    {
        if (!itemUseAnimTriggers.ContainsKey(animType))
        {
            return;
        }

        int actionID = itemUseAnimTriggers[animType];

        if (animator1P != null)
        {
            animator1P.SetInteger(hashActionID, actionID);
            animator1P.SetTrigger(hashOnAction);
        }
        if (animator3P != null)
        {
            animator3P.SetInteger(hashActionID, actionID);
            animator3P.SetTrigger(hashOnAction);
        }
    }


    public void OnActionExecuteEvent(bool is1PModel)
    {
        if (player.IsLocalPlayer && !is1PModel)
        {
            return;
        }

        player.Equipment.HandleAnimationEvent();
    }
}
