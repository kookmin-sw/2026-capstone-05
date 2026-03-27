using UnityEngine;

public class IKWeightOverride : StateMachineBehaviour
{
    [Header("IK Target Weights during this Animation")]
    public float targetLeftHandWeight = 0f;
    public float targetRightHandWeight = 0f;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerIKManager ikManager = animator.GetComponent<PlayerIKManager>();
        if (ikManager != null)
        {
            ikManager.SetActionIKOverride(true, targetLeftHandWeight, targetRightHandWeight);
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerIKManager ikManager = animator.GetComponent<PlayerIKManager>();
        if (ikManager != null)
        {
            ikManager.SetActionIKOverride(false, 0f, 0f);
        }
    }
}