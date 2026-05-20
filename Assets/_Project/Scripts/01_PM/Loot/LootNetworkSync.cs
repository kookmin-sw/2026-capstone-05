using System.Collections.Generic;
using Fusion;
using Systems.GridInventory;
using UnityEngine;

namespace Systems.Loot
{
    [RequireComponent(typeof(NetworkObject))]
    public class LootNetworkSync : NetworkBehaviour, IPlayerLeft
    {
        [Networked] [Capacity(64)] public NetworkDictionary<NetworkString<_64>, PlayerRef> ActiveLootUsers => default;

        private static LootNetworkSync _instance;
        public static LootNetworkSync Instance 
        { 
            get 
            {
                if (_instance == null && AuthSession.IsOffline)
                {
                    _instance = FindFirstObjectByType<LootNetworkSync>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("LootNetworkSync_Offline");
                        _instance = go.AddComponent<LootNetworkSync>();
                    }
                    _instance.InitializeOffline();
                }
                else if (_instance == null)
                {
                    // 씬에 LootNetworkSync만 있고 Spawned 이전에 접근하는 경우 (싱글 등)
                    _instance = FindFirstObjectByType<LootNetworkSync>();
                }
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        private readonly Dictionary<string, GridInventoryModel> modelsById = new Dictionary<string, GridInventoryModel>();
        private readonly HashSet<string> dirtyLootIds = new HashSet<string>();
        private readonly HashSet<string> baseStorageIds = new HashSet<string>();
        private readonly Dictionary<int, IncomingPayload> incomingPayloads = new Dictionary<int, IncomingPayload>();
        private int outboundSequence;

        public override void Spawned()
        {
            Instance = this;
            if (AuthSession.IsOffline)
                return; // 오프라인: Start()에서 InitializeOffline로 모델 로드

            EnsureSceneLootModels();

            if (HasStateAuthority)
            {
                LoadAllLoots();
                BroadcastAllLoots(PlayerRef.None);
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
            if (AuthSession.IsOffline && !isOfflineInitialized)
            {
                InitializeOffline();
            }
        }

        private void InitializeOffline()
        {
            isOfflineInitialized = true;
            _instance = this;
            EnsureSceneLootModels();
            LoadAllLoots();
        }

        public void SubmitLootSnapshot(string lootId, LootSaveData snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.lootId = NormalizeLootId(lootId);
            string json = JsonUtility.ToJson(snapshot);

            if (HasStateAuthority || AuthSession.IsOffline)
            {
                ApplyLootSnapshot(json);
                return;
            }

            SendSnapshotRequestChunks(json);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestReplaceLootChunk(PlayerRef requestedBy, int sequence, int chunkIndex, int totalChunks, NetworkString<_64> chunk)
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
            ApplyLootSnapshot(incoming.Combine());
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestFullSync(PlayerRef requestedBy)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            BroadcastAllLoots(requestedBy);
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority) return;

            List<NetworkString<_64>> keysToRemove = new List<NetworkString<_64>>();
            foreach (var kvp in ActiveLootUsers)
            {
                if (kvp.Value == player)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                ActiveLootUsers.Remove(key);
                BroadcastLoot(key.ToString(), PlayerRef.None);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestOpenLoot(PlayerRef requestedBy, NetworkString<_64> lootId)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;

            if (ActiveLootUsers.TryGet(lootId, out PlayerRef currentUser))
            {
                if (currentUser != requestedBy)
                {
                    Rpc_DenyOpenLoot(requestedBy, lootId);
                    return;
                }
            }

            ActiveLootUsers.Set(lootId, requestedBy);
            Rpc_ApproveOpenLoot(requestedBy, lootId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_ApproveOpenLoot(PlayerRef targetPlayer, NetworkString<_64> lootId)
        {
            if (Runner != null && targetPlayer != Runner.LocalPlayer) return;

            if (LootController.Instance != null)
            {
                LootController.Instance.HandleOpenApproved(lootId.ToString());
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_DenyOpenLoot(PlayerRef targetPlayer, NetworkString<_64> lootId)
        {
            if (Runner != null && targetPlayer != Runner.LocalPlayer) return;

            if (LootController.Instance != null)
            {
                LootController.Instance.HandleOpenDenied(lootId.ToString());
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_NotifyCloseLoot(PlayerRef requestedBy, NetworkString<_64> lootId)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;

            if (ActiveLootUsers.TryGet(lootId, out PlayerRef currentUser) && currentUser == requestedBy)
            {
                ActiveLootUsers.Remove(lootId);
                BroadcastLoot(lootId.ToString(), PlayerRef.None);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestMoveItem(PlayerRef requestedBy, NetworkString<_64> lootId, int sourceSlotIndex, int targetSlotIndex, int newRotation)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;
            
            GridInventoryModel model = GetOrCreateModel(lootId.ToString());
            (int sx, int sy) = model.GetCoordinates(sourceSlotIndex);
            ItemInstance sourceItem = model.Get(sx, sy);
            
            if (sourceItem == null) 
            {
                return;
            }
            
            var anchor = model.GetItemAnchorPosition(sourceItem);
            if (anchor.x != sx || anchor.y != sy) 
            {
                return;
            }

            (int tx, int ty) = model.GetCoordinates(targetSlotIndex);
            
            model.TryRemove(sourceItem);
            ItemRotation oldRot = sourceItem.currentRotation;
            sourceItem.currentRotation = (ItemRotation)newRotation;

            // Find overlapping items
            HashSet<ItemInstance> overlappingItems = new HashSet<ItemInstance>();
            var positions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);
            bool outOfBounds = false;
            
            foreach (var pos in positions) {
                int checkX = tx + pos.x;
                int checkY = ty + pos.y;
                if (checkX < 0 || checkY < 0 || checkX >= model.Width || checkY >= model.Height) {
                    outOfBounds = true;
                    break;
                }
                var foundItem = model.Get(checkX, checkY);
                if (foundItem != null) {
                    overlappingItems.Add(foundItem);
                }
            }

            if (outOfBounds || overlappingItems.Count > 1) {
                // Fail
                sourceItem.currentRotation = oldRot;
                model.PlaceItem(sourceItem, sx, sy);
                return;
            }

            if (overlappingItems.Count == 0) {
                // Free space
                model.PlaceItem(sourceItem, tx, ty);
                dirtyLootIds.Add(lootId.ToString());
                return;
            }

            // 1 item overlapping
            ItemInstance targetItem = null;
            foreach (var item in overlappingItems) { targetItem = item; break; }

            // Stack Combine
            if (sourceItem.Data == targetItem.Data && targetItem.Data.maxStackSize > 1) {
                int total = sourceItem.currentStackCount + targetItem.currentStackCount;
                if (total <= targetItem.Data.maxStackSize) {
                    targetItem.currentStackCount = total;
                    model.Items.Invoke();
                } else {
                    sourceItem.currentRotation = oldRot;
                    targetItem.currentStackCount = targetItem.Data.maxStackSize;
                    sourceItem.currentStackCount = total - targetItem.Data.maxStackSize;
                    model.PlaceItem(sourceItem, sx, sy);
                }
                dirtyLootIds.Add(lootId.ToString());
                return;
            }

            // 1:1 Swap
            var bOld = model.GetItemAnchorPosition(targetItem);
            var delta = new Vector2Int(tx, ty) - new Vector2Int(sx, sy);
            var bNew = new Vector2Int(bOld.x - delta.x, bOld.y - delta.y);

            bool canPlaceA = model.CanPlaceItem(sourceItem, tx, ty, targetItem);
            bool canPlaceB = model.CanPlaceItem(targetItem, bNew.x, bNew.y, targetItem);

            bool overlapEachOther = false;
            var aPositions = sourceItem.Data.gridShape.GetRotatedPositions(sourceItem.currentRotation);
            var bPositionsTarget = targetItem.Data.gridShape.GetRotatedPositions(targetItem.currentRotation);
            foreach (var a in aPositions) {
                var absA = new Vector2Int(tx + a.x, ty + a.y);
                foreach (var b in bPositionsTarget) {
                    var absB = new Vector2Int(bNew.x + b.x, bNew.y + b.y);
                    if (absA == absB) {
                        overlapEachOther = true;
                        break;
                    }
                }
                if (overlapEachOther) break;
            }

            if (!overlapEachOther && canPlaceA && canPlaceB) {
                model.TryRemove(targetItem);
                model.PlaceItem(sourceItem, tx, ty);
                model.PlaceItem(targetItem, bNew.x, bNew.y);
                dirtyLootIds.Add(lootId.ToString());
            } else {
                sourceItem.currentRotation = oldRot;
                model.PlaceItem(sourceItem, sx, sy);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestTakeItemWithSwap(PlayerRef requestedBy, NetworkString<_64> lootId, NetworkString<_64> swapItemId, int sourceLootSlotIndex, int targetInventorySlotIndex, int takeQuantity, int swapQuantity)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;
            
            GridInventoryModel model = GetOrCreateModel(lootId.ToString());
            (int sx, int sy) = model.GetCoordinates(sourceLootSlotIndex);
            ItemInstance item = model.Get(sx, sy);
            
            if (item == null) return;
            
            var anchor = model.GetItemAnchorPosition(item);
            if (anchor.x != sx || anchor.y != sy) return;

            // 1. 상자에서 아이템 제거 (Take)
            int actualTakeQty = Mathf.Min(takeQuantity, item.currentStackCount);
            if (actualTakeQty == item.currentStackCount)
            {
                model.TryRemove(item);
            }
            else
            {
                item.currentStackCount -= actualTakeQty;
                model.Items.Invoke();
            }

            // 2. 상자에 스왑된 아이템 배치 (Put)
            ItemData swapItemDef = ItemDataRegistry.Find(swapItemId.ToString());
            if (swapItemDef != null)
            {
                ItemInstance swapItem = new ItemInstance(swapItemDef, swapQuantity);
                model.PlaceItem(swapItem, sx, sy);
            }

            dirtyLootIds.Add(lootId.ToString());
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestPutItemWithSwap(PlayerRef requestedBy, NetworkString<_64> lootId, NetworkString<_64> putItemId, int sourceInventorySlotIndex, int targetLootSlotIndex, int putQuantity, int putRotation, NetworkString<_64> swapItemId, int swapQuantity)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;
            
            ItemData putItemDef = ItemDataRegistry.Find(putItemId.ToString());
            if (putItemDef == null) return;

            GridInventoryModel model = GetOrCreateModel(lootId.ToString());
            (int tx, int ty) = model.GetCoordinates(targetLootSlotIndex);

            var existingItem = model.Get(tx, ty);
            if (existingItem != null)
            {
                // 1. 상자에서 기존 아이템 제거 (스왑되어 플레이어에게 감)
                model.TryRemove(existingItem);
            }

            // 2. 상자에 새 아이템 배치 (Put)
            ItemInstance newItem = new ItemInstance(putItemDef, putQuantity);
            newItem.currentRotation = (ItemRotation)putRotation;
            model.PlaceItem(newItem, tx, ty);
            
            dirtyLootIds.Add(lootId.ToString());
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestTakeItem(PlayerRef requestedBy, NetworkString<_64> lootId, int sourceSlotIndex, int targetInventorySlotIndex, int quantity)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;
            
            GridInventoryModel model = GetOrCreateModel(lootId.ToString());
            (int sx, int sy) = model.GetCoordinates(sourceSlotIndex);
            ItemInstance item = model.Get(sx, sy);
            
            if (item == null) 
            {
                return; // 비정상적인 요청
            }
            
            var anchor = model.GetItemAnchorPosition(item);
            if (anchor.x != sx || anchor.y != sy) 
            {
                return; // 비정상적인 요청
            }

            int takeQty = Mathf.Min(quantity, item.currentStackCount);
            if (takeQty <= 0) return;

            if (takeQty == item.currentStackCount)
            {
                model.TryRemove(item);
            }
            else
            {
                item.currentStackCount -= takeQty;
                model.Items.Invoke();
            }

            dirtyLootIds.Add(lootId.ToString());
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestPutItem(PlayerRef requestedBy, NetworkString<_64> lootId, NetworkString<_64> itemId, int sourceInventorySlotIndex, int targetLootSlotIndex, int quantity, int rotation)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;
            
            ItemData itemDef = ItemDataRegistry.Find(itemId.ToString());
            if (itemDef == null) return;

            GridInventoryModel model = GetOrCreateModel(lootId.ToString());
            (int tx, int ty) = model.GetCoordinates(targetLootSlotIndex);

            ItemInstance newItem = new ItemInstance(itemDef, quantity);
            newItem.currentRotation = (ItemRotation)rotation;

            // 클라이언트가 이미 검증을 마치고 보낸 통보이므로, 서버는 해당 위치에 그대로 적용합니다.
            // 만약 이미 아이템이 있다면 스택 병합이거나 1:1 스왑일 수 있습니다.
            var existingItem = model.Get(tx, ty);
            
            if (existingItem != null && existingItem.Data == itemDef && itemDef.maxStackSize > 1)
            {
                // 스택 병합 통보 수락
                int total = newItem.currentStackCount + existingItem.currentStackCount;
                if (total <= itemDef.maxStackSize)
                {
                    existingItem.currentStackCount = total;
                }
                else
                {
                    existingItem.currentStackCount = itemDef.maxStackSize;
                }
                model.Items.Invoke();
                dirtyLootIds.Add(lootId.ToString());
            }
            else if (existingItem == null)
            {
                // 빈 자리 배치 통보 수락
                model.PlaceItem(newItem, tx, ty);
                dirtyLootIds.Add(lootId.ToString());
            }
            else
            {
                // 1:1 스왑 통보 수락
                model.TryRemove(existingItem);
                model.PlaceItem(newItem, tx, ty);
                dirtyLootIds.Add(lootId.ToString());
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestTakeAll(PlayerRef requestedBy, NetworkString<_64> lootId)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;
            
            GridInventoryModel model = GetOrCreateModel(lootId.ToString());
            List<ItemInstance> itemsToMove = new List<ItemInstance>();
            HashSet<ItemInstance> processed = new HashSet<ItemInstance>();
            
            for (int i = 0; i < model.Items.Length; i++)
            {
                var item = model.Items[i];
                if (item != null && !processed.Contains(item))
                {
                    processed.Add(item);
                    itemsToMove.Add(item);
                }
            }

            foreach (var item in itemsToMove)
            {
                model.TryRemove(item);
            }

            if (itemsToMove.Count > 0)
            {
                dirtyLootIds.Add(lootId.ToString());
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestQuickMove(PlayerRef requestedBy, NetworkString<_64> lootId, NetworkString<_64> itemId, int sourceSlotIndex, int quantity, bool isTake)
        {
            if (!HasStateAuthority && !AuthSession.IsOffline) return;
            
            if (isTake)
            {
                GridInventoryModel model = GetOrCreateModel(lootId.ToString());
                (int sx, int sy) = model.GetCoordinates(sourceSlotIndex);
                ItemInstance item = model.Get(sx, sy);
                
                if (item == null) 
                {
                    return;
                }
                
                var anchor = model.GetItemAnchorPosition(item);
                if (anchor.x != sx || anchor.y != sy) 
                {
                    return;
                }

                int takeQty = Mathf.Min(quantity, item.currentStackCount);
                if (takeQty <= 0) return;

                if (takeQty == item.currentStackCount)
                {
                    model.TryRemove(item);
                }
                else
                {
                    item.currentStackCount -= takeQty;
                    model.Items.Invoke();
                }
                
                dirtyLootIds.Add(lootId.ToString());
            }
            else
            {
                ItemData itemDef = ItemDataRegistry.Find(itemId.ToString());
                if (itemDef == null) return;

                GridInventoryModel model = GetOrCreateModel(lootId.ToString());
                ItemInstance newItem = new ItemInstance(itemDef, quantity);
                
                if (model.TryAdd(newItem))
                {
                    dirtyLootIds.Add(lootId.ToString());
                }
            }
        }

        private void ApplyLootSnapshot(string json)
        {
            if ((!HasStateAuthority && !AuthSession.IsOffline) || string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            LootSaveData data = JsonUtility.FromJson<LootSaveData>(json);
            if (data == null || string.IsNullOrWhiteSpace(data.lootId))
            {
                return;
            }

            data.lootId = NormalizeLootId(data.lootId);
            GridInventoryModel model = GetOrCreateModel(data.lootId, data.width, data.height);
            LootGridSerializer.ApplyToModel(data, model);
            dirtyLootIds.Add(data.lootId);
            BroadcastLoot(data.lootId, PlayerRef.None);
        }

        private void BroadcastLoot(string lootId, PlayerRef targetPlayer)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (!modelsById.TryGetValue(lootId, out GridInventoryModel model))
            {
                return;
            }

            LootSaveCollectionData collection = new LootSaveCollectionData();
            collection.loots.Add(LootGridSerializer.ToSaveData(lootId, model));

            SendPayloadChunks(targetPlayer, JsonUtility.ToJson(collection));
        }

        public void LoadAllLoots()
        {
            if (!HasStateAuthority && !AuthSession.IsOffline)
            {
                return;
            }

            EnsureSceneLootModels();
            List<string> ids = new List<string>(modelsById.Keys);
            foreach (string lootId in ids)
            {
                if (baseStorageIds.Contains(lootId) && modelsById.TryGetValue(lootId, out GridInventoryModel model))
                {
                    LootSaveData data = LootSaveManager.LoadLoot(lootId, model.Width, model.Height);
                    LootGridSerializer.ApplyToModel(data, model);
                }
            }

            dirtyLootIds.Clear();
        }

        public void SaveAllLoots()
        {
            if (!HasStateAuthority && !AuthSession.IsOffline)
            {
                return;
            }

            foreach (string lootId in new List<string>(dirtyLootIds))
            {
                if (!baseStorageIds.Contains(lootId))
                {
                    continue;
                }

                if (!modelsById.TryGetValue(lootId, out GridInventoryModel model))
                {
                    continue;
                }

                LootSaveManager.SaveLoot(LootGridSerializer.ToSaveData(lootId, model));
            }

            dirtyLootIds.Clear();
        }

        public void ApplyLootDataToScene()
        {
            EnsureSceneLootModels();
            BroadcastAllLoots(PlayerRef.None);
        }

        public GridInventoryModel GetOrCreateModel(string lootId, int width = 9, int height = 18)
        {
            string id = NormalizeLootId(lootId);
            if (!modelsById.TryGetValue(id, out GridInventoryModel model))
            {
                model = new GridInventoryModel(width, height);
                modelsById[id] = model;
            }

            return model;
        }

        private void BroadcastAllLoots(PlayerRef targetPlayer)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            LootSaveCollectionData collection = new LootSaveCollectionData();
            foreach (KeyValuePair<string, GridInventoryModel> pair in modelsById)
            {
                collection.loots.Add(LootGridSerializer.ToSaveData(pair.Key, pair.Value));
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
                RpcReceiveLootChunk(targetPlayer, sequence, i, totalChunks, chunk);
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
                RpcRequestReplaceLootChunk(Runner.LocalPlayer, sequence, i, totalChunks, chunk);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcReceiveLootChunk(PlayerRef targetPlayer, int sequence, int chunkIndex, int totalChunks, NetworkString<_64> chunk)
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
            LootSaveCollectionData collection = JsonUtility.FromJson<LootSaveCollectionData>(json);
            if (collection?.loots == null)
            {
                return;
            }

            foreach (LootSaveData data in collection.loots)
            {
                if (data == null || string.IsNullOrWhiteSpace(data.lootId))
                {
                    continue;
                }

                data.lootId = NormalizeLootId(data.lootId);
                LootGridSerializer.ApplyToModel(data, GetOrCreateModel(data.lootId, data.width, data.height));
            }
        }

        private void EnsureSceneLootModels()
        {
            InteractableLoot[] loots = FindObjectsByType<InteractableLoot>(FindObjectsSortMode.None);
            foreach (InteractableLoot loot in loots)
            {
                if (loot != null && loot.HasConfiguration)
                {
                    GetOrCreateModel(loot.StorageId, loot.Width, loot.Height);
                    if (loot.IsBaseStorage)
                    {
                        baseStorageIds.Add(loot.StorageId);
                    }
                }
            }
        }

        private static string NormalizeLootId(string lootId)
        {
            return string.IsNullOrWhiteSpace(lootId) ? "Loot_0" : lootId.Trim();
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
