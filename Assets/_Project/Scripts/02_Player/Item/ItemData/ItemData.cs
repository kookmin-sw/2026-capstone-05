using UnityEngine;

/// <summary>
/// 아이템 종류 당 하나씩 존재하는 데이터 클래스의 추상 클래스 (무기, 도구, 소비 아이템 등)
/// </summary>
public abstract class ItemData : ScriptableObject
{
    public abstract ItemType Type { get; }

    [Header("Base Info")]
    public string itemID;
    // TODO: Localization 적용
    public string itemName;
    [TextArea(2, 4)]
    public string description;

    [Header("Inventory & UI")]
    public Sprite itemIcon;
    public ItemGridShape gridShape; // 인벤토리에서 차지하는 모양
    public int maxStackSize; // 최대 스택 수 (1이면 스택 불가)
    //public float weight;

    [Header("World Interaction")]
    public GameObject pickupPrefab; // 바닥에 떨어졌을 때 보여질 프리팹
    public GameObject equipPrefab; // 장착 시 보여질 프리팹 (무기, 도구 등)

    [Header("Animation & Action Settings")]
    public ItemUseAnimationType useAnimationType = ItemUseAnimationType.None;
    public float actionCooldown = 1f; // 아이템 사용 후 행동이 재사용 가능해질 때까지의 시간 (초)

    [Header("Economic Value")]
    public bool isTradable = true;
    public int price = 0;
}