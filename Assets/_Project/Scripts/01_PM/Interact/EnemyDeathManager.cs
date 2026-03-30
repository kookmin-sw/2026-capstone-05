using System;
using UnityEngine;

/// <summary>
/// 적 사망 이벤트를 관리하는 매니저
/// </summary>
public class EnemyDeathManager : MonoBehaviour
{
    public static EnemyDeathManager Instance { get; private set; }
    
    // 적이 죽을 때 발생하는 이벤트
    public static event Action OnEnemyDied;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 적이 죽었을 때 호출되는 메서드 (UnityEvent에서 호출 가능)
    /// </summary>
    public void NotifyEnemyDeath()
    {
        OnEnemyDied?.Invoke();
    }
    
    /// <summary>
    /// 정적 메서드로도 접근 가능
    /// </summary>
    public static void NotifyEnemyDeathStatic()
    {
        if (Instance != null)
        {
            Instance.NotifyEnemyDeath();
        }
    }
}