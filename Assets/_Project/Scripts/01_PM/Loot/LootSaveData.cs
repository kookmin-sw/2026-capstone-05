using System;
using System.Collections.Generic;

namespace Systems.Loot
{
    [Serializable]
    public class LootSaveData
    {
        public string lootId;
        public int width;
        public int height;
        public List<LootItemData> items = new List<LootItemData>();
    }

    [Serializable]
    public class LootItemData
    {
        public int slotIndex;
        public string itemId;
        public int itemCount;
        public int itemRotation;
        public float itemDurability;
    }

    [Serializable]
    public class LootSaveCollectionData
    {
        public List<LootSaveData> loots = new List<LootSaveData>();
    }
}
