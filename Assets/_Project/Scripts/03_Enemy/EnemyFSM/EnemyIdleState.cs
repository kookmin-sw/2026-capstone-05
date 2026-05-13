using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float stopTimer;

    public EnemyIdleState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        stopTimer = Random.Range(enemy.Data.idleStartTime, enemy.Data.idleEndTime);
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", 0f, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        if (enemy.TryChangeStateBySuspicion())
            return;

        stopTimer -= Time.deltaTime;
        if (stopTimer <= 0f)
        {
            stateMachine.ChangeState(enemy.PatrolState);
        }
    }
}
