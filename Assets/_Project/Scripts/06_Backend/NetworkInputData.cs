using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public NetworkButtons Buttons;

    public const int Jump = 0;
    public const int Sprint = 1;
    public const int Crouch = 2;
}
