using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float idleStartTime = 2f;
    private float idleEndTime = 4f;
    private float idleDuration;
    private float elapsedTime;

    public EnemyIdleState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        idleDuration = Random.Range(idleStartTime, idleEndTime);
        elapsedTime = 0f;
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        if (enemy.SuspicionLevel >= 100f)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.SuspicionLevel >= 50f)
        {
            stateMachine.ChangeState(enemy.AlertState);
            return;
        }

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= idleDuration)
        {
            stateMachine.ChangeState(enemy.PatrolState);
        }
    }
}
