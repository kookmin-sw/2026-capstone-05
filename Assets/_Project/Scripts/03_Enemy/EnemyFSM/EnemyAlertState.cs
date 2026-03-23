using UnityEngine;

public class EnemyAlertState : EnemyState
{
    public EnemyAlertState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
    }

    public override void LogicUpdate()
    {
        if (enemy.NoiseSuspicionLevel >= 100f)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.NoiseSuspicionLevel < 50f)
        {
            stateMachine.ChangeState(enemy.IdleState);
            return;
        }

        // 소음 방향 응시
        LookAtNoisePosition();
    }

    private void LookAtNoisePosition()
    {
        Vector3 direction = (enemy.DetectedNoisePosition - enemy.transform.position).normalized;
        direction.y = 0f;

        if (direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        enemy.transform.rotation = Quaternion.Slerp(
            enemy.transform.rotation,
            targetRotation,
            Time.deltaTime * 10f
        );
    }
}
