using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// 아이템 종류 당 하나씩 존재하는 데이터 클래스의 추상 클래스 (무기, 도구, 소비 아이템 등)
/// </summary>
public abstract class ItemData : ScriptableObject
{
    public abstract ItemType Type { get; }

    [Header("Base Info")]
    public string itemID;
    public LocalizedString itemName;
    public LocalizedString description;

    public string ItemNameString => (itemName != null && !itemName.IsEmpty) ? itemName.GetLocalizedString() : itemID;
    public virtual string DescriptionString => (description != null && !description.IsEmpty) ? description.GetLocalizedString() : "설명이 없습니다.";

    [Header("Inventory & UI")]
    public Sprite itemIcon;
    public ItemGridShape gridShape; // 인벤토리에서 차지하는 모양
    public int maxStackSize = 1; // 최대 스택 수 (1이면 스택 불가)
    //public float weight;

    [Header("World Interaction")]
    public GameObject pickupPrefab; // 바닥에 떨어졌을 때 보여질 프리팹 (ItemPickup)
    public GameObject equipPrefab; // 장착 시 보여질 프리팹 (무기, 도구 등) (EquippedItemBehaviour)

    [Header("Animation & Action Settings")]
    public ItemUseAnimationType useAnimationType = ItemUseAnimationType.None;
    public ItemPoseType poseType = ItemPoseType.Default; // 아이템 사용 시 플레이어가 취하는 포즈 타입
    public float actionCooldown = 1f; // 아이템 사용 후 행동이 재사용 가능해질 때까지의 시간 (초)

    [Header("Economic Value")]
    public bool isTradable = true;
    public int price = 0;
}