using UnityEngine;

public abstract class EnemyState
{
    protected EnemyAI enemy;
    protected EnemyStateMachine stateMachine;

    public EnemyState(EnemyAI enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void LogicUpdate() { }
    public virtual void PhysicsUpdate() { }
}
