using Fusion;
using UnityEngine;

public struct BackendPlayerNetworkInput : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public NetworkButtons Buttons;

    public const int JumpButton = 0;
    public const int SprintButton = 1;
    public const int CrouchButton = 2;
    public const int InteractButton = 3;
    public const int ActionButton = 4;
}
