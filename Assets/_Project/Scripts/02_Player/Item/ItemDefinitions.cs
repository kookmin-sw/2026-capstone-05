using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Weapon,
    Consumable,
    Tool,
    Throwable,
    Misc
}

public enum ItemRotation
{
    Deg0 = 0,
    Deg90 = 1,
    Deg180 = 2,
    Deg270 = 3
}

public enum ItemUseAnimationType
{
    None,
    MeleeAttack,
    FirearmShoot,
    Eat,
    Drink,
    Throw,
    ToolUse
}

[System.Serializable]
public class ItemGridShape
{
    [Tooltip("아이템이 인벤토리에서 차지하는 칸들의 상대 좌표. 원점(0, 0)을 포함해야합니다.")]
    public List<Vector2Int> basePositions = new List<Vector2Int>() { Vector2Int.zero };

    /// <summary>
    /// 지정된 회전 상태에 맞게 변환된 좌표 리스트를 반환합니다.
    /// </summary>
    public List<Vector2Int> GetRotatedPositions(ItemRotation rotation)
    {
        if (rotation == ItemRotation.Deg0)
            return new List<Vector2Int>(basePositions);

        List<Vector2Int> rotatedPositions = new List<Vector2Int>();

        foreach (Vector2Int pos in basePositions)
        {
            Vector2Int newPos = Vector2Int.zero;

            switch (rotation)
            {
                case ItemRotation.Deg90:
                    newPos = new Vector2Int(pos.y, -pos.x);
                    break;
                case ItemRotation.Deg180:
                    newPos = new Vector2Int(-pos.x, -pos.y);
                    break;
                case ItemRotation.Deg270:
                    newPos = new Vector2Int(-pos.y, pos.x);
                    break;
            }
            rotatedPositions.Add(newPos);
        }

        return rotatedPositions;
    }
}