using System.Collections.Generic;
using Fusion;

public static class BackendLocalInputRegistry
{
    private static readonly Dictionary<NetworkRunner, NetworkInputBridge> RunnerToInput = new();

    public static void Register(NetworkRunner runner, NetworkInputBridge bridge)
    {
        if (runner == null || bridge == null)
            return;

        RunnerToInput[runner] = bridge;
    }

    public static void Unregister(NetworkRunner runner, NetworkInputBridge bridge)
    {
        if (runner == null || bridge == null)
            return;

        if (RunnerToInput.TryGetValue(runner, out NetworkInputBridge current) && current == bridge)
            RunnerToInput.Remove(runner);
    }

    public static bool TryCollect(NetworkRunner runner, out BackendNetworkInputData inputData)
    {
        inputData = default;

        if (runner == null)
            return false;

        if (!RunnerToInput.TryGetValue(runner, out NetworkInputBridge bridge) || bridge == null)
            return false;

        return bridge.TryBuildNetworkInput(out inputData);
    }
}
