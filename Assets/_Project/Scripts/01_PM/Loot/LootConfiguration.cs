using System.Collections.Generic;
using UnityEngine;

namespace Systems.Loot
{
    [CreateAssetMenu(fileName = "New Loot Configuration", menuName = "Loot/Loot Configuration")]
    public class LootConfiguration : ScriptableObject
    {
        [Header("Loot Configuration")]
        [Tooltip("Used as the save/network key. Use a unique value for each persistent loot container.")]
        [SerializeField] private string storageId = "Loot_0";
        [SerializeField] private string lootTitle = "Supply Chest";
        [SerializeField, Min(1)] private int lootWidth = 9;
        [SerializeField, Min(1)] private int lootHeight = 18;

        [Header("Initial Items (Only spawned if storage is empty)")]
        [SerializeField] private List<LootItemSetup> initialItems = new List<LootItemSetup>();

        public string StorageId => string.IsNullOrWhiteSpace(storageId) ? "Loot_0" : storageId.Trim();
        public string LootTitle => lootTitle;
        public int Width => Mathf.Max(1, lootWidth);
        public int Height => Mathf.Max(1, lootHeight);
        public IReadOnlyList<LootItemSetup> InitialItems => initialItems;

        private void OnValidate()
        {
            lootWidth = Mathf.Max(1, lootWidth);
            lootHeight = Mathf.Max(1, lootHeight);
        }
    }
}
