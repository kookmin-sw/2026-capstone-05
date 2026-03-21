using UnityEngine;

public class EnemyAlertState : EnemyState
{
    private float fallbackDuration = 1.5f;
    private float alertTimer;

    public EnemyAlertState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        alertTimer = fallbackDuration;
    }

    public override void Exit()
    {
        enemy.Agent.isStopped = false;
    }

    public override void LogicUpdate()
    {
        LookAtNoisePosition();

        alertTimer -= Time.deltaTime;
        if (alertTimer <= 0f)
        {
            stateMachine.ChangeState(enemy.ChaseState);
        }
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
