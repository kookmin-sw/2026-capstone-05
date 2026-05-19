using Fusion;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemySpawner : MonoBehaviour
{
    private enum SpawnSlot
    {
        None,
        Default,
        Night
    }

    private static readonly Color SpawnerGizmoColor = new Color(1f, 0.25f, 0.1f, 0.9f);

    [Header("Spawn Settings")]
    [Tooltip("기본 적 프리팹입니다. Night Enemy Prefab이 비어 있으면 낮과 밤 모두 이 프리팹을 사용합니다.")]
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [Tooltip("밤에 사용할 적 프리팹입니다. 기본 프리팹 없이 이것만 할당하면 밤에만 스폰됩니다.")]
    [SerializeField] private NetworkPrefabRef nightEnemyPrefab;
    [Tooltip("밤 여부를 판단할 DayNightCycle입니다. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private DayNightCycle dayNightCycle;
    [FormerlySerializedAs("spawnIntervalSeconds")]
    [SerializeField, Min(0.1f)] private float respawnDelaySeconds = 5f;

    private NetworkObject currentEnemy;
    private EnemyHealth currentEnemyHealth;
    private NetworkRunner cachedRunner;
    private DayNightCycle cachedDayNightCycle;
    private SpawnSlot currentSpawnSlot = SpawnSlot.None;
    private SpawnSlot lastDesiredSpawnSlot = SpawnSlot.None;
    private float respawnAtTime = -1f;
    private bool wasRoundRunning;
    private bool warnedMissingDayNightCycle;
    private bool warnedMissingPrefab;

    private void OnDisable()
    {
        UnsubscribeFromCurrentEnemy();
        currentEnemy = null;
        currentSpawnSlot = SpawnSlot.None;
        lastDesiredSpawnSlot = SpawnSlot.None;
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
            lastDesiredSpawnSlot = SpawnSlot.None;
            respawnAtTime = -1f;
            return;
        }

        SpawnSlot desiredSpawnSlot = GetCurrentSpawnSlot();
        if (desiredSpawnSlot == SpawnSlot.None)
        {
            DespawnCurrentEnemy(runner);
            wasRoundRunning = false;
            lastDesiredSpawnSlot = SpawnSlot.None;
            respawnAtTime = -1f;
            return;
        }

        bool desiredSpawnSlotChanged = desiredSpawnSlot != lastDesiredSpawnSlot;
        lastDesiredSpawnSlot = desiredSpawnSlot;

        if (!wasRoundRunning || desiredSpawnSlotChanged)
        {
            wasRoundRunning = true;
            respawnAtTime = -1f;

            if (HasLivingEnemy())
            {
                if (currentSpawnSlot != desiredSpawnSlot)
                {
                    DespawnCurrentEnemy(runner);
                    TrySpawnEnemy(runner, desiredSpawnSlot);
                }

                return;
            }

            TrySpawnEnemy(runner, desiredSpawnSlot);
            return;
        }

        if (HasLivingEnemy())
        {
            if (currentSpawnSlot != desiredSpawnSlot)
            {
                DespawnCurrentEnemy(runner);
                TrySpawnEnemy(runner, desiredSpawnSlot);
            }

            return;
        }

        if (respawnAtTime < 0f || Time.time < respawnAtTime)
        {
            return;
        }

        TrySpawnEnemy(runner, desiredSpawnSlot);
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
            currentSpawnSlot = SpawnSlot.None;
            UnsubscribeFromCurrentEnemy();
            if (IsRoundRunning())
            {
                StartRespawnTimer();
            }
        }

        return false;
    }

    private void TrySpawnEnemy(NetworkRunner runner, SpawnSlot spawnSlot)
    {
        NetworkPrefabRef prefabToSpawn = GetPrefab(spawnSlot);
        if (!TryGetSpawnTransform(prefabToSpawn, out Vector3 position, out Quaternion rotation))
        {
            StartRespawnTimer();
            return;
        }

        NetworkObject spawnedEnemy = runner.Spawn(
            prefabToSpawn,
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
            currentSpawnSlot = spawnSlot;
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
        currentSpawnSlot = SpawnSlot.None;

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
        currentSpawnSlot = SpawnSlot.None;
    }

    private bool IsRoundRunning()
    {
        return BackendRoundManager.Instance != null && BackendRoundManager.Instance.IsRoundRunning;
    }

    private SpawnSlot GetCurrentSpawnSlot()
    {
        bool hasDefaultPrefab = enemyPrefab.IsValid;
        bool hasNightPrefab = nightEnemyPrefab.IsValid;

        if (!hasDefaultPrefab && !hasNightPrefab)
        {
            return SpawnSlot.None;
        }

        if (!hasNightPrefab)
        {
            return SpawnSlot.Default;
        }

        if (!hasDefaultPrefab)
        {
            return IsNightTime() ? SpawnSlot.Night : SpawnSlot.None;
        }

        return IsNightTime() ? SpawnSlot.Night : SpawnSlot.Default;
    }

    private NetworkPrefabRef GetPrefab(SpawnSlot spawnSlot)
    {
        switch (spawnSlot)
        {
            case SpawnSlot.Default:
                return enemyPrefab;
            case SpawnSlot.Night:
                return nightEnemyPrefab;
            default:
                return default(NetworkPrefabRef);
        }
    }

    private bool IsNightTime()
    {
        DayNightCycle cycle = ResolveDayNightCycle();
        return cycle != null && cycle.IsNightTime;
    }

    private DayNightCycle ResolveDayNightCycle()
    {
        if (dayNightCycle != null)
        {
            cachedDayNightCycle = dayNightCycle;
            return cachedDayNightCycle;
        }

        if (cachedDayNightCycle != null)
        {
            return cachedDayNightCycle;
        }

        cachedDayNightCycle = FindFirstObjectByType<DayNightCycle>();
        if (cachedDayNightCycle == null && nightEnemyPrefab.IsValid && !warnedMissingDayNightCycle)
        {
            Debug.LogWarning("[EnemySpawner] Night Enemy Prefab을 시간대에 맞춰 사용하려면 DayNightCycle 참조가 필요합니다.", this);
            warnedMissingDayNightCycle = true;
        }

        return cachedDayNightCycle;
    }

    private bool TryGetSpawnTransform(NetworkPrefabRef prefabToSpawn, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;

        if (prefabToSpawn.IsValid == false)
        {
            if (!warnedMissingPrefab)
            {
                Debug.LogWarning("[EnemySpawner] 스폰할 enemy prefab 이 비어있어 스폰할 수 없습니다.", this);
                warnedMissingPrefab = true;
            }

            return false;
        }

        warnedMissingPrefab = false;
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
