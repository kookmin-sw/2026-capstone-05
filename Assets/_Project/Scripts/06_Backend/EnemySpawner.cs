using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(NetworkObject))]
public class EnemySpawner : NetworkBehaviour
{
    private static readonly Color SpawnerGizmoColor = new Color(1f, 0.25f, 0.1f, 0.9f);
    private static readonly Color SpawnPointGizmoColor = new Color(1f, 0.75f, 0.1f, 0.9f);
    private static readonly Color SpawnLinkGizmoColor = new Color(1f, 0.6f, 0.1f, 0.35f);

    [Header("Spawn Settings")]
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [FormerlySerializedAs("spawnIntervalSeconds")]
    [SerializeField, Min(0.1f)] private float respawnDelaySeconds = 5f;

    [Networked] private TickTimer RespawnTimer { get; set; }

    private NetworkObject currentEnemy;
    private EnemyHealth currentEnemyHealth;
    private bool wasRoundRunning;

    public override void Spawned()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        wasRoundRunning = IsRoundRunning();
        RespawnTimer = TickTimer.None;

        if (wasRoundRunning)
        {
            TrySpawnEnemy();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        bool isRoundRunning = IsRoundRunning();
        if (!isRoundRunning)
        {
            if (wasRoundRunning)
            {
                DespawnCurrentEnemy();
            }

            wasRoundRunning = false;
            RespawnTimer = TickTimer.None;
            return;
        }

        if (!wasRoundRunning)
        {
            wasRoundRunning = true;
            TrySpawnEnemy();
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
        wasRoundRunning = false;
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
            if (IsRoundRunning())
            {
                StartRespawnTimer();
            }
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

        if (IsRoundRunning())
        {
            StartRespawnTimer();
        }
    }

    private void StartRespawnTimer()
    {
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelaySeconds);
    }

    private void DespawnCurrentEnemy()
    {
        if (currentEnemy != null && currentEnemy.IsValid && Runner != null)
        {
            Runner.Despawn(currentEnemy);
        }

        UnsubscribeFromCurrentEnemy();
        currentEnemy = null;
    }

    private bool IsRoundRunning()
    {
        return BackendRoundManager.Instance != null && BackendRoundManager.Instance.IsRoundRunning;
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

    private void OnDrawGizmos()
    {
        DrawSpawnGizmo(transform.position, transform.rotation, SpawnerGizmoColor, 2.2f);

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
            {
                continue;
            }

            Gizmos.color = SpawnLinkGizmoColor;
            Gizmos.DrawLine(transform.position, spawnPoint.position);
            DrawSpawnGizmo(spawnPoint.position, spawnPoint.rotation, SpawnPointGizmoColor, 1.9f);
        }
    }

    private static void DrawSpawnGizmo(Vector3 position, Quaternion rotation, Color color, float size)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, size);
        Gizmos.DrawSphere(position, size * 0.18f);
    }
}
