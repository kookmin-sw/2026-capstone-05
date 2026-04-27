using UnityEngine;

/// <summary>
/// 단일 스포너 위치를 관리하는 스포너.
/// 스폰 포인트를 비워두면 Spawner 자신의 Transform을 사용한다.
/// </summary>
public class Spawner : MonoBehaviour
{
    [Header("Single Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    // 다른 스크립트(예: PlayerRespawn)에서 쉽게 접근하기 위한 싱글톤
    public static Spawner Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        if (Instance != this)
        {
            Debug.LogWarning("[Spawner] Spawner는 하나만 존재해야 합니다. 중복 Spawner를 제거합니다.");
            Destroy(gameObject);
        }
    }

    public Transform GetSpawnPoint()
    {
        return spawnPoint != null ? spawnPoint : transform;
    }
}
