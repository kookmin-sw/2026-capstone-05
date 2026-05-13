using Fusion;
using UnityEngine;

public static class LocalPlayerReferenceResolver
{
    private static PlayerController cachedPlayer;
    private static NetworkRunner cachedRunner;

    public static bool TryGetLocalPlayer(out PlayerController player)
    {
        player = null;

        if (IsCachedPlayerValid())
        {
            player = cachedPlayer;
            return true;
        }

        cachedPlayer = null;

        PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (controllers == null || controllers.Length == 0)
            return false;

        NetworkRunner runner = GetRunner();
        bool hasNetworkPlayer = false;
        if (runner != null)
        {
            foreach (PlayerController controller in controllers)
            {
                if (controller == null)
                    continue;

                NetworkObject networkObject = controller.GetComponent<NetworkObject>();
                hasNetworkPlayer |= networkObject != null;
                if (networkObject != null && networkObject.InputAuthority == runner.LocalPlayer)
                {
                    cachedPlayer = controller;
                    player = cachedPlayer;
                    return true;
                }
            }

            if (hasNetworkPlayer)
                return false;
        }

        foreach (PlayerController controller in controllers)
        {
            if (controller != null && controller.IsLocalPlayer)
            {
                cachedPlayer = controller;
                player = cachedPlayer;
                return true;
            }
        }

        if (controllers.Length == 1)
        {
            cachedPlayer = controllers[0];
            player = cachedPlayer;
            return player != null;
        }

        return false;
    }

    public static bool TryGetLocalCondition(out PlayerCondition condition)
    {
        condition = null;

        if (!TryGetLocalPlayer(out PlayerController player) || player == null)
            return false;

        condition = player.Condition != null ? player.Condition : player.GetComponent<PlayerCondition>();
        return condition != null;
    }

    public static bool TryGetLocalNoiseEmitter(out PlayerNoiseEmitter noiseEmitter)
    {
        noiseEmitter = null;

        if (!TryGetLocalPlayer(out PlayerController player) || player == null)
            return false;

        noiseEmitter = player.NoiseEmitter != null ? player.NoiseEmitter : player.GetComponent<PlayerNoiseEmitter>();
        return noiseEmitter != null;
    }

    private static bool IsCachedPlayerValid()
    {
        if (cachedPlayer == null)
            return false;

        NetworkObject networkObject = cachedPlayer.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            NetworkRunner runner = GetRunner();
            return runner != null && networkObject.InputAuthority == runner.LocalPlayer;
        }

        return cachedPlayer.IsLocalPlayer || (GetRunner() == null && cachedPlayer.gameObject.scene.IsValid());
    }

    private static NetworkRunner GetRunner()
    {
        if (cachedRunner == null)
        {
            cachedRunner = Object.FindFirstObjectByType<NetworkRunner>();
        }

        return cachedRunner;
    }
}
