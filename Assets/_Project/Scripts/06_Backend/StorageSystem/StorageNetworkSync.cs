using System.Collections.Generic;
using System.Linq;
using Fusion;
using Systems.GridInventory;
using UnityEngine;

namespace Systems.StorageSystem
{
    [RequireComponent(typeof(NetworkObject))]
    public class StorageNetworkSync : NetworkBehaviour
    {
        [Header("Storage")]
        [SerializeField] private int storageWidth = 9;
        [SerializeField] private int storageHeight = 18;
        [SerializeField] private string[] defaultStorageIds = { "Storage_0", "Storage_1" };

        public static StorageNetworkSync Instance { get; private set; }

        private readonly Dictionary<string, GridInventoryModel> modelsById = new Dictionary<string, GridInventoryModel>();
        private readonly HashSet<string> dirtyStorageIds = new HashSet<string>();
        private readonly Dictionary<int, IncomingPayload> incomingPayloads = new Dictionary<int, IncomingPayload>();
        private int outboundSequence;
        private int localOperationSequence;

        public int StorageWidth => storageWidth;
        public int StorageHeight => storageHeight;
        public bool IsAuthoritative => HasStateAuthority;
        public event System.Action<int, bool, string, int, int> OnStorageOperationConfirmed;

        public override void Spawned()
        {
            Instance = this;
            StorageRackRuntimeBinder.BindDefaultRacks(this);
            EnsureSceneStorageModels();

            if (HasStateAuthority)
            {
                LoadAllStorages();
                BroadcastAllStorages(PlayerRef.None);
                return;
            }

            if (Runner != null)
            {
                RpcRequestFullSync(Runner.LocalPlayer);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SubmitStorageSnapshot(string storageId, StorageSaveData snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.storageId = NormalizeStorageId(storageId);
            string json = JsonUtility.ToJson(snapshot);

            if (HasStateAuthority)
            {
                ApplyStorageSnapshot(json);
                return;
            }

            SendSnapshotRequestChunks(json);
        }

        public int SubmitMoveItem(string storageId, int fromSlotIndex, int toSlotIndex, int rotation)
        {
            int requestId;
            string id = NormalizeStorageId(storageId);
            if (HasStateAuthority)
            {
                requestId = NextOperationId();
                return ApplyMoveItem(id, fromSlotIndex, toSlotIndex, rotation) ? requestId : 0;
            }

            if (Runner == null)
            {
                return 0;
            }

            requestId = NextOperationId();
            RpcRequestMoveItem(Runner.LocalPlayer, requestId, id, fromSlotIndex, toSlotIndex, rotation);
            return requestId;
        }

        public int SubmitPutItem(string storageId, int targetSlotIndex, ItemInstance item)
        {
            int requestId;
            if (item?.Data == null)
            {
                return 0;
            }

            string id = NormalizeStorageId(storageId);
            if (HasStateAuthority)
            {
                requestId = NextOperationId();
                return ApplyPutItem(id, targetSlotIndex, item.Data.itemID, item.currentStackCount, (int)item.currentRotation) ? requestId : 0;
            }

            if (Runner == null)
            {
                return 0;
            }

            requestId = NextOperationId();
            RpcRequestPutItem(Runner.LocalPlayer, requestId, id, targetSlotIndex, item.Data.itemID, item.currentStackCount, (int)item.currentRotation);
            return requestId;
        }

        public int SubmitRemoveItem(string storageId, int sourceSlotIndex, int rotation = -1)
        {
            int requestId;
            string id = NormalizeStorageId(storageId);
            if (HasStateAuthority)
            {
                requestId = NextOperationId();
                return ApplyRemoveItem(id, sourceSlotIndex, rotation, out _, out _, out _) ? requestId : 0;
            }

            if (Runner == null)
            {
                return 0;
            }

            requestId = NextOperationId();
            RpcRequestRemoveItem(Runner.LocalPlayer, requestId, id, sourceSlotIndex, rotation);
            return requestId;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestMoveItem(PlayerRef requestedBy, int requestId, NetworkString<_64> storageId, int fromSlotIndex, int toSlotIndex, int rotation)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            bool success = ApplyMoveItem(storageId.ToString(), fromSlotIndex, toSlotIndex, rotation);
            RpcConfirmStorageOperation(requestedBy, requestId, success, string.Empty, 0, 0);
            if (!success)
            {
                BroadcastAllStorages(requestedBy);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestPutItem(PlayerRef requestedBy, int requestId, NetworkString<_64> storageId, int targetSlotIndex, NetworkString<_128> itemId, int count, int rotation)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            string itemIdText = itemId.ToString();
            bool success = ApplyPutItem(storageId.ToString(), targetSlotIndex, itemIdText, count, rotation);
            RpcConfirmStorageOperation(requestedBy, requestId, success, itemIdText, count, rotation);
            if (!success)
            {
                BroadcastAllStorages(requestedBy);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestRemoveItem(PlayerRef requestedBy, int requestId, NetworkString<_64> storageId, int sourceSlotIndex, int rotation)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            string itemId = string.Empty;
            int count = 0;
            int confirmedRotation = 0;
            bool success = ApplyRemoveItem(storageId.ToString(), sourceSlotIndex, rotation, out itemId, out count, out confirmedRotation);
            RpcConfirmStorageOperation(requestedBy, requestId, success, itemId, count, confirmedRotation);
            if (!success)
            {
                BroadcastAllStorages(requestedBy);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcConfirmStorageOperation(PlayerRef targetPlayer, int requestId, NetworkBool success, NetworkString<_128> itemId, int count, int rotation)
        {
            if (Runner != null && targetPlayer != PlayerRef.None && targetPlayer != Runner.LocalPlayer)
            {
                return;
            }

            OnStorageOperationConfirmed?.Invoke(requestId, success, itemId.ToString(), count, rotation);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestReplaceStorageChunk(PlayerRef requestedBy, int sequence, int chunkIndex, int totalChunks, NetworkString<_64> chunk)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            int key = BuildIncomingRequestKey(requestedBy, sequence);
            if (!incomingPayloads.TryGetValue(key, out IncomingPayload incoming))
            {
                incoming = new IncomingPayload(totalChunks);
                incomingPayloads[key] = incoming;
            }

            incoming.SetChunk(chunkIndex, chunk.ToString());
            if (!incoming.IsComplete)
            {
                return;
            }

            incomingPayloads.Remove(key);
            ApplyStorageSnapshot(incoming.Combine());
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestFullSync(PlayerRef requestedBy)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            BroadcastAllStorages(requestedBy);
        }

        private void ApplyStorageSnapshot(string json)
        {
            if (!HasStateAuthority || string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            StorageSaveData data = JsonUtility.FromJson<StorageSaveData>(json);
            if (data == null || string.IsNullOrWhiteSpace(data.storageId))
            {
                return;
            }

            data.storageId = NormalizeStorageId(data.storageId);
            data.width = storageWidth;
            data.height = storageHeight;
            GridInventoryModel model = GetOrCreateModel(data.storageId);
            StorageGridSerializer.ApplyToModel(data, model);
            dirtyStorageIds.Add(data.storageId);
            SaveStorageIfDirty(data.storageId);
            BroadcastAllStorages(PlayerRef.None);
        }

        private void MarkStorageDirtyAndBroadcast(string storageId)
        {
            string id = NormalizeStorageId(storageId);
            if (!modelsById.ContainsKey(id))
            {
                return;
            }

            dirtyStorageIds.Add(id);
            SaveStorageIfDirty(id);
            BroadcastAllStorages(PlayerRef.None);
        }

        private bool ApplyMoveItem(string storageId, int fromSlotIndex, int toSlotIndex, int rotation)
        {
            string id = NormalizeStorageId(storageId);
            GridInventoryModel model = GetOrCreateModel(id);
            ItemInstance item = model.Get(fromSlotIndex);
            if (item == null)
            {
                return false;
            }

            (int oldX, int oldY) = model.GetItemAnchorPosition(item);
            (int newX, int newY) = model.GetCoordinates(toSlotIndex);
            ItemRotation oldRotation = item.currentRotation;
            item.currentRotation = (ItemRotation)Mathf.Clamp(rotation, 0, 3);

            model.TryRemove(item);
            if (!PlaceOrStackInModel(model, item, newX, newY))
            {
                item.currentRotation = oldRotation;
                model.PlaceItem(item, oldX, oldY);
                return false;
            }

            dirtyStorageIds.Add(id);
            SaveStorageIfDirty(id);
            BroadcastAllStorages(PlayerRef.None);
            return true;
        }

        private bool ApplyPutItem(string storageId, int targetSlotIndex, string itemId, int count, int rotation)
        {
            ItemData itemDefinition = ItemDataRegistry.Find(itemId);
            if (itemDefinition == null)
            {
                return false;
            }

            string id = NormalizeStorageId(storageId);
            GridInventoryModel model = GetOrCreateModel(id);
            ItemInstance item = new ItemInstance(itemDefinition, Mathf.Max(1, count));
            item.currentRotation = (ItemRotation)Mathf.Clamp(rotation, 0, 3);
            (int x, int y) = model.GetCoordinates(targetSlotIndex);

            if (!PlaceOrStackInModel(model, item, x, y))
            {
                return false;
            }

            dirtyStorageIds.Add(id);
            SaveStorageIfDirty(id);
            BroadcastAllStorages(PlayerRef.None);
            return true;
        }

        private bool ApplyRemoveItem(string storageId, int sourceSlotIndex, int requestedRotation, out string itemId, out int count, out int rotation)
        {
            itemId = string.Empty;
            count = 0;
            rotation = 0;

            string id = NormalizeStorageId(storageId);
            GridInventoryModel model = GetOrCreateModel(id);
            ItemInstance item = model.Get(sourceSlotIndex);
            if (item == null)
            {
                return false;
            }

            if (requestedRotation >= 0)
            {
                item.currentRotation = (ItemRotation)Mathf.Clamp(requestedRotation, 0, 3);
            }

            itemId = item.Data != null ? item.Data.itemID : string.Empty;
            count = item.currentStackCount;
            rotation = (int)item.currentRotation;

            if (!model.TryRemove(item))
            {
                return false;
            }

            dirtyStorageIds.Add(id);
            SaveStorageIfDirty(id);
            BroadcastAllStorages(PlayerRef.None);
            return true;
        }

        private void SaveStorageIfDirty(string storageId)
        {
            if (!HasStateAuthority || string.IsNullOrWhiteSpace(storageId))
            {
                return;
            }

            string id = NormalizeStorageId(storageId);
            if (!dirtyStorageIds.Contains(id) || !modelsById.TryGetValue(id, out GridInventoryModel model))
            {
                return;
            }

            StorageSaveManager.SaveStorage(StorageGridSerializer.ToSaveData(id, model));
            dirtyStorageIds.Remove(id);
        }

        private int NextOperationId()
        {
            return ++localOperationSequence;
        }

        private static bool PlaceOrStackInModel(GridInventoryModel targetModel, ItemInstance sourceItem, int x, int y)
        {
            ItemInstance targetItem = targetModel.Get(x, y);
            if (targetItem != null && targetItem != sourceItem && targetItem.Data == sourceItem.Data && targetItem.Data.maxStackSize > 1)
            {
                int total = targetItem.currentStackCount + sourceItem.currentStackCount;
                if (total <= targetItem.Data.maxStackSize)
                {
                    targetItem.currentStackCount = total;
                    targetModel.Items.Invoke();
                    return true;
                }

                return false;
            }

            return targetModel.PlaceItem(sourceItem, x, y);
        }

        public void LoadAllStorages()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            EnsureSceneStorageModels();
            List<string> ids = new List<string>(modelsById.Keys);
            foreach (string storageId in ids)
            {
                StorageSaveData data = StorageSaveManager.LoadStorage(storageId, storageWidth, storageHeight);
                StorageGridSerializer.ApplyToModel(data, GetOrCreateModel(storageId));
            }

            dirtyStorageIds.Clear();
        }

        public void SaveAllStorages()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            foreach (string storageId in new List<string>(dirtyStorageIds))
            {
                if (!modelsById.TryGetValue(storageId, out GridInventoryModel model))
                {
                    continue;
                }

                StorageSaveManager.SaveStorage(StorageGridSerializer.ToSaveData(storageId, model));
            }

            dirtyStorageIds.Clear();
        }

        public void ApplyStorageDataToScene()
        {
            EnsureSceneStorageModels();
            BroadcastAllStorages(PlayerRef.None);
        }

        public GridInventoryModel GetOrCreateModel(string storageId)
        {
            string id = NormalizeStorageId(storageId);
            if (!modelsById.TryGetValue(id, out GridInventoryModel model))
            {
                model = new GridInventoryModel(storageWidth, storageHeight);
                modelsById[id] = model;
            }

            return model;
        }

        private void BroadcastAllStorages(PlayerRef targetPlayer)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            StorageSaveCollectionData collection = new StorageSaveCollectionData();
            foreach (KeyValuePair<string, GridInventoryModel> pair in modelsById)
            {
                collection.storages.Add(StorageGridSerializer.ToSaveData(pair.Key, pair.Value));
            }

            SendPayloadChunks(targetPlayer, JsonUtility.ToJson(collection));
        }

        private void SendPayloadChunks(PlayerRef targetPlayer, string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            const int chunkSize = 40;
            int sequence = ++outboundSequence;
            int totalChunks = Mathf.CeilToInt(payload.Length / (float)chunkSize);
            for (int i = 0; i < totalChunks; i++)
            {
                int length = Mathf.Min(chunkSize, payload.Length - i * chunkSize);
                string chunk = payload.Substring(i * chunkSize, length);
                RpcReceiveStorageChunk(targetPlayer, sequence, i, totalChunks, chunk);
            }
        }

        private void SendSnapshotRequestChunks(string payload)
        {
            if (Runner == null || string.IsNullOrEmpty(payload))
            {
                return;
            }

            const int chunkSize = 40;
            int sequence = ++outboundSequence;
            int totalChunks = Mathf.CeilToInt(payload.Length / (float)chunkSize);
            for (int i = 0; i < totalChunks; i++)
            {
                int length = Mathf.Min(chunkSize, payload.Length - i * chunkSize);
                string chunk = payload.Substring(i * chunkSize, length);
                RpcRequestReplaceStorageChunk(Runner.LocalPlayer, sequence, i, totalChunks, chunk);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcReceiveStorageChunk(PlayerRef targetPlayer, int sequence, int chunkIndex, int totalChunks, NetworkString<_64> chunk)
        {
            if (HasStateAuthority)
            {
                return;
            }

            if (targetPlayer != PlayerRef.None && Runner != null && targetPlayer != Runner.LocalPlayer)
            {
                return;
            }

            if (!incomingPayloads.TryGetValue(sequence, out IncomingPayload incoming))
            {
                incoming = new IncomingPayload(totalChunks);
                incomingPayloads[sequence] = incoming;
            }

            incoming.SetChunk(chunkIndex, chunk.ToString());
            if (!incoming.IsComplete)
            {
                return;
            }

            incomingPayloads.Remove(sequence);
            ApplyPayloadJson(incoming.Combine());
        }

        private void ApplyPayloadJson(string json)
        {
            StorageSaveCollectionData collection = JsonUtility.FromJson<StorageSaveCollectionData>(json);
            if (collection?.storages == null)
            {
                return;
            }

            foreach (StorageSaveData data in collection.storages)
            {
                if (data == null || string.IsNullOrWhiteSpace(data.storageId))
                {
                    continue;
                }

                data.storageId = NormalizeStorageId(data.storageId);
                StorageGridSerializer.ApplyToModel(data, GetOrCreateModel(data.storageId));
            }
        }

        private void EnsureSceneStorageModels()
        {
            if (defaultStorageIds != null)
            {
                foreach (string id in defaultStorageIds)
                {
                    GetOrCreateModel(id);
                }
            }

            StorageBox[] boxes = FindObjectsByType<StorageBox>(FindObjectsSortMode.None)
                .OrderBy(GetStableHierarchyPath)
                .ToArray();
            EnsureUniqueStorageBoxIds(boxes);
            foreach (StorageBox box in boxes)
            {
                if (box != null)
                {
                    GetOrCreateModel(box.StorageId);
                }
            }
        }

        private void EnsureUniqueStorageBoxIds(StorageBox[] boxes)
        {
            if (boxes == null || boxes.Length == 0)
            {
                return;
            }

            HashSet<string> usedIds = new HashSet<string>();
            int nextIndex = 0;
            foreach (StorageBox box in boxes)
            {
                if (box == null)
                {
                    continue;
                }

                string id = NormalizeStorageId(box.StorageId);
                if (usedIds.Contains(id))
                {
                    id = GetNextAvailableStorageId(usedIds, ref nextIndex);
                    box.Configure(id, StorageUI.ActiveInstance, this);
                }

                usedIds.Add(id);
            }
        }

        private static string GetNextAvailableStorageId(HashSet<string> usedIds, ref int nextIndex)
        {
            string id;
            do
            {
                id = $"Storage_{nextIndex++}";
            }
            while (usedIds.Contains(id));

            return id;
        }

        private static string GetStableHierarchyPath(StorageBox box)
        {
            if (box == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = box.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string NormalizeStorageId(string storageId)
        {
            return string.IsNullOrWhiteSpace(storageId) ? "Storage_0" : storageId.Trim();
        }

        private static int BuildIncomingRequestKey(PlayerRef playerRef, int sequence)
        {
            return playerRef.RawEncoded * 100000 + sequence;
        }

        private sealed class IncomingPayload
        {
            private readonly string[] chunks;
            private int receivedCount;

            public IncomingPayload(int totalChunks)
            {
                chunks = new string[Mathf.Max(1, totalChunks)];
            }

            public bool IsComplete => receivedCount >= chunks.Length;

            public void SetChunk(int index, string value)
            {
                if (index < 0 || index >= chunks.Length || chunks[index] != null)
                {
                    return;
                }

                chunks[index] = value ?? string.Empty;
                receivedCount++;
            }

            public string Combine()
            {
                return string.Concat(chunks);
            }
        }
    }
}
