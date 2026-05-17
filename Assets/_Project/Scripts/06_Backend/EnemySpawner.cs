using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemySpawner : MonoBehaviour
{
    private static readonly Color SpawnerGizmoColor = new Color(1f, 0.25f, 0.1f, 0.9f);

    [Header("Spawn Settings")]
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [FormerlySerializedAs("spawnIntervalSeconds")]
    [SerializeField, Min(0.1f)] private float respawnDelaySeconds = 5f;

    private NetworkObject currentEnemy;
    private EnemyHealth currentEnemyHealth;
    private NetworkRunner cachedRunner;
    private float respawnAtTime = -1f;
    private bool wasRoundRunning;

    private void OnDisable()
    {
        UnsubscribeFromCurrentEnemy();
        currentEnemy = null;
        wasRoundRunning = false;
        respawnAtTime = -1f;
    }

    private void Update()
    {
        NetworkRunner runner = GetStateAuthorityRunner();
        if (runner == null)
        {
            return;
        }

        bool isRoundRunning = IsRoundRunning();
        if (!isRoundRunning)
        {
            if (wasRoundRunning)
            {
                DespawnCurrentEnemy(runner);
            }

            wasRoundRunning = false;
            respawnAtTime = -1f;
            return;
        }

        if (!wasRoundRunning)
        {
            wasRoundRunning = true;
            TrySpawnEnemy(runner);
            return;
        }

        if (HasLivingEnemy())
        {
            return;
        }

        if (respawnAtTime < 0f || Time.time < respawnAtTime)
        {
            return;
        }

        TrySpawnEnemy(runner);
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

    private void TrySpawnEnemy(NetworkRunner runner)
    {
        if (!TryGetSpawnTransform(out Vector3 position, out Quaternion rotation))
        {
            StartRespawnTimer();
            return;
        }

        NetworkObject spawnedEnemy = runner.Spawn(
            enemyPrefab,
            position,
            rotation,
            PlayerRef.None,
            onBeforeSpawned: (_, enemyObject) =>
            {
                if (enemyObject != null)
                {
                    ApplyEnemySpawnPose(enemyObject, position, rotation);
                }
            });

        if (spawnedEnemy != null)
        {
            ApplyEnemySpawnPose(spawnedEnemy, position, rotation);
            currentEnemy = spawnedEnemy;
            SubscribeToEnemy(spawnedEnemy);
            respawnAtTime = -1f;
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
        respawnAtTime = Time.time + respawnDelaySeconds;
    }

    private void DespawnCurrentEnemy(NetworkRunner runner)
    {
        if (currentEnemy != null && currentEnemy.IsValid && runner != null)
        {
            runner.Despawn(currentEnemy);
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

        return true;
    }

    private static void ApplyEnemySpawnPose(NetworkObject enemyObject, Vector3 position, Quaternion rotation)
    {
        enemyObject.transform.SetPositionAndRotation(position, rotation);

        EnemyAI enemyAI = enemyObject.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.ApplySpawnPose(position, rotation);
        }
    }

    private NetworkRunner GetStateAuthorityRunner()
    {
        if (cachedRunner != null && cachedRunner.IsRunning && cachedRunner.IsServer)
        {
            return cachedRunner;
        }

        NetworkRunner[] runners = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
        foreach (NetworkRunner runner in runners)
        {
            if (runner != null && runner.IsRunning && runner.IsServer)
            {
                cachedRunner = runner;
                return cachedRunner;
            }
        }

        cachedRunner = null;
        return null;
    }

    private void OnDrawGizmos()
    {
        DrawSpawnGizmo(transform.position, transform.rotation, SpawnerGizmoColor, 2.2f);
    }

    private static void DrawSpawnGizmo(Vector3 position, Quaternion rotation, Color color, float size)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, size);
        Gizmos.DrawSphere(position, size * 0.18f);
    }
}
