using UnityEngine;
using UnityEngine.AI;

public class EnemySearchState : EnemyState
{
    private enum SearchPhase { Approaching, Searching }

    private SearchPhase phase;
    private Vector3 searchCenter;
    private Vector3 lastNoisePosition;
    private float waitTimer;
    private bool isWaiting;
    private bool isLeftTurn;
    private bool isTurnPlaying;
    private bool moveAfterTurn;

    public EnemySearchState(EnemyAI enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine) { }

    public override void Enter()
    {
        searchCenter = enemy.DetectedNoisePosition;
        lastNoisePosition = searchCenter;
        enemy.SetPatrolCenter(searchCenter);

        enemy.Agent.speed = enemy.Data.searchSpeed;
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;

        phase = SearchPhase.Approaching;
        isWaiting = false;

        SetApproachDestination();
    }

    public override void Exit()
    {
        StopWaitTurn();
        enemy.Animator.CrossFade("Locomotion", 0.2f);
        enemy.Agent.isStopped = false;
        enemy.Agent.updateRotation = true;
    }

    public override void LogicUpdate()
    {
        enemy.Animator.SetFloat("Speed", enemy.Agent.velocity.magnitude / enemy.Data.searchSpeed, 0.2f, Time.deltaTime);
        enemy.Animator.SetFloat("Angle", 0f, 0.2f, Time.deltaTime);

        if (enemy.IsPlayerInAttackRadius())
        {
            stateMachine.ChangeState(enemy.AttackState);
            return;
        }

        if (enemy.DetectedNoisePosition != lastNoisePosition)
        {
            stateMachine.ChangeState(enemy.ChaseState);
            return;
        }

        if (enemy.Suspicion < enemy.Data.searchExitThreshold)
        {
            stateMachine.ChangeState(enemy.AlertState);
            return;
        }

        if (phase == SearchPhase.Approaching)
            UpdateApproachPhase();
        else
            UpdateSearchPhase();
    }

    private void UpdateApproachPhase()
    {
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            phase = SearchPhase.Searching;
            SetSearchDestination();
        }
    }

    private void UpdateSearchPhase()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f && !moveAfterTurn)
            {
                if (isTurnPlaying)
                {
                    moveAfterTurn = true;
                }
                else
                {
                    isWaiting = false;
                    enemy.Agent.isStopped = false;
                    enemy.Agent.updateRotation = true;
                    SetSearchDestination();
                }
            }
            return;
        }

        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance)
        {
            isWaiting = true;
            waitTimer = Random.Range(enemy.Data.waitStartTime, enemy.Data.waitEndTime);
            enemy.Agent.isStopped = true;
            enemy.Agent.updateRotation = false;
            StartWaitTurn();
        }
    }

    private void StartWaitTurn()
    {
        enemy.AnimationEventHandler.OnTurnEnd += HandleTurnEnd;
        isTurnPlaying = true;
        moveAfterTurn = false;
        isLeftTurn = Random.value > 0.5f;
        string turnTrigger = isLeftTurn ? "TurnLeft" : "TurnRight";
        enemy.Animator.SetTrigger(turnTrigger);
        enemy.NotifyAnimatorTrigger(turnTrigger);
    }

    private void StopWaitTurn()
    {
        enemy.AnimationEventHandler.OnTurnEnd -= HandleTurnEnd;
        enemy.Animator.ResetTrigger("TurnLeft");
        enemy.Animator.ResetTrigger("TurnRight");
        isTurnPlaying = false;
        moveAfterTurn = false;
    }

    private void SetApproachDestination()
    {
        Vector3 targetDirection = searchCenter - enemy.transform.position;
        targetDirection.y = 0f;

        float approachOffset = Random.Range(1.5f, 3f);
        float approachDist = Mathf.Max(0f, targetDirection.magnitude - approachOffset);
        Vector3 approachPos = enemy.transform.position + targetDirection.normalized * approachDist;

        const float navMeshSampleRange = 2f;
        if (NavMesh.SamplePosition(approachPos, out NavMeshHit hit, navMeshSampleRange, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(hit.position);
        }
        else
        {
            phase = SearchPhase.Searching;
            SetSearchDestination();
        }
    }

    private void SetSearchDestination()
    {
        const int maxAttempts = 5;
        const float navMeshSampleRange = 2f;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * enemy.Data.searchRadius;
            Vector3 targetPos = searchCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, navMeshSampleRange, NavMesh.AllAreas))
            {
                enemy.Agent.SetDestination(hit.position);
                return;
            }
        }

        if (NavMesh.SamplePosition(searchCenter, out NavMeshHit fallback, navMeshSampleRange, NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(fallback.position);
        }
    }

    private void HandleTurnEnd()
    {
        enemy.AnimationEventHandler.OnTurnEnd -= HandleTurnEnd;
        isTurnPlaying = false;
        enemy.Animator.CrossFade("Locomotion", 0.2f);

        if (moveAfterTurn)
        {
            moveAfterTurn = false;
            isWaiting = false;
            enemy.Agent.isStopped = false;
            enemy.Agent.updateRotation = true;
            SetSearchDestination();
        }
    }
}
