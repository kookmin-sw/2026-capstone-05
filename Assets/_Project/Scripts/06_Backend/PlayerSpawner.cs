using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 06_Backend 전용 스포너.
/// Room 입장 완료 이후 Player.prefab(NetworkObject + PlayerAdapter)을 찾아 스폰/디스폰만 담당한다.
/// </summary>
public class PlayerSpawner
{
    private const string LogSpawn = "[PlayerSpawner]";

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers;
    private readonly int _maxPlayers;

    private NetworkObject _playerPrefab;

    public PlayerSpawner(Dictionary<PlayerRef, NetworkObject> spawnedPlayers, int maxPlayers)
    {
        _spawnedPlayers = spawnedPlayers;
        _maxPlayers = Mathf.Max(1, maxPlayers);
    }

    public bool TrySpawn(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
            return false;

        EnsurePlayerPrefabAssigned();
        if (_playerPrefab == null)
        {
            Debug.LogError($"{LogSpawn} Player prefab resolve failed. player={player}");
            return false;
        }

        if (_spawnedPlayers.ContainsKey(player))
        {
            Debug.Log($"{LogSpawn} Skip duplicate spawn. player={player}");
            return false;
        }

        Vector3 spawnPosition = GetSpawnPosition(player);
        Debug.Log($"{LogSpawn} Spawn start. prefab={_playerPrefab.name}, player={player}, pos={spawnPosition}");

        NetworkObject playerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
        _spawnedPlayers[player] = playerObject;

        Debug.Log($"{LogSpawn} Spawn done. object={playerObject?.name}, inputAuth={playerObject?.InputAuthority}, stateAuth={playerObject?.StateAuthority}, local={runner.LocalPlayer}");
        return true;
    }

    public void Despawn(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null)
            return;

        if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObject) && playerObject != null)
        {
            Debug.Log($"{LogSpawn} Despawn. player={player}, object={playerObject.name}");
            runner.Despawn(playerObject);
        }

        _spawnedPlayers.Remove(player);
    }

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        int index = Mathf.Abs(player.RawEncoded % _maxPlayers);
        float angle = (360f / _maxPlayers) * index;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * 2.5f);
        return new Vector3(0f, 1f, 0f) + offset;
    }

    private void EnsurePlayerPrefabAssigned()
    {
        if (_playerPrefab != null)
            return;

        if (NetworkProjectConfig.Global?.PrefabTable == null)
            return;

        foreach ((NetworkPrefabId _, INetworkPrefabSource source) in NetworkProjectConfig.Global.PrefabTable.GetEntries())
        {
            NetworkObject candidate = TryResolvePrefab(source);
            if (candidate == null)
                continue;

            // 02_Player Player.prefab + 06_Backend adapter 연결 구조를 우선 탐색
            bool hasPlayerController = candidate.GetComponent<PlayerController>() != null;
            bool hasAdapter = candidate.GetComponent<PlayerAdapter>() != null || candidate.GetComponent<BackendPlayerNetworkAdapter>() != null;
            bool isPlayerName = string.Equals(candidate.name, "Player", StringComparison.OrdinalIgnoreCase);

            if (hasPlayerController && hasAdapter && isPlayerName)
            {
                _playerPrefab = candidate;
                Debug.Log($"{LogSpawn} Preferred prefab selected: {candidate.name}");
                return;
            }
        }

        foreach ((NetworkPrefabId _, INetworkPrefabSource source) in NetworkProjectConfig.Global.PrefabTable.GetEntries())
        {
            NetworkObject candidate = TryResolvePrefab(source);
            if (candidate == null)
                continue;

            if (candidate.GetComponent<PlayerAdapter>() != null || candidate.GetComponent<BackendPlayerNetworkAdapter>() != null)
            {
                _playerPrefab = candidate;
                Debug.LogWarning($"{LogSpawn} Fallback prefab selected: {candidate.name}");
                return;
            }
        }
    }

    private static NetworkObject TryResolvePrefab(INetworkPrefabSource source)
    {
        switch (source)
        {
            case NetworkPrefabSourceStatic staticSource:
                return staticSource.Object;
            case NetworkPrefabSourceStaticLazy lazySource:
                return lazySource.Object.asset;
            case NetworkPrefabSourceResource resourceSource:
                try
                {
                    resourceSource.Acquire(true);
                    return resourceSource.WaitForResult();
                }
                finally
                {
                    try { resourceSource.Release(); } catch { }
                }
            default:
                return null;
        }
    }
}
