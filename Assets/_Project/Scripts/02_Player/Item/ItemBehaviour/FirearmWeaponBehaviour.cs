using UnityEngine;

public class FirearmWeaponBehaviour : EquippedItemBehaviour
{
    [SerializeField] private Transform muzzlePoint; // 총구 위치

    public override bool Use()
    {
        FirearmItemData data = itemInstance.Data as FirearmItemData;
        if (data == null)
        {
            return false;
        }

        // 총알 확인

        if (!base.Use())
        {
            return false;
        }

        Shoot(data);

        player.AddCameraRecoil(data.recoilForce, new Vector3(0, 0, -0.05f), new Vector3(-5f, 0, 0));

        return true;
    }

    private void Shoot(FirearmItemData data)
    {
        // 총알 감소 로직

        //if (data.muzzleFlashPrefab != null && muzzlePoint != null)
        //{
        //    Instantiate(data.muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
        //}

        Transform originTransform = player.CameraTransform != null ? player.CameraTransform : Camera.main.transform;
        Ray ray = new Ray(originTransform.position, originTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, data.attackRange, player.Equipment.hitLayerMask))
        {
            Debug.Log($"Hit: {hit.collider.name}");
            if (hit.collider.TryGetComponent(out IDamageable target))
            {
                target.TakeDamage(data.damage);
            }

            // TODO: 피격 이펙트(피, 불꽃, 흙먼지 등) 스폰
        }

        NoiseManager.Instance.GenerateNoise(transform.position, data.shootNoiseType);
    }

    /// <summary>
    /// R키 등을 눌렀을 때 PlayerEquipment 등에서 호출
    /// </summary>
    public void Reload()
    {
        // 이미 꽉 찼거나 인벤토리에 여유 탄약이 없으면 Return하는 로직 필요
        //player.Animator.PlayUseItemAnimation(ItemUseAnimationType.Reload); // 장전 애니메이션
    }

    /// <summary>
    /// 장전 애니메이션 도중 탄창이 결합되는(찰칵) 프레임에서 호출됨
    /// </summary>
    public override void OnAnimationEventTriggered()
    {
        FirearmItemData data = itemInstance.Data as FirearmItemData;
        if (data == null)
        {
            return;
        }

        // 탄약 보충 로직 (인벤토리에서 총알을 빼서 currentAmmo에 넣기)
        // itemInstance.currentAmmo = data.maxAmmo; 

        Debug.Log("Reload Complete!");
    }
}