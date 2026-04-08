using UnityEngine;
using Unity.Cinemachine;

public class PlayerHitFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    private CinemachineImpulseSource impulseSource;

    [Header("Camera Shake Settings")]
    [Tooltip("데미지 1당 흔들리는 강도 비율")]
    public float shakeMultiplier = 0.05f;
    public float maxShakeForce = 3.0f;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Start()
    {
        if (player != null && player.Condition != null)
        {
            player.Condition.OnTakeDamageEvent += HandleTakeDamage;
        }
    }

    private void OnDestroy()
    {
        if (player != null && player.Condition != null)
        {
            player.Condition.OnTakeDamageEvent -= HandleTakeDamage;
        }
    }

    /// <summary>
    /// 플레이어가 데미지를 입을 때마다 호출
    /// </summary>
    private void HandleTakeDamage(float damageAmount)
    {
        if (!player.IsLocalPlayer || impulseSource == null)
        {
            return;
        }

        float force = Mathf.Clamp(damageAmount * shakeMultiplier, 0.1f, maxShakeForce);

        Vector3 randomHitDirection = new Vector3(
            Random.Range(-1f, 1f),
            -1f,
            Random.Range(-1f, 1f)
        ).normalized;

        impulseSource.GenerateImpulseWithVelocity(randomHitDirection * force);

        // TODO: 피격 사운드, UI 피드백 등
    }
}