using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float idleDuration;
    private float elapsedTime;

    public EnemyIdleState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        idleDuration = Random.Range(enemy.Data.idleMinTime, enemy.Data.idleMaxTime);
        elapsedTime = 0f;
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        // 의심 수치 100 이상: ChaseState 진입
        if (enemy.NoiseSuspicionLevel >= 100f)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        // 의심 수치 50 이상: AlertState 진입
        if (enemy.NoiseSuspicionLevel >= 50f)
        {
            stateMachine.ChangeState(enemy.AlertState);
            return;
        }

        // 대기 시간 종료 시 PatrolState 진입
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= idleDuration)
        {
            stateMachine.ChangeState(enemy.PatrolState);
        }
    }
}
