using System.Collections.Generic;
using UnityEngine;
using Systems.GridInventory;

namespace Systems.Loot
{
    [System.Serializable]
    public class LootItemSetup
    {
        public ItemData itemData;
        public int quantity = 1;
    }

    public class InteractableLoot : MonoBehaviour, IInteractable
    {
        [SerializeField] private string lootTitle = "Supply Chest";
        [SerializeField] private int gridCols = 8;
        [SerializeField] private int gridRows = 8;
        
        [SerializeField] private List<LootItemSetup> initialItems;
        
        private void Awake()
        {
            // 레퍼런스에 맞춰 양쪽 8x8 고정
            gridCols = 8;
            gridRows = 8;
        }
        
        private List<ItemInstance> currentItems = new List<ItemInstance>();
        private bool isInitialized = false;

        private void InitializeItems()
        {
            if (isInitialized) return;
            isInitialized = true;
            
            foreach (var setup in initialItems)
            {
                if (setup.itemData != null)
                {
                    currentItems.Add(new ItemInstance(setup.itemData, setup.quantity));
                }
            }
        }

        public bool CanInteract(PlayerController player)
        {
            // 루팅 창이 이미 열려있지 않다면 상호작용 가능
            return LootController.Instance != null && !LootController.Instance.IsOpen;
        }

        public string GetInteractPrompt()
        {
            return "열기";
        }

        public string GetObjectName()
        {
            return lootTitle;
        }

        public void OnInteract(PlayerController player)
        {
            InitializeItems();
            
            if (LootController.Instance != null)
            {
                LootController.Instance.OpenLoot(this, lootTitle, gridCols, gridRows, currentItems);
            }
        }

        public void SaveRemainingItems(List<ItemInstance> remaining)
        {
            currentItems = new List<ItemInstance>(remaining);
            
            // 만약 상자가 비었다면 애니메이션을 처리하거나 파괴하는 로직을 추가할 수 있습니다.
        }
    }
}
