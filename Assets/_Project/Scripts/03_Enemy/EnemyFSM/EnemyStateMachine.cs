using UnityEngine;

public class EnemyStateMachine
{
    public EnemyState CurrentState { get; private set; }

    public void Initialize(EnemyState startingState)
    {
        CurrentState = startingState;
        if (CurrentState != null)
        {
            CurrentState.Enter();
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (CurrentState == newState)
            return;

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
