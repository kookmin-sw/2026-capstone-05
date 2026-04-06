using Fusion;
using UnityEngine;

public struct BackendNetworkInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public NetworkBool JumpPressed;
    public NetworkBool SprintPressed;
}
