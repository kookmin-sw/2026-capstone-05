using UnityEngine;

public class PlayerStateMachine
{
    public PlayerState CurrentState { get; private set; }

    public void Initialize(PlayerState startingState)
    {
        CurrentState = startingState;
        if (CurrentState != null)
        {
            CurrentState.Enter();
        }
    }

    public void ChangeState(PlayerState newState)
    {
        if (CurrentState != null)
        {
            CurrentState.Exit();
        }
        CurrentState = newState;
        if (CurrentState != null)
        {
            CurrentState.Enter();
        }
    }
}
