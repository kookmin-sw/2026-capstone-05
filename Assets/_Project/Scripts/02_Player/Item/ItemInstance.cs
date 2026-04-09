using UnityEngine;

/// <summary>
/// 실제 게임 내에서 아이템 하나하나를 나타내는 클래스
/// </summary>
[System.Serializable]
public class ItemInstance
{
    [SerializeField] private ItemData data;
    public ItemData Data => data;

    public ItemRotation currentRotation = ItemRotation.Deg0;

    public int currentStackCount = 1;

    public ItemInstance(ItemData data, int amount = 1)
    {
        if (data == null)
        {
            Debug.LogError("ItemData cannot be null when creating an ItemInstance.");
            return;
        }

        this.data = data;

        currentStackCount = Mathf.Clamp(amount, 1, data.maxStackSize);
    }

    public void Rotate(bool isClockwise)
    {
        if (isClockwise)
        {
            int nextRotation = ((int)currentRotation + 1) % 4;
            currentRotation = (ItemRotation)nextRotation;
        }
        else
        {
            int nextRotation = ((int)currentRotation - 1 + 4) % 4;
            currentRotation = (ItemRotation)nextRotation;
        }
    }
}