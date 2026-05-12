using System;
using System.Collections.Generic;

namespace Systems.StorageSystem
{
    [Serializable]
    public class StorageSaveData
    {
        public string storageId;
        public int width;
        public int height;
        public List<StorageItemData> items = new List<StorageItemData>();
    }

    [Serializable]
    public class StorageSaveCollectionData
    {
        public List<StorageSaveData> storages = new List<StorageSaveData>();
    }
}
