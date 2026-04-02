using UnityEngine;

public class EnemyAlertState : EnemyState
{
    private bool isLeftTurn;

    public EnemyAlertState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        enemy.Agent.updateRotation = false;
        enemy.Animator.SetBool("IsAlert", true);
        enemy.AnimationEventHandler.OnTurnEnd += HandleTurnEnd;
        enemy.AnimationEventHandler.OnTurnREnd += HandleTurnREnd;

        isLeftTurn = GetTurnDirection();
        enemy.Animator.SetTrigger(isLeftTurn ? "TurnLeft" : "TurnRight");
    }

    public override void Exit()
    {
        enemy.AnimationEventHandler.OnTurnEnd -= HandleTurnEnd;
        enemy.AnimationEventHandler.OnTurnREnd -= HandleTurnREnd;

        enemy.Animator.ResetTrigger("TurnLeft");
        enemy.Animator.ResetTrigger("TurnRight");
        enemy.Animator.ResetTrigger("TurnLeftR");
        enemy.Animator.ResetTrigger("TurnRightR");
        enemy.Animator.SetBool("IsAlert", false);
        enemy.Animator.CrossFade("Locomotion", 0.2f);

        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);

        if (enemy.DetectedNoisePosition != Vector3.zero)
            enemy.LookAtDetectedNoisePosition();

        if (enemy.IsPlayerInAttackRadius())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        if (enemy.SuspicionLevel >= enemy.Data.chaseThreshold)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.SuspicionLevel >= enemy.Data.searchThreshold)
        {
            stateMachine.ChangeState(enemy.SearchState);
            return;
        }

        if (enemy.SuspicionLevel < enemy.Data.alertThreshold)
        {
            stateMachine.ChangeState(enemy.PatrolState);
            return;
        }
    }

    private bool GetTurnDirection()
    {
        if (enemy.DetectedNoisePosition == Vector3.zero)
            return true;

        Vector3 toNoise = enemy.DetectedNoisePosition - enemy.transform.position;
        toNoise.y = 0f;
        float angle = Vector3.SignedAngle(enemy.transform.forward, toNoise, Vector3.up);
        return angle < 0f;
    }

    private void HandleTurnEnd()
    {
        enemy.Animator.SetTrigger(isLeftTurn ? "TurnLeftR" : "TurnRightR");
    }

    private void HandleTurnREnd()
    {
        if (enemy.SuspicionLevel < enemy.Data.alertThreshold)
        {
            stateMachine.ChangeState(enemy.PatrolState);
            return;
        }

        enemy.Animator.SetTrigger(isLeftTurn ? "TurnLeft" : "TurnRight");
    }
}
