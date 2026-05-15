using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(NetworkObject))]
public class EnemySpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [FormerlySerializedAs("spawnIntervalSeconds")]
    [SerializeField, Min(0.1f)] private float respawnDelaySeconds = 5f;

    [Networked] private TickTimer RespawnTimer { get; set; }

    private NetworkObject currentEnemy;
    private EnemyHealth currentEnemyHealth;

    public override void Spawned()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        TrySpawnEnemy();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (HasLivingEnemy())
        {
            return;
        }

        if (!RespawnTimer.Expired(Runner))
        {
            return;
        }

        TrySpawnEnemy();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnsubscribeFromCurrentEnemy();
        currentEnemy = null;
    }

    private bool HasLivingEnemy()
    {
        if (currentEnemy != null && currentEnemy.IsValid)
        {
            return true;
        }

        if (currentEnemy != null)
        {
            currentEnemy = null;
            UnsubscribeFromCurrentEnemy();
            StartRespawnTimer();
        }

        return false;
    }

    private void TrySpawnEnemy()
    {
        if (!TryGetSpawnTransform(out Vector3 position, out Quaternion rotation))
        {
            StartRespawnTimer();
            return;
        }

        NetworkObject spawnedEnemy = Runner.Spawn(enemyPrefab, position, rotation, PlayerRef.None);
        if (spawnedEnemy != null)
        {
            currentEnemy = spawnedEnemy;
            SubscribeToEnemy(spawnedEnemy);
            RespawnTimer = TickTimer.None;
            return;
        }

        StartRespawnTimer();
    }

    private void SubscribeToEnemy(NetworkObject enemyObject)
    {
        UnsubscribeFromCurrentEnemy();

        currentEnemyHealth = enemyObject.GetComponent<EnemyHealth>();
        if (currentEnemyHealth == null)
        {
            Debug.LogWarning("[EnemySpawner] Spawned enemy does not have EnemyHealth.", enemyObject);
            return;
        }

        currentEnemyHealth.Died += HandleCurrentEnemyDied;
    }

    private void UnsubscribeFromCurrentEnemy()
    {
        if (currentEnemyHealth != null)
        {
            currentEnemyHealth.Died -= HandleCurrentEnemyDied;
            currentEnemyHealth = null;
        }
    }

    private void HandleCurrentEnemyDied(EnemyHealth enemyHealth)
    {
        if (enemyHealth != currentEnemyHealth)
        {
            return;
        }

        UnsubscribeFromCurrentEnemy();
        currentEnemy = null;
        StartRespawnTimer();
    }

    private void StartRespawnTimer()
    {
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelaySeconds);
    }

    private bool TryGetSpawnTransform(out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;

        if (enemyPrefab.IsValid == false)
        {
            Debug.LogWarning("[EnemySpawner] enemyPrefab 이 비어있어 스폰할 수 없습니다.", this);
            return false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return true;
        }

        int index = Random.Range(0, spawnPoints.Length);
        Transform selected = spawnPoints[index];
        if (selected == null)
        {
            return true;
        }

        position = selected.position;
        rotation = selected.rotation;
        return true;
    }
}
