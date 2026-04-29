using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerIKManager : MonoBehaviour
{
    public enum ViewType { FirstPerson, ThirdPerson }

    [Header("Rig Type")]
    public ViewType viewType;

    [Header("Rig References")]
    public TwoBoneIKConstraint leftHandIK;
    public TwoBoneIKConstraint rightHandIK;
    public float lerpSpeed = 15f;

    private float baseLeftWeight = 0f;
    private float baseRightWeight = 1f;

    private int actionOverrideCount = 0;
    private float actionLeftWeight = 0f;
    private float actionRightWeight = 0f;

    private RigBuilder rigBuilder;

    private void Awake()
    {
        rigBuilder = GetComponent<RigBuilder>();
    }

    /// <summary>
    /// 무기를 장착/해제할 때 호출합니다.
    /// </summary>
    public void SetLeftHandWeaponGrip(Transform gripTarget)
    {
        leftHandIK.data.target = gripTarget;
        baseLeftWeight = gripTarget != null ? 1f : 0f;
        rigBuilder.Build();
    }

    public void SetActionIKOverride(bool isOverride, float left, float right)
    {
        if (isOverride)
        {
            actionOverrideCount++;
            actionLeftWeight = left;
            actionRightWeight = right;
        }
        else
        {
            actionOverrideCount--;
        }

        if (actionOverrideCount < 0)
        {
            actionOverrideCount = 0;
        }
    }

    private void Update()
    {
        bool isActionOverride = actionOverrideCount > 0;

        float targetLeft = isActionOverride ? actionLeftWeight : baseLeftWeight;
        float targetRight = isActionOverride ? actionRightWeight : baseRightWeight;

        if (leftHandIK != null)
        {
            leftHandIK.weight = Mathf.Lerp(leftHandIK.weight, targetLeft, Time.deltaTime * lerpSpeed);
        }
        if (rightHandIK != null)
        {
            rightHandIK.weight = Mathf.Lerp(rightHandIK.weight, targetRight, Time.deltaTime * lerpSpeed);
        }
    }
}