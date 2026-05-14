using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BackendPlayerNetworkSync : NetworkBehaviour
{
    private const float SpawnLockDurationSeconds = 0.35f;

    private PlayerController _playerController;
    private PlayerInputHandler _inputHandler;
    private PlayerAnimator _playerAnimator;
    private PlayerEquipment _playerEquipment;
    private Transform _cameraTransform;
    private Vector3 _authoritativeSpawnPosition;
    private Quaternion _authoritativeSpawnRotation;
    private float _spawnLockRemainingSeconds;
    private bool _spawnGravityWasEnabled;
    private bool _spawnLockInitialized;
    private string _lastAppliedEquippedItemId = string.Empty;
    private int _lastAppliedUseAnimationCount;

    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkRotation { get; set; }
    [Networked] private float CameraPitch { get; set; }
    [Networked] private Vector2 NetworkMoveInput { get; set; }
    [Networked] private NetworkBool NetworkIsGrounded { get; set; }
    [Networked] private NetworkBool NetworkIsSprinting { get; set; }
    [Networked] private NetworkBool NetworkIsCrouching { get; set; }
    [Networked, Capacity(64)] private NetworkString<_64> NetworkEquippedItemId { get; set; }
    [Networked] private int NetworkEquippedStackCount { get; set; }
    [Networked] private int NetworkUseAnimationType { get; set; }
    [Networked] private int NetworkUseAnimationCount { get; set; }

    public static BackendPlayerNetworkSync LocalInstance { get; private set; }

    private static readonly System.Collections.Generic.HashSet<int> ClaimedPickupKeys = new();
    private static readonly System.Collections.Generic.HashSet<int> ClaimedDoorKeys = new();

    public bool IsNetworkReady => Runner != null && Object != null;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            LocalInstance = this;
        }

        _playerController = GetComponent<PlayerController>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _playerAnimator = GetComponent<PlayerAnimator>();
        _playerEquipment = GetComponent<PlayerEquipment>();
        _cameraTransform = _playerController != null ? _playerController.CameraTransform : null;

        if (_playerEquipment != null)
        {
            _playerEquipment.OnEquippedItemChanged += HandleLocalEquippedItemChanged;
            _playerEquipment.OnUseAnimationRequested += HandleLocalUseAnimationRequested;
        }

        // 입력 권한이 있는 클라이언트는 로컬에서 즉시 시뮬레이션해 체감 지연을 줄입니다.
        if (_playerController != null && !HasStateAuthority && !Object.HasInputAuthority)
        {
            _playerController.enabled = false;
        }

        SyncInputOverrideState();

        if (HasStateAuthority)
        {
            BeginServerSpawnLock();
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            CameraPitch = ReadPitch();
            NetworkMoveInput = Vector2.zero;
            NetworkIsGrounded = true;
            NetworkIsSprinting = false;
            NetworkIsCrouching = false;
            ApplyEquippedItemNetworkState(_playerEquipment != null ? _playerEquipment.CurrentItemInstance : null);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }

        if (_playerEquipment != null)
        {
            _playerEquipment.OnEquippedItemChanged -= HandleLocalEquippedItemChanged;
            _playerEquipment.OnUseAnimationRequested -= HandleLocalUseAnimationRequested;
        }
    }

    public override void FixedUpdateNetwork()
    {
        SyncInputOverrideState();

        if (HasStateAuthority)
        {
            UpdateServerSpawnLock();

            if (_inputHandler != null &&
                !Object.HasInputAuthority &&
                GetInput(out BackendPlayerNetworkInput input))
            {
                _inputHandler.ApplyNetworkSnapshot(new PlayerInputSnapshot
                {
                    Move = input.Move,
                    Look = input.Look,
                    Sprint = input.Buttons.IsSet(BackendPlayerNetworkInput.SprintButton),
                    Jump = input.Buttons.IsSet(BackendPlayerNetworkInput.JumpButton),
                    Crouch = input.Buttons.IsSet(BackendPlayerNetworkInput.CrouchButton),
                    Interact = input.Buttons.IsSet(BackendPlayerNetworkInput.InteractButton),
                    Action = input.Buttons.IsSet(BackendPlayerNetworkInput.ActionButton)
                });
            }

            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
            CameraPitch = ReadPitch();
            NetworkMoveInput = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;
            NetworkIsGrounded = _playerController != null && _playerController.IsGrounded;
            NetworkIsSprinting = _inputHandler != null && _inputHandler.IsSprinting;
            NetworkIsCrouching = _playerController != null &&
                                _playerController.GroundedState != null &&
                                _playerController.GroundedState.CurrentPosture == PlayerGroundedPosture.Crouching;

            if (!Object.HasInputAuthority)
            {
                ApplyProxyEquipmentState();
                ApplyProxyUseAnimationState();
            }
        }
    }

    private void BeginServerSpawnLock()
    {
        _authoritativeSpawnPosition = transform.position;
        _authoritativeSpawnRotation = transform.rotation;
        _spawnLockRemainingSeconds = SpawnLockDurationSeconds;
        _spawnLockInitialized = true;

        if (_playerController != null)
        {
            _spawnGravityWasEnabled = _playerController.useGravity;
            _playerController.useGravity = false;
            _playerController.currentVelocity = Vector3.zero;
        }
    }

    private void UpdateServerSpawnLock()
    {
        if (!_spawnLockInitialized)
            return;

        if (_spawnLockRemainingSeconds > 0f)
        {
            _spawnLockRemainingSeconds -= Runner != null ? Runner.DeltaTime : Time.deltaTime;

            transform.SetPositionAndRotation(_authoritativeSpawnPosition, _authoritativeSpawnRotation);

            if (_playerController != null)
            {
                _playerController.currentVelocity = Vector3.zero;
            }

            return;
        }

        _spawnLockInitialized = false;

        if (_playerController != null)
        {
            _playerController.useGravity = _spawnGravityWasEnabled;
        }
    }

    public override void Render()
    {
        if (HasStateAuthority)
            return;

        // 서버가 받은 값을 그대로 전파해 적용합니다.
        transform.SetPositionAndRotation(NetworkPosition, NetworkRotation);

        if (_cameraTransform != null)
        {
            _cameraTransform.localRotation = Quaternion.Euler(CameraPitch, 0f, 0f);
        }

        ApplyProxyAnimationState();
        ApplyProxyEquipmentState();
        ApplyProxyUseAnimationState();
    }

    private void ApplyProxyAnimationState()
    {
        if (_playerAnimator == null)
            return;

        _playerAnimator.SetGrounded(NetworkIsGrounded);
        _playerAnimator.SetSprinting(NetworkIsSprinting);
        _playerAnimator.SetCrouching(NetworkIsCrouching);
        _playerAnimator.UpdateMovement(NetworkMoveInput);
    }

    private float ReadPitch()
    {
        if (_cameraTransform == null)
            return 0f;

        float rawX = _cameraTransform.localEulerAngles.x;
        if (rawX > 180f)
            rawX -= 360f;

        return rawX;
    }

    private void SyncInputOverrideState()
    {
        if (_inputHandler == null || Object == null)
            return;

        _inputHandler.SetNetworkInputOverride(!Object.HasInputAuthority);
    }

    public void RequestPickup(ItemPickup pickup)
    {
        if (pickup == null || pickup.IsPickedUp || pickup.itemInstance?.Data == null)
            return;

        PlayerRef requester = Object != null ? Object.InputAuthority : PlayerRef.None;
        if (requester == PlayerRef.None && Runner != null)
            requester = Runner.LocalPlayer;

        if (HasStateAuthority)
        {
            TryApprovePickup(requester, pickup.PickupKey);
            return;
        }

        RpcRequestPickup(requester, pickup.PickupKey);
    }

    public void RequestDoorToggle(DoorInteractable door, bool shouldOpen, int openDirection)
    {
        if (door == null)
            return;

        PlayerRef requester = Object != null ? Object.InputAuthority : PlayerRef.None;
        if (requester == PlayerRef.None && Runner != null)
            requester = Runner.LocalPlayer;

        int normalizedDirection = openDirection >= 0 ? 1 : -1;

        if (HasStateAuthority)
        {
            ApproveDoorToggle(requester, door.DoorKey, shouldOpen, normalizedDirection);
            return;
        }

        RpcRequestDoorToggle(requester, door.DoorKey, shouldOpen, normalizedDirection);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcRequestPickup(PlayerRef requestedBy, int pickupKey)
    {
        if (Object != null && requestedBy != Object.InputAuthority)
            requestedBy = Object.InputAuthority;

        TryApprovePickup(requestedBy, pickupKey);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcRequestDoorToggle(PlayerRef requestedBy, int doorKey, NetworkBool shouldOpen, int openDirection)
    {
        if (Object != null && requestedBy != Object.InputAuthority)
            requestedBy = Object.InputAuthority;

        ApproveDoorToggle(requestedBy, doorKey, shouldOpen, openDirection);
    }

    private void TryApprovePickup(PlayerRef requestedBy, int pickupKey)
    {
        if (!HasStateAuthority || requestedBy == PlayerRef.None)
            return;

        if (ClaimedPickupKeys.Contains(pickupKey))
            return;

        if (!ItemPickup.TryGetPickup(pickupKey, out ItemPickup pickup) ||
            pickup.IsPickedUp ||
            pickup.itemInstance?.Data == null)
        {
            return;
        }

        ClaimedPickupKeys.Add(pickupKey);
        string itemId = pickup.itemInstance.Data.itemID;
        int stackCount = pickup.itemInstance.currentStackCount;
        pickup.MarkPickedUpFromNetwork();
        RpcConfirmPickup(pickupKey, requestedBy, itemId, stackCount);
    }

    private void ApproveDoorToggle(PlayerRef requestedBy, int doorKey, bool shouldOpen, int openDirection)
    {
        if (!HasStateAuthority || requestedBy == PlayerRef.None)
            return;

        if (!DoorInteractable.TryGetDoor(doorKey, out DoorInteractable door))
            return;

        int normalizedDirection = openDirection >= 0 ? 1 : -1;

        if (ClaimedDoorKeys.Add(doorKey))
        {
            // Keep track of replicated door keys seen by the authority side.
        }

        door.ApplyState(shouldOpen, normalizedDirection);
        RpcConfirmDoorToggle(doorKey, shouldOpen, normalizedDirection);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcConfirmPickup(int pickupKey, PlayerRef approvedPlayer, NetworkString<_64> itemId, int stackCount)
    {
        PlayerController localPlayer = FindLocalPlayerController();
        bool shouldGrantToLocalPlayer = Runner != null && Runner.LocalPlayer == approvedPlayer;

        if (shouldGrantToLocalPlayer && ItemPickup.TryGetPickup(pickupKey, out ItemPickup pickup))
        {
            ItemData itemData = pickup.itemInstance?.Data != null
                ? pickup.itemInstance.Data
                : ItemDataRegistry.Find(itemId.ToString());
            ItemDataRegistry.Register(itemData);
        }

        ItemPickup.ApplyNetworkPickup(
            pickupKey,
            shouldGrantToLocalPlayer,
            localPlayer,
            itemId.ToString(),
            stackCount);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcConfirmDoorToggle(int doorKey, NetworkBool shouldOpen, int openDirection)
    {
        ClaimedDoorKeys.Add(doorKey);
        DoorInteractable.ApplyNetworkState(doorKey, shouldOpen, openDirection);
    }

    private void HandleLocalEquippedItemChanged(ItemInstance itemInstance)
    {
        if (Object == null || !Object.HasInputAuthority)
            return;

        string itemId = itemInstance?.Data != null ? itemInstance.Data.itemID : string.Empty;
        int stackCount = itemInstance != null ? itemInstance.currentStackCount : 0;

        if (HasStateAuthority)
        {
            SetNetworkEquippedItem(itemId, stackCount);
        }
        else
        {
            RpcRequestSetEquippedItem(itemId, stackCount);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcRequestSetEquippedItem(NetworkString<_64> itemId, int stackCount)
    {
        SetNetworkEquippedItem(itemId.ToString(), stackCount);
    }

    private void SetNetworkEquippedItem(string itemId, int stackCount)
    {
        NetworkEquippedItemId = itemId ?? string.Empty;
        NetworkEquippedStackCount = stackCount;

        if (!Object.HasInputAuthority)
            ApplyProxyEquipmentState();
    }

    private void ApplyEquippedItemNetworkState(ItemInstance itemInstance)
    {
        string itemId = itemInstance?.Data != null ? itemInstance.Data.itemID : string.Empty;
        int stackCount = itemInstance != null ? itemInstance.currentStackCount : 0;
        SetNetworkEquippedItem(itemId, stackCount);
    }

    private void ApplyProxyEquipmentState()
    {
        if (_playerEquipment == null)
            return;

        string itemId = NetworkEquippedItemId.ToString();
        if (_lastAppliedEquippedItemId == itemId)
            return;

        _lastAppliedEquippedItemId = itemId;

        if (string.IsNullOrEmpty(itemId))
        {
            _playerEquipment.UnequipItem(false);
            return;
        }

        ItemData itemData = ItemDataRegistry.Find(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"[BackendPlayerNetworkSync] ItemData not found for equipped item id '{itemId}'.");
            return;
        }

        int stackCount = Mathf.Max(1, NetworkEquippedStackCount);
        _playerEquipment.EquipItem(new ItemInstance(itemData, stackCount), false);
    }

    private void HandleLocalUseAnimationRequested(ItemUseAnimationType animationType)
    {
        if (animationType == ItemUseAnimationType.None || Object == null || !Object.HasInputAuthority)
            return;

        if (HasStateAuthority)
        {
            SetNetworkUseAnimation(animationType);
        }
        else
        {
            RpcRequestUseAnimation((int)animationType);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcRequestUseAnimation(int animationType)
    {
        SetNetworkUseAnimation((ItemUseAnimationType)animationType);
    }

    private void SetNetworkUseAnimation(ItemUseAnimationType animationType)
    {
        if (animationType == ItemUseAnimationType.None)
            return;

        NetworkUseAnimationType = (int)animationType;
        NetworkUseAnimationCount++;

        if (!Object.HasInputAuthority)
            ApplyProxyUseAnimationState();
    }

    private void ApplyProxyUseAnimationState()
    {
        if (_playerAnimator == null || Object.HasInputAuthority)
            return;

        if (_lastAppliedUseAnimationCount == NetworkUseAnimationCount)
            return;

        _lastAppliedUseAnimationCount = NetworkUseAnimationCount;
        _playerAnimator.PlayUseItemAnimation((ItemUseAnimationType)NetworkUseAnimationType);
    }

    private static PlayerController FindLocalPlayerController()
    {
        PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController controller in controllers)
        {
            if (controller != null && controller.IsLocalPlayer)
                return controller;
        }

        return null;
    }

    // --- HOST to CLIENTS ---
        
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcRequestInventorySave()
    {
        if (!HasInputAuthority) return; // Only the local player responds
        
        // Client collects its inventory data
        if (Systems.GridInventory.GridInventory.Instance != null && Systems.GridInventory.GridInventory.Instance.Controller != null)
        {
            Systems.GridInventory.InventorySaveData data = Systems.GridInventory.GridInventorySaveSystem.GetSaveData(Systems.GridInventory.GridInventory.Instance.Controller.Model);
            string json = JsonUtility.ToJson(data);
            
            // Send back to Host
            RpcSendInventoryDataToHost(json);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcReceiveInventoryLoad(string json)
    {
        if (!HasInputAuthority) return; // Only the local player applies
        
        if (Systems.GridInventory.GridInventory.Instance != null && Systems.GridInventory.GridInventory.Instance.Controller != null)
        {
            Systems.GridInventory.InventorySaveData data = JsonUtility.FromJson<Systems.GridInventory.InventorySaveData>(json);
            Systems.GridInventory.GridInventorySaveSystem.ApplySaveData(Systems.GridInventory.GridInventory.Instance.Controller.Model, data);
        }
    }

    // --- CLIENT to HOST ---
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcSendInventoryDataToHost(string json)
    {
        // Host saves it to disk per player
        string playerId = Object.InputAuthority.PlayerId.ToString();
        Systems.GridInventory.GridInventorySaveSystem.SaveInventoryDataToDisk(playerId, json);
    }

    // --- GOLD SYNC ---

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestSpendGold(int amount)
    {
        if (!HasStateAuthority) return;

        if (BackendRoundManager.Instance != null && BackendRoundManager.Instance.SharedGold >= amount)
        {
            BackendRoundManager.Instance.SharedGold -= amount;
            RpcSpendGoldResult(true);
        }
        else
        {
            RpcSpendGoldResult(false);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestAddGold(int amount)
    {
        if (!HasStateAuthority) return;

        if (BackendRoundManager.Instance != null)
        {
            BackendRoundManager.Instance.SharedGold += amount;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RpcSpendGoldResult(NetworkBool success)
    {
        if (Systems.Shop.ShopController.Instance != null)
        {
            Systems.Shop.ShopController.Instance.OnSpendGoldResult(success);
        }
    }

    // --- HOST ACTIONS ---
    
    public void HostInitiateSaveAll()
    {
        if (!HasStateAuthority) return;
        
        foreach (var sync in FindObjectsByType<BackendPlayerNetworkSync>(FindObjectsSortMode.None))
        {
            sync.RpcRequestInventorySave();
        }

        if (Systems.Loot.LootNetworkSync.Instance != null)
        {
            Systems.Loot.LootNetworkSync.Instance.SaveAllLoots();
        }
    }

    public void HostInitiateLoadAll()
    {
        if (!HasStateAuthority) return;

        foreach (var sync in FindObjectsByType<BackendPlayerNetworkSync>(FindObjectsSortMode.None))
        {
            string playerId = sync.Object.InputAuthority.PlayerId.ToString();
            string json = Systems.GridInventory.GridInventorySaveSystem.LoadInventoryDataFromDisk(playerId);
            
            if (!string.IsNullOrEmpty(json))
            {
                sync.RpcReceiveInventoryLoad(json);
            }
        }
    }
}
