using UnityEngine;
using UnityEngine.AI;

public class EnemyAlertState : EnemyState
{
    private bool useArcMovement;
    private bool isFacingTarget;
    private float arcRadius;

    public EnemyAlertState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Animator.SetBool("IsAlert", true);

        bool shouldTurn = enemy.SuspicionLevel >= enemy.Data.lookThreshold
            && enemy.DetectedNoisePosition != Vector3.zero
            && GetDirectionAngle() >= enemy.Data.alignAngleThreshold;

        if (shouldTurn && Random.value > 0.5f)
        {
            useArcMovement = true;
            isFacingTarget = false;
            arcRadius = Random.Range(enemy.Data.alertArcRadiusMin, enemy.Data.alertArcRadiusMax);
            enemy.Agent.isStopped = false;
            enemy.Agent.updateRotation = true;
            enemy.Agent.speed = enemy.Data.chaseSpeed;
        }
        else
        {
            useArcMovement = false;
            enemy.Agent.isStopped = true;
            enemy.Agent.updateRotation = false;
        }
    }

    public override void Exit()
    {
        enemy.Animator.SetBool("IsAlert", false);
        enemy.Animator.CrossFade("Locomotion", 0.2f);
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        if (useArcMovement && !isFacingTarget)
        {
            UpdateArcDestination();
            enemy.Animator.SetFloat("Speed", enemy.Agent.velocity.magnitude / enemy.Data.chaseSpeed, 0.2f, Time.deltaTime);

            if (GetDirectionAngle() < enemy.Data.alignAngleThreshold)
            {
                isFacingTarget = true;
                enemy.Agent.isStopped = true;
                enemy.Agent.updateRotation = false;
            }
        }
        else
        {
            bool shouldLook = enemy.SuspicionLevel >= enemy.Data.lookThreshold
                && enemy.DetectedNoisePosition != Vector3.zero
                && GetDirectionAngle() >= enemy.Data.alignAngleThreshold;

            if (shouldLook)
            {
                enemy.Animator.SetFloat("Speed", 0.5f, 0.2f, Time.deltaTime);
                enemy.LookAtDetectedNoisePosition();
            }
            else
            {
                enemy.Animator.SetFloat("Speed", 0f, 0.2f, Time.deltaTime);
            }
        }

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

    private void UpdateArcDestination()
    {
        if (enemy.DetectedNoisePosition == Vector3.zero) return;

        Vector3 noiseDirection = enemy.DetectedNoisePosition - enemy.transform.position;
        noiseDirection.y = 0f;

        float signedAngle = Vector3.SignedAngle(enemy.transform.forward, noiseDirection, Vector3.up);
        float side = signedAngle < 0f ? -arcRadius : arcRadius;

        Vector3 candidate = enemy.transform.position
            + enemy.transform.forward * arcRadius
            + enemy.transform.right * side;

        const float navMeshSampleRange = 2f;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRange, NavMesh.AllAreas))
            enemy.Agent.SetDestination(hit.position);
        else
            enemy.Agent.isStopped = true;
    }

    private float GetDirectionAngle()
    {
        if (enemy.DetectedNoisePosition == Vector3.zero) return 0f;

        Vector3 noiseDirection = enemy.DetectedNoisePosition - enemy.transform.position;
        noiseDirection.y = 0f;
        return Vector3.Angle(enemy.transform.forward, noiseDirection);
    }
}
