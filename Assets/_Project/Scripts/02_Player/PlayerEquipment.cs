using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform handSocket1P;

    public EquippedItemBehaviour CurrentItem { get; private set; }

    private Transform cameraTransform;

    [Header("Unarmed Settings")]
    public float unarmedDamage = 5f;
    public float unarmedAttackCooldown = 0.8f;
    public float unarmedRange = 1f;
    public float unarmedHitRadius = 0.3f;

    public LayerMask hitLayerMask = ~0;

    private float lastUnarmedAttackTime = 0f;

    private void Start()
    {
        cameraTransform = player.CameraTransform;
    }

    /// <summary>
    /// 인벤토리(퀵슬롯)에서 아이템을 선택했을 때 호출됩니다.
    /// </summary>
    public void EquipItem(ItemInstance itemInstance)
    {
        if (CurrentItem != null)
        {
            Destroy(CurrentItem.gameObject);
        }

        if (itemInstance.Data.equipPrefab == null)
        {
            Debug.Log("이 아이템은 손에 들 수 없습니다.");
            return;
        }

        GameObject spawnedItem = Instantiate(itemInstance.Data.equipPrefab, handSocket1P);

        CurrentItem = spawnedItem.GetComponent<EquippedItemBehaviour>();
        if (CurrentItem != null)
        {
            CurrentItem.Initialize(player, itemInstance);
        }
    }

    /// <summary>
    /// PlayerInputHandler에서 좌클릭(Action)을 감지했을 때 호출됩니다.
    /// </summary>
    public void UseCurrentItem()
    {
        if (CurrentItem != null)
        {
            CurrentItem.Use();
        }
        else
        {
            if (Time.time - lastUnarmedAttackTime >= unarmedAttackCooldown)
            {
                lastUnarmedAttackTime = Time.time;
                player.Animator.PlayUseItemAnimation(ItemUseAnimationType.None);
            }
        }
    }

    /// <summary>
    /// PlayerAnimator에서 애니메이션 이벤트가 발생했을 때 호출됩니다.
    /// </summary>
    public void HandleAnimationEvent()
    {
        if (CurrentItem != null)
        {
            CurrentItem.OnAnimationEventTriggered();
        }
        else
        {
            PerformUnarmedHitCheck();
        }
    }

    private void PerformUnarmedHitCheck()
    {
        Transform originTransform = cameraTransform != null ? cameraTransform : Camera.main.transform;

        Ray ray = new Ray(originTransform.position, originTransform.forward);
        if (Physics.SphereCast(ray, unarmedHitRadius, out RaycastHit hit, unarmedRange, hitLayerMask))
        {
            if (hit.collider.TryGetComponent(out IDamageable target))
            {
                // 대미지 넣었을 때
                target.TakeDamage(unarmedDamage);

                // TODO: 타격음 발생
                // NoiseManager.Instance.GenerateNoise(hit.point, NoiseData.NoiseType.PunchHit);
            }
            else
            {
                // 뭔가 맞긴 했는데 대미지를 줄 수 없는 물체였을 때
                // TODO: 타격음 발생
            }
        }
        else
        {
            // TODO: 헛스윙 소리?
        }
    }

    //private void OnDrawGizmosSelected()
    //{
    //    Transform originTransform = cameraTransform != null ? cameraTransform : Camera.main?.transform;
    //    if (originTransform == null)
    //    {
    //        return;
    //    }

    //    Gizmos.color = Color.red;
    //    Vector3 startPos = originTransform.position;
    //    Vector3 endPos = startPos + originTransform.forward * unarmedRange;

    //    Gizmos.DrawWireSphere(startPos, unarmedHitRadius);
    //    Gizmos.DrawWireSphere(endPos, unarmedHitRadius);
    //    Gizmos.DrawLine(startPos, endPos);
    //}
}