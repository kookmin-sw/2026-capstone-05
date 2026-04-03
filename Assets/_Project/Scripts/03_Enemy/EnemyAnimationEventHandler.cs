using UnityEngine;

public class EnemyAnimationEventHandler : MonoBehaviour
{
    public event System.Action<int> OnAttackStart;
    public event System.Action<int> OnAttackEnd;
    public event System.Action OnFinalAttackEnd;
    public event System.Action OnHitEnd;
    public event System.Action OnDeadEnd;
    public event System.Action OnTurnEnd;
    public event System.Action OnTurnREnd;

    public void AttackStart(int index) => OnAttackStart?.Invoke(index);
    public void AttackEnd(int index) => OnAttackEnd?.Invoke(index);
    public void FinalAttackEnd() => OnFinalAttackEnd?.Invoke();
    public void HitEnd() => OnHitEnd?.Invoke();
    public void DeadEnd() => OnDeadEnd?.Invoke();
    public void TurnEnd() => OnTurnEnd?.Invoke();
    public void TurnREnd() => OnTurnREnd?.Invoke();
}