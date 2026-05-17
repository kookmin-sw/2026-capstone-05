using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.Loot
{
    [Serializable]
    public class LootTableEntry
    {
        public ItemData itemData;
        [Tooltip("Relative appearance score. 0 disables this item, 10 is the most common.")]
        [Range(0f, 10f)] public float frequency = 1f;
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;
    }

    [CreateAssetMenu(fileName = "New Loot Configuration", menuName = "Loot/Loot Container Definition")]
    public class LootConfiguration : ScriptableObject
    {
        [Header("Container Definition")]
        [SerializeField] private string storageIdPrefix = "Loot";
        [SerializeField] private string lootTitle = "Supply Chest";
        [SerializeField, Min(1)] private int lootWidth = 9;
        [SerializeField, Min(1)] private int lootHeight = 18;

        [Header("Generated Items")]
        [SerializeField, Min(0)] private int minItemRolls = 1;
        [SerializeField, Min(0)] private int maxItemRolls = 3;
        [SerializeField] private List<LootTableEntry> lootTable = new List<LootTableEntry>();

        public string StorageIdPrefix => string.IsNullOrWhiteSpace(storageIdPrefix) ? "Loot" : storageIdPrefix.Trim();
        public string LootTitle => lootTitle;
        public int Width => Mathf.Max(1, lootWidth);
        public int Height => Mathf.Max(1, lootHeight);

        public List<ItemInstance> RollItems(string storageId)
        {
            List<ItemInstance> items = new List<ItemInstance>();
            if (lootTable == null || lootTable.Count == 0 || maxItemRolls <= 0)
            {
                return items;
            }

            int minRolls = Mathf.Clamp(minItemRolls, 0, maxItemRolls);
            int maxRolls = Mathf.Max(minRolls, maxItemRolls);
            System.Random random = new System.Random(BuildStableSeed(storageId, name));
            int rollCount = random.Next(minRolls, maxRolls + 1);

            for (int i = 0; i < rollCount; i++)
            {
                LootTableEntry entry = PickEntry(random);
                if (entry?.itemData == null)
                {
                    continue;
                }

                int minQuantity = Mathf.Max(1, entry.minQuantity);
                int maxQuantity = Mathf.Max(minQuantity, entry.maxQuantity);
                int quantity = random.Next(minQuantity, maxQuantity + 1);
                items.Add(new ItemInstance(entry.itemData, quantity));
            }

            return items;
        }

        private LootTableEntry PickEntry(System.Random random)
        {
            float totalWeight = 0f;
            for (int i = 0; i < lootTable.Count; i++)
            {
                LootTableEntry entry = lootTable[i];
                if (entry?.itemData == null || entry.frequency <= 0f)
                {
                    continue;
                }

                totalWeight += entry.frequency;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            double pick = random.NextDouble() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < lootTable.Count; i++)
            {
                LootTableEntry entry = lootTable[i];
                if (entry?.itemData == null || entry.frequency <= 0f)
                {
                    continue;
                }

                cumulative += entry.frequency;
                if (pick <= cumulative)
                {
                    return entry;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            lootWidth = Mathf.Max(1, lootWidth);
            lootHeight = Mathf.Max(1, lootHeight);
            minItemRolls = Mathf.Max(0, minItemRolls);
            maxItemRolls = Mathf.Max(minItemRolls, maxItemRolls);

            if (lootTable == null)
            {
                return;
            }

            foreach (LootTableEntry entry in lootTable)
            {
                if (entry == null)
                {
                    continue;
                }

                entry.frequency = Mathf.Clamp(entry.frequency, 0f, 10f);
                entry.minQuantity = Mathf.Max(1, entry.minQuantity);
                entry.maxQuantity = Mathf.Max(entry.minQuantity, entry.maxQuantity);
            }
        }

        private static int BuildStableSeed(string storageId, string definitionName)
        {
            unchecked
            {
                int hash = (int)2166136261;
                AppendStableHash(ref hash, storageId);
                AppendStableHash(ref hash, definitionName);
                return hash == 0 ? 1 : hash;
            }
        }

        private static void AppendStableHash(ref int hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            unchecked
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
            }
        }
    }
}
