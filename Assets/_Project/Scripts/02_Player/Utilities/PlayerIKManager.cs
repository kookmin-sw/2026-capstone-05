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
    private bool isActionOverride = false;
    private float actionLeftWeight = 0f;
    private float actionRightWeight = 0f;

    //public void OnItemChanged(ItemData newItem)
    //{
    //    if (newItem != null)
    //    {
    //        IKSetting mySetting = (viewType == ViewType.FirstPerson) ? newItem.ik1P : newItem.ik3P;

    //        baseLeftWeight = mySetting.leftHandWeight;
    //        baseRightWeight = mySetting.rightHandWeight;
    //    }
    //}

    public void SetActionIKOverride(bool isOverride, float left, float right)
    {
        isActionOverride = isOverride;
        actionLeftWeight = left;
        actionRightWeight = right;
    }

    private void Update()
    {
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