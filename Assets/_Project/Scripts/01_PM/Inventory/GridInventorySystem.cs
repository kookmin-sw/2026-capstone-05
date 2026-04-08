using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 인벤토리 저장 데이터 구조
/// </summary>
[System.Serializable]
public class InventorySaveData
{
    public List<SavedInventoryItem> items = new List<SavedInventoryItem>();
}

[System.Serializable]
public class SavedInventoryItem
{
    public string itemID;
    public int slotX;
    public int slotY;
    public int quantity;
    public ItemRotation rotation;
    
    public SavedInventoryItem(string id, int x, int y, int qty, ItemRotation rot)
    {
        itemID = id;
        slotX = x;
        slotY = y;
        quantity = qty;
        rotation = rot;
    }
}

namespace Systems.Inventory
{
    /// <summary>
    /// ItemInstance 기반 그리드 인벤토리 시스템
    /// 데이터베이스 구조 (slot_x, slot_y, size_x, size_y, rotated)를 지원
    /// </summary>
    [System.Serializable]
    public class GridInventorySlot
    {
        public ItemInstance itemInstance;
        public int slotX;
        public int slotY;
        public int sizeX;
        public int sizeY;
        public bool isRotated;

        public GridInventorySlot(ItemInstance instance, int x, int y, int sX, int sY, bool rotated = false)
        {
            itemInstance = instance;
            slotX = x;
            slotY = y;
            sizeX = sX;
            sizeY = sY;
            isRotated = rotated;
        }
    }

    public class GridInventorySystem : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 8;
        [SerializeField] private int gridHeight = 10;

        [Header("UI References")]
        [SerializeField] private GridInventoryUI inventoryUI;

        // 그리드 상태 추적 (어떤 슬롯이 점유되었는지)
        private ItemInstance[,] gridSlots;
        
        // 실제 아이템 인스턴스들 저장
        private List<GridInventorySlot> inventorySlots = new List<GridInventorySlot>();

        public event System.Action<List<GridInventorySlot>> OnInventoryChanged;

        private void Awake()
        {
            gridSlots = new ItemInstance[gridWidth, gridHeight];
            
            if (inventoryUI != null)
            {
                inventoryUI.Initialize(this, gridWidth, gridHeight);
            }
        }

        /// <summary>
        /// 아이템을 인벤토리에 추가 시도
        /// </summary>
        public bool TryAddItem(ItemInstance itemInstance)
        {
            if (itemInstance?.Data == null) return false;

            // ItemData에서 그리드 모양 정보 가져오기
            var gridShape = itemInstance.Data.gridShape;
            if (gridShape == null || gridShape.basePositions == null || gridShape.basePositions.Count == 0)
            {
                // 기본 1x1 아이템으로 처리
                return TryAddItem_Simple(itemInstance, 1, 1);
            }

            // 복잡한 모양의 아이템 처리
            return TryAddItem_Complex(itemInstance, gridShape);
        }

        /// <summary>
        /// 간단한 사각형 아이템 추가
        /// </summary>
        private bool TryAddItem_Simple(ItemInstance itemInstance, int itemWidth, int itemHeight)
        {
            for (int y = 0; y <= gridHeight - itemHeight; y++)
            {
                for (int x = 0; x <= gridWidth - itemWidth; x++)
                {
                    if (CanPlaceAt(x, y, itemWidth, itemHeight))
                    {
                        PlaceItem(itemInstance, x, y, itemWidth, itemHeight, false);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 복잡한 모양의 아이템 추가 (회전 지원)
        /// </summary>
        private bool TryAddItem_Complex(ItemInstance itemInstance, ItemGridShape gridShape)
        {
            // 0도, 90도, 180도, 270도 회전 시도
            for (int rotation = 0; rotation < 4; rotation++)
            {
                ItemRotation rotationEnum = (ItemRotation)rotation;
                var positions = gridShape.GetRotatedPositions(rotationEnum);
                
                var (minX, maxX, minY, maxY) = GetBounds(positions);
                int width = maxX - minX + 1;
                int height = maxY - minY + 1;

                for (int y = -minY; y <= gridHeight - height + minY; y++)
                {
                    for (int x = -minX; x <= gridWidth - width + minX; x++)
                    {
                        if (CanPlaceAt_Complex(x, y, positions))
                        {
                            PlaceItem_Complex(itemInstance, x, y, positions, rotationEnum != ItemRotation.Deg0);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 지정된 위치에 사각형 아이템을 배치할 수 있는지 확인
        /// </summary>
        private bool CanPlaceAt(int startX, int startY, int width, int height)
        {
            if (startX + width > gridWidth || startY + height > gridHeight) return false;

            for (int y = startY; y < startY + height; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    if (gridSlots[x, y] != null) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 지정된 위치에 복잡한 모양의 아이템을 배치할 수 있는지 확인
        /// </summary>
        private bool CanPlaceAt_Complex(int baseX, int baseY, List<Vector2Int> positions)
        {
            foreach (var pos in positions)
            {
                int x = baseX + pos.x;
                int y = baseY + pos.y;
                
                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
                if (gridSlots[x, y] != null) return false;
            }
            return true;
        }

        /// <summary>
        /// 사각형 아이템 배치
        /// </summary>
        private void PlaceItem(ItemInstance itemInstance, int startX, int startY, int width, int height, bool isRotated)
        {
            // 그리드 점유
            for (int y = startY; y < startY + height; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    gridSlots[x, y] = itemInstance;
                }
            }

            // 인벤토리 슬롯에 추가
            var slot = new GridInventorySlot(itemInstance, startX, startY, width, height, isRotated);
            inventorySlots.Add(slot);

            // UI 업데이트
            OnInventoryChanged?.Invoke(inventorySlots);
        }

        /// <summary>
        /// 복잡한 모양 아이템 배치
        /// </summary>
        private void PlaceItem_Complex(ItemInstance itemInstance, int baseX, int baseY, List<Vector2Int> positions, bool isRotated)
        {
            // 그리드 점유
            foreach (var pos in positions)
            {
                int x = baseX + pos.x;
                int y = baseY + pos.y;
                gridSlots[x, y] = itemInstance;
            }

            // 바운드 박스 계산
            var (minX, maxX, minY, maxY) = GetBounds(positions);
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            // 인벤토리 슬롯에 추가
            var slot = new GridInventorySlot(itemInstance, baseX + minX, baseY + minY, width, height, isRotated);
            inventorySlots.Add(slot);

            // UI 업데이트
            OnInventoryChanged?.Invoke(inventorySlots);
        }

        /// <summary>
        /// 아이템 제거
        /// </summary>
        public bool RemoveItem(ItemInstance itemInstance)
        {
            var slot = inventorySlots.Find(s => s.itemInstance == itemInstance);
            if (slot == null) return false;

            inventorySlots.Remove(slot);

            // 그리드에서 제거
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (gridSlots[x, y] == itemInstance)
                    {
                        gridSlots[x, y] = null;
                    }
                }
            }

            OnInventoryChanged?.Invoke(inventorySlots);
            return true;
        }

        /// <summary>
        /// 위치 리스트에서 바운드 박스 계산
        /// </summary>
        private (int minX, int maxX, int minY, int maxY) GetBounds(List<Vector2Int> positions)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var pos in positions)
            {
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxY = Mathf.Max(maxY, pos.y);
            }

            return (minX, maxX, minY, maxY);
        }

        /// <summary>
        /// 현재 인벤토리 상태 반환
        /// </summary>
        public List<GridInventorySlot> GetAllItems()
        {
            return new List<GridInventorySlot>(inventorySlots);
        }

        /// <summary>
        /// 인벤토리 데이터를 JSON 파일로 저장
        /// </summary>
        public void SaveInventory()
        {
            var saveData = new InventorySaveData();
            
            foreach (var slot in inventorySlots)
            {
                var item = slot.itemInstance;
                var savedItem = new SavedInventoryItem(
                    item.Data.itemID,
                    slot.slotX,
                    slot.slotY, 
                    item.currentStackCount,
                    item.currentRotation
                );
                saveData.items.Add(savedItem);
            }
            
            string json = JsonUtility.ToJson(saveData, true);
            string savePath = GetSavePath();
            
            try
            {
                System.IO.File.WriteAllText(savePath, json);
                Debug.Log($"[인벤토리] 저장 완료: {savePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[인벤토리] 저장 실패: {e.Message}");
            }
        }
        
        /// <summary>
        /// JSON 파일에서 인벤토리 데이터 로드
        /// </summary>
        public void LoadInventory()
        {
            string savePath = GetSavePath();
            
            if (!System.IO.File.Exists(savePath))
            {
                Debug.Log("[인벤토리] 저장 파일이 없습니다.");
                return;
            }
            
            try
            {
                string json = System.IO.File.ReadAllText(savePath);
                var saveData = JsonUtility.FromJson<InventorySaveData>(json);
                
                // 기존 인벤토리 비우기
                ClearInventory();
                
                // 저장된 아이템들 복원
                foreach (var savedItem in saveData.items)
                {
                    // ItemData 찾기
                    var itemData = FindItemDataByID(savedItem.itemID);
                    if (itemData == null)
                    {
                        Debug.LogWarning($"[인벤토리] 아이템 ID를 찾을 수 없음: {savedItem.itemID}");
                        continue;
                    }
                    
                    // ItemInstance 생성
                    var itemInstance = new ItemInstance(itemData, savedItem.quantity);
                    itemInstance.currentRotation = savedItem.rotation;
                    
                    // 특정 위치에 배치
                    if (!TryAddItemAt(itemInstance, savedItem.slotX, savedItem.slotY))
                    {
                        Debug.LogWarning($"[인벤토리] 아이템 배치 실패: {savedItem.itemID} at ({savedItem.slotX}, {savedItem.slotY})");
                    }
                }
                
                Debug.Log($"[인벤토리] 로드 완료: {saveData.items.Count}개 아이템");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[인벤토리] 로드 실패: {e.Message}");
            }
        }
        
        /// <summary>
        /// 모든 아이템 제거
        /// </summary>
        public void ClearInventory()
        {
            inventorySlots.Clear();
            
            // 그리드 초기화
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    gridSlots[x, y] = null;
                }
            }
            
            OnInventoryChanged?.Invoke(inventorySlots);
        }
        
        /// <summary>
        /// 저장 파일 경로 반환
        /// </summary>
        private string GetSavePath()
        {
            string folderPath = Path.Combine(Application.dataPath, "_Project", "JSON", "PM");
            
            // 폴더가 없으면 생성
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            
            return Path.Combine(folderPath, "inventory_save.json");
        }
        
        /// <summary>
        /// itemID로 ItemData 찾기
        /// </summary>
        private ItemData FindItemDataByID(string itemID)
        {
            // 등록된 모든 ItemData 에셋에서 찾기
            var allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
            foreach (var data in allItemData)
            {
                if (data.itemID == itemID)
                {
                    return data;
                }
            }
            return null;
        }
        public bool TryAddItemAt(ItemInstance itemInstance, int targetX, int targetY)
        {
            if (itemInstance?.Data == null) return false;

            var gridShape = itemInstance.Data.gridShape;
            
            // 기본 1x1 아이템 처리
            if (gridShape == null || gridShape.basePositions == null || gridShape.basePositions.Count == 0)
            {
                // 기본 1x1 크기
                if (!CanPlaceAt(targetX, targetY, 1, 1)) return false;
                PlaceItem(itemInstance, targetX, targetY, 1, 1, false);
                return true;
            }

            // 복잡한 모양 아이템 처리
            var positions = gridShape.GetRotatedPositions(itemInstance.currentRotation);
            var (minX, maxX, minY, maxY) = GetBounds(positions);
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            // 경계 체크
            if (targetX < -minX || targetY < -minY || 
                targetX + width - minX > gridWidth || 
                targetY + height - minY > gridHeight)
                return false;

            // 해당 위치에 배치 가능한지 확인
            if (!CanPlaceAt_Complex(targetX, targetY, positions))
                return false;

            // 아이템 배치
            PlaceItem_Complex(itemInstance, targetX, targetY, positions, itemInstance.currentRotation != ItemRotation.Deg0);
            return true;
        }

        /// <summary>
        /// 특정 위치의 아이템 반환
        /// </summary>
        public ItemInstance GetItemAt(int x, int y)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return null;
            return gridSlots[x, y];
        }

    }
}