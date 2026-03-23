using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] private Animator animator1P;
    [SerializeField] private Animator animator3P;

    [Header("Animation Parameters")]
    public float speedDampTime = 0.1f;

    [Header("IK Settings")]
    public RigBuilder rigBuilder1P;
    [Range(0f, 1f)] public float ikWeight1P = 0f;

    private readonly int hashIsGrounded = Animator.StringToHash("IsGrounded");
    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashIsCrouching = Animator.StringToHash("IsCrouching");
    private readonly int hashIsSprinting = Animator.StringToHash("IsSprinting");

    private void Update()
    {
        if (rigBuilder1P != null && rigBuilder1P.layers.Count > 0)
        {
            rigBuilder1P.layers[0].rig.weight = ikWeight1P;
        }
    }

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
}
