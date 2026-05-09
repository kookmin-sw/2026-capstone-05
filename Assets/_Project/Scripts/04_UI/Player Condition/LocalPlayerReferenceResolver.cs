using Fusion;
using UnityEngine;

public static class LocalPlayerReferenceResolver
{
    public static bool TryGetLocalPlayer(out PlayerController player)
    {
        player = null;

        PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (controllers == null || controllers.Length == 0)
            return false;

        NetworkRunner runner = Object.FindAnyObjectByType<NetworkRunner>();
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
                    player = controller;
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
                player = controller;
                return true;
            }
        }

        if (controllers.Length == 1)
        {
            player = controllers[0];
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
}
