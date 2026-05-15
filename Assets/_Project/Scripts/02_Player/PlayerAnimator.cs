using System.Collections.Generic;
using UnityEngine;

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
    private readonly int hashOnChangePose = Animator.StringToHash("OnChangePose");
    private readonly int hashPoseID = Animator.StringToHash("PoseID");
    private readonly int hashIsConsuming = Animator.StringToHash("IsConsuming");
    private readonly int hashIsDead = Animator.StringToHash("IsDead");

    private Dictionary<ItemUseAnimationType, int> itemUseAnimTriggers = new Dictionary<ItemUseAnimationType, int>
    {
        { ItemUseAnimationType.None, 0 },
        { ItemUseAnimationType.UnarmedAttack, 1 }, // Unarmed Attack
        { ItemUseAnimationType.MeleeAttack, 2 }, // Use Melee
        { ItemUseAnimationType.Throw, 3 }, // Throw
        { ItemUseAnimationType.ConsumeItem, 4 } // Consume Item
    };

    private Dictionary<ItemPoseType, int> itemPoseIDs = new Dictionary<ItemPoseType, int>
    {
        { ItemPoseType.Default, 0 },
        { ItemPoseType.Rifle, 1 }
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

    public void Die() => SetDead(true);
    public void Revive() => SetDead(false);

    public void SetDead(bool isDead)
    {
        if (animator1P != null)
        {
            animator1P.SetBool(hashIsDead, isDead);
            for (int i = 1; i < animator1P.layerCount; i++)
            {
                animator1P.SetLayerWeight(i, isDead ? 0f : 1f);
            }
        }
        if (animator3P != null)
        {
            animator3P.SetBool(hashIsDead, isDead);
            for (int i = 1; i < animator3P.layerCount; i++)
            {
                animator3P.SetLayerWeight(i, isDead ? 0f : 1f);
            }
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
        if (animType == ItemUseAnimationType.None)
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

    /// <summary>
    /// 아이템 데이터에 등록된 poseType을 받아 해당 포즈로 전환합니다.
    /// </summary>
    /// <param name="poseType"></param>
    public void SetItemPose(ItemPoseType poseType)
    {
        if (!itemPoseIDs.ContainsKey(poseType))
        {
            return;
        }

        int poseID = itemPoseIDs[poseType];

        if (animator1P != null)
        {
            animator1P.SetTrigger(hashOnChangePose);
            animator1P.SetInteger(hashPoseID, poseID);
        }
        if (animator3P != null)
        {
            animator3P.SetTrigger(hashOnChangePose);
            animator3P.SetInteger(hashPoseID, poseID);
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

    public void SetConsuming(bool isConsuming)
    {
        if (animator1P != null)
        {
            animator1P.SetBool(hashIsConsuming, isConsuming);
        }
        if (animator3P != null)
        {
            animator3P.SetBool(hashIsConsuming, isConsuming);
        }
    }
}
