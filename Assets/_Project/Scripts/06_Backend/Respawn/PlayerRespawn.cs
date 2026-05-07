using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerCondition))]
[RequireComponent(typeof(PlayerController))]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private float respawnDelaySeconds = 3f;

    private PlayerCondition condition;
    private PlayerController controller;
    private bool isRespawning;

    private void Awake()
    {
        condition = GetComponent<PlayerCondition>();
        controller = GetComponent<PlayerController>();
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

        controller.canAction = false;
        controller.canLook = false;
        controller.currentVelocity = Vector3.zero;

        yield return new WaitForSeconds(respawnDelaySeconds);

        SpawnAtSpawner();
        condition.ReviveToFull();

        controller.currentVelocity = Vector3.zero;
        controller.canAction = true;
        controller.canLook = true;

        isRespawning = false;
    }

    [ContextMenu("Spawn At Spawner")]
    public void SpawnAtSpawner()
    {
        if (Spawner.Instance == null)
        {
            Debug.LogWarning("[PlayerRespawn] Spawner.Instance를 찾지 못했습니다.");
            return;
        }

        Transform spawnPoint = Spawner.Instance.GetSpawnPoint();
        CharacterController characterController = controller.Controller;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }
}
