using UnityEngine;

[CreateAssetMenu(fileName = "New Flashlight", menuName = "Item Data/Tool/Flashlight")]
public class FlashlightData : ToolItemData
{
    [Header("Flashlight Settings")]
    public float lightRange; // 빛의 범위
    public float lightIntensity; // 빛의 세기
    public Color lightColor = Color.white; // 빛의 색상

    private void Reset()
    {
        useAnimationType = ItemUseAnimationType.None;
        poseType = ItemPoseType.RaiseOneHand;
    }
}
