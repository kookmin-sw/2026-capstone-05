using System.Collections.Generic;
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

        private static StorageNetworkSync _instance;
        public static StorageNetworkSync Instance 
        { 
            get 
            {
                if (_instance == null && PlayerNetworkSetup.IsOfflineTestMode)
                {
                    _instance = FindFirstObjectByType<StorageNetworkSync>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("StorageNetworkSync_Offline");
                        _instance = go.AddComponent<StorageNetworkSync>();
                    }
                    _instance.InitializeOffline();
                }
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        private readonly Dictionary<string, GridInventoryModel> modelsById = new Dictionary<string, GridInventoryModel>();
        private readonly HashSet<string> dirtyStorageIds = new HashSet<string>();
        private readonly Dictionary<int, IncomingPayload> incomingPayloads = new Dictionary<int, IncomingPayload>();
        private int outboundSequence;

        public int StorageWidth => storageWidth;
        public int StorageHeight => storageHeight;

        public override void Spawned()
        {
            if (PlayerNetworkSetup.IsOfflineTestMode) return; // 오프라인 모드에서는 Start()에서 처리됨
            
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

        private bool isOfflineInitialized = false;

        private void Start()
        {
            if (PlayerNetworkSetup.IsOfflineTestMode && !isOfflineInitialized)
            {
                InitializeOffline();
            }
        }

        private void InitializeOffline()
        {
            isOfflineInitialized = true;
            _instance = this;
            StorageRackRuntimeBinder.BindDefaultRacks(this);
            EnsureSceneStorageModels();
            LoadAllStorages();
        }

        public void SubmitStorageSnapshot(string storageId, StorageSaveData snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.storageId = NormalizeStorageId(storageId);
            string json = JsonUtility.ToJson(snapshot);

            if (HasStateAuthority || PlayerNetworkSetup.IsOfflineTestMode)
            {
                ApplyStorageSnapshot(json);
                return;
            }

            SendSnapshotRequestChunks(json);
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
            if ((!HasStateAuthority && !PlayerNetworkSetup.IsOfflineTestMode) || string.IsNullOrWhiteSpace(json))
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
            BroadcastAllStorages(PlayerRef.None);
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

            StorageBox[] boxes = FindObjectsByType<StorageBox>(FindObjectsSortMode.None);
            foreach (StorageBox box in boxes)
            {
                if (box != null)
                {
                    GetOrCreateModel(box.StorageId);
                }
            }
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
