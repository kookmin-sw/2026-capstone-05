using UnityEngine;

/// <summary>
/// 적의 사망을 EnemyDeathManager에게 알리는 컴포넌트
/// Enemy 오브젝트에 부착하여 사용
/// </summary>
public class EnemyDeathNotifier : MonoBehaviour
{
    /// <summary>
    /// 사망 이벤트를 EnemyDeathManager에게 알림
    /// UnityEvent에서 직접 호출 가능
    /// </summary>
    public void NotifyDeath()
    {
        // EnemyDeathManager의 정적 메서드를 호출하여 사망 이벤트 발생
        EnemyDeathManager.NotifyEnemyDeathStatic();
    }
}