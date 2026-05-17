using System;
using System.Collections.Generic;
using Systems.GridInventory;
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
        [SerializeField, Min(1)] private int placementAttemptsPerRoll = 8;
        [SerializeField] private List<LootTableEntry> lootTable = new List<LootTableEntry>();

        public string StorageIdPrefix => string.IsNullOrWhiteSpace(storageIdPrefix) ? "Loot" : storageIdPrefix.Trim();
        public string LootTitle => lootTitle;
        public int Width => Mathf.Max(1, lootWidth);
        public int Height => Mathf.Max(1, lootHeight);

        public List<ItemInstance> RollItems(string storageId)
        {
            List<ItemInstance> items = new List<ItemInstance>();
            if (!TryCreatePicker(out WeightedLootPicker picker) || maxItemRolls <= 0)
            {
                return items;
            }

            System.Random random = new System.Random(BuildStableSeed(storageId, name));
            int rollCount = GetRollCount(random);
            Dictionary<ItemData, int> generatedCounts = new Dictionary<ItemData, int>();

            for (int i = 0; i < rollCount; i++)
            {
                LootTableEntry entry = picker.Pick(random);
                if (entry?.itemData == null || !CanCreateMore(entry, generatedCounts))
                {
                    continue;
                }

                ItemInstance item = CreateItemInstance(entry, random, generatedCounts);
                if (item == null)
                {
                    continue;
                }

                items.Add(item);
                AddGeneratedCount(generatedCounts, entry.itemData, item.currentStackCount);
            }

            return items;
        }

        public int PopulateModel(GridInventoryModel model, string storageId)
        {
            if (model == null || !TryCreatePicker(out WeightedLootPicker picker) || maxItemRolls <= 0)
            {
                return 0;
            }

            System.Random random = new System.Random(BuildStableSeed(storageId, name));
            int rollCount = GetRollCount(random);
            int placedCount = 0;
            Dictionary<ItemData, int> generatedCounts = new Dictionary<ItemData, int>();

            for (int roll = 0; roll < rollCount; roll++)
            {
                for (int attempt = 0; attempt < placementAttemptsPerRoll; attempt++)
                {
                    LootTableEntry entry = picker.Pick(random);
                    if (entry?.itemData == null || !CanCreateMore(entry, generatedCounts))
                    {
                        continue;
                    }

                    ItemInstance item = CreateItemInstance(entry, random, generatedCounts);
                    if (item == null)
                    {
                        continue;
                    }

                    if (model.TryAdd(item))
                    {
                        AddGeneratedCount(generatedCounts, entry.itemData, item.currentStackCount);
                        placedCount++;
                        break;
                    }
                }
            }

            return placedCount;
        }

        private int GetRollCount(System.Random random)
        {
            int minRolls = Mathf.Clamp(minItemRolls, 0, maxItemRolls);
            int maxRolls = Mathf.Max(minRolls, maxItemRolls);
            return random.Next(minRolls, maxRolls + 1);
        }

        private static ItemInstance CreateItemInstance(
            LootTableEntry entry,
            System.Random random,
            Dictionary<ItemData, int> generatedCounts)
        {
            if (entry?.itemData == null)
            {
                return null;
            }

            int alreadyGenerated = GetGeneratedCount(generatedCounts, entry.itemData);
            int minQuantity = Mathf.Max(1, entry.minQuantity);
            int maxQuantity = Mathf.Max(minQuantity, entry.maxQuantity);
            int remainingQuantity = maxQuantity - alreadyGenerated;
            if (remainingQuantity <= 0)
            {
                return null;
            }

            minQuantity = Mathf.Min(minQuantity, remainingQuantity);
            maxQuantity = Mathf.Min(maxQuantity, remainingQuantity);
            int quantity = random.Next(minQuantity, maxQuantity + 1);
            return new ItemInstance(entry.itemData, quantity);
        }

        private static bool CanCreateMore(LootTableEntry entry, Dictionary<ItemData, int> generatedCounts)
        {
            if (entry?.itemData == null)
            {
                return false;
            }

            int maxQuantity = Mathf.Max(1, entry.maxQuantity);
            return GetGeneratedCount(generatedCounts, entry.itemData) < maxQuantity;
        }

        private static int GetGeneratedCount(Dictionary<ItemData, int> generatedCounts, ItemData itemData)
        {
            return itemData != null && generatedCounts.TryGetValue(itemData, out int count) ? count : 0;
        }

        private static void AddGeneratedCount(Dictionary<ItemData, int> generatedCounts, ItemData itemData, int amount)
        {
            if (itemData == null || amount <= 0)
            {
                return;
            }

            generatedCounts[itemData] = GetGeneratedCount(generatedCounts, itemData) + amount;
        }

        private bool TryCreatePicker(out WeightedLootPicker picker)
        {
            picker = default;
            if (lootTable == null || lootTable.Count == 0)
            {
                return false;
            }

            List<WeightedLootEntry> entries = new List<WeightedLootEntry>();
            float totalWeight = 0f;
            for (int i = 0; i < lootTable.Count; i++)
            {
                LootTableEntry entry = lootTable[i];
                if (entry?.itemData == null || entry.frequency <= 0f)
                {
                    continue;
                }

                totalWeight += entry.frequency;
                entries.Add(new WeightedLootEntry(entry, totalWeight));
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            picker = new WeightedLootPicker(entries, totalWeight);
            return true;
        }

        private void OnValidate()
        {
            lootWidth = Mathf.Max(1, lootWidth);
            lootHeight = Mathf.Max(1, lootHeight);
            minItemRolls = Mathf.Max(0, minItemRolls);
            maxItemRolls = Mathf.Max(minItemRolls, maxItemRolls);
            placementAttemptsPerRoll = Mathf.Max(1, placementAttemptsPerRoll);

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

        private readonly struct WeightedLootEntry
        {
            public readonly LootTableEntry Entry;
            public readonly float CumulativeWeight;

            public WeightedLootEntry(LootTableEntry entry, float cumulativeWeight)
            {
                Entry = entry;
                CumulativeWeight = cumulativeWeight;
            }
        }

        private readonly struct WeightedLootPicker
        {
            private readonly List<WeightedLootEntry> entries;
            private readonly float totalWeight;

            public WeightedLootPicker(List<WeightedLootEntry> entries, float totalWeight)
            {
                this.entries = entries;
                this.totalWeight = totalWeight;
            }

            public LootTableEntry Pick(System.Random random)
            {
                if (entries == null || entries.Count == 0 || totalWeight <= 0f)
                {
                    return null;
                }

                double pick = random.NextDouble() * totalWeight;
                int low = 0;
                int high = entries.Count - 1;
                while (low < high)
                {
                    int mid = low + ((high - low) / 2);
                    if (pick <= entries[mid].CumulativeWeight)
                    {
                        high = mid;
                    }
                    else
                    {
                        low = mid + 1;
                    }
                }

                return entries[low].Entry;
            }
        }
    }
}
