using System;

namespace Systems.StorageSystem
{
    [Serializable]
    public class StorageItemData
    {
        public int slotIndex;
        public string itemId;
        public int itemCount;
        public int itemRotation;
        public float itemDurability;
    }
}
