using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class EnemySpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(0.1f)] private float spawnIntervalSeconds = 5f;
    [SerializeField, Min(1)] private int maxAliveEnemies = 10;

    [Networked] private TickTimer SpawnTimer { get; set; }

    private readonly List<NetworkObject> _aliveEnemies = new List<NetworkObject>();

    public override void Spawned()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnIntervalSeconds);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        CleanupDestroyedEnemies();

        if (_aliveEnemies.Count >= maxAliveEnemies)
        {
            return;
        }

        if (!SpawnTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        if (!TryGetSpawnTransform(out Vector3 position, out Quaternion rotation))
        {
            SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnIntervalSeconds);
            return;
        }

        NetworkObject spawnedEnemy = Runner.Spawn(enemyPrefab, position, rotation, PlayerRef.None);
        if (spawnedEnemy != null)
        {
            _aliveEnemies.Add(spawnedEnemy);
        }

        SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnIntervalSeconds);
    }

    private void CleanupDestroyedEnemies()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (_aliveEnemies[i] == null || !_aliveEnemies[i].IsValid)
            {
                _aliveEnemies.RemoveAt(i);
            }
        }
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
