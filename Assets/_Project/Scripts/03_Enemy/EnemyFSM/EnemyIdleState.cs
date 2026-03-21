using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float idleMinTime = 2f;
    private float idleMaxTime = 4f;
    private float idleDuration;
    private float elapsedTime;

    public EnemyIdleState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;

        idleDuration = Random.Range(idleMinTime, idleMaxTime);
        elapsedTime = 0f;
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        // 소음 감지 시 AlertState 진입
        if (enemy.HasNoiseDetected)
        {
            enemy.ConsumeNoiseDetection();
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
