using System.Collections;
using Systems.GridInventory;
using UnityEngine;

[RequireComponent(typeof(PlayerCondition))]
[RequireComponent(typeof(PlayerController))]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private float respawnDelaySeconds = 3f;

    private PlayerCondition condition;
    private PlayerController controller;
    private BackendPlayerNetworkSync networkSync;
    private bool isRespawning;
    private bool suppressNextDeathDrop;

    private void Awake()
    {
        condition = GetComponent<PlayerCondition>();
        controller = GetComponent<PlayerController>();
        networkSync = GetComponent<BackendPlayerNetworkSync>();
    }

    private void Start()
    {
        SpawnAtSpawner();
    }

    private void OnEnable()
    {
        if (condition != null)
        {
            condition.OnDiedEvent += HandlePlayerDied;
        }
    }

    private void OnDisable()
    {
        if (condition != null)
        {
            condition.OnDiedEvent -= HandlePlayerDied;
        }
    }

    private void HandlePlayerDied()
    {
        if (isRespawning)
        {
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        Vector3 deathPosition = transform.position;

        controller.canAction = false;
        controller.canLook = false;
        controller.currentVelocity = Vector3.zero;
        controller.Equipment?.UnequipItem();

        if (suppressNextDeathDrop)
        {
            suppressNextDeathDrop = false;
        }
        else
        {
            DropInventoryAtDeathPosition(deathPosition);
        }

        yield return new WaitForSeconds(respawnDelaySeconds);

        SpawnAtSpawner();
        condition.Revive();
        networkSync?.PublishConditionResetSnapshot();

        controller.ResetMovementStateForRespawn();

        isRespawning = false;
    }

    private void DropInventoryAtDeathPosition(Vector3 deathPosition)
    {
        if (!ShouldDropLocalInventoryOnDeath())
            return;

        GlobalDragDropRouter.DropAllPlayerItemsAt(deathPosition);
    }

    private bool ShouldDropLocalInventoryOnDeath()
    {
        return networkSync == null || networkSync.Object == null || networkSync.Object.HasInputAuthority;
    }

    public void ApplyRoundEndBunkerPenaltyIfOutside()
    {
        if (condition == null || condition.IsInBunker)
        {
            return;
        }

        suppressNextDeathDrop = false;
        GlobalDragDropRouter.ClearAllPlayerItemsWithoutDrop();
        controller.Equipment?.UnequipItem();
    }

    [ContextMenu("Spawn At Spawner")]
    public void SpawnAtSpawner()
    {
        suppressNextDeathDrop = false;

        if (Spawner.Instance == null)
        {
            Debug.LogWarning("[PlayerRespawn] Spawner.Instance를 찾지 못했습니다.");
            return;
        }

        Transform spawnPoint = Spawner.Instance.GetSpawnPoint();
        CharacterController characterController = controller.Controller;

        if (characterController != null && controller != null)
        {
            Vector3 point1 = transform.position + characterController.center + Vector3.up * (characterController.height * 0.5f - characterController.radius);
            Vector3 point2 = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f - characterController.radius);

            Collider[] hits = Physics.OverlapCapsule(point1, point2, characterController.radius, Physics.AllLayers, QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (hit.isTrigger)
                {
                    var triggerScript = hit.GetComponent<IndoorsTrigger>();
                    if (triggerScript != null)
                    {
                        triggerScript.OnPlayerForceExit(controller);
                    }
                }
            }
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        condition?.SetInBunker(true);
        controller?.ResetMovementStateForRespawn();
    }
}
