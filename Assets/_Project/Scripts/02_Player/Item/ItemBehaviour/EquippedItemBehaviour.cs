using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 플레이어 손에 장착되는 모든 아이템 프리팹의 최상위 부모 클래스
/// </summary>
public abstract class EquippedItemBehaviour : MonoBehaviour
{
    private Renderer[] renderers;

    protected PlayerController player;
    protected ItemInstance itemInstance;

    protected float lastUseTime = -999f;

    protected CinemachineImpulseSource impulseSource;

    public bool Is1PModel { get; private set; }

    public virtual void Initialize(PlayerController owner, ItemInstance instance, bool is1P)
    {
        player = owner;
        itemInstance = instance;
        Is1PModel = is1P;
        renderers = GetComponentsInChildren<Renderer>(true);

        if (player.IsLocalPlayer)
        {
            if (Is1PModel)
            {
                SetRenderersShadowCastingMode(UnityEngine.Rendering.ShadowCastingMode.Off);
            }
            else
            {
                SetRenderersShadowCastingMode(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
            }
        }
        else if (!Is1PModel)
        {
            SetRenderersShadowCastingMode(UnityEngine.Rendering.ShadowCastingMode.On);
        }

        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    /// <summary>
    /// 좌클릭(Primary Use) 시 호출되는 메서드.
    /// </summary>
    public virtual void Use()
    {
        if (Time.time - lastUseTime < itemInstance.Data.actionCooldown)
        {
            return;
        }

        lastUseTime = Time.time;

        player.Animator.PlayUseItemAnimation(itemInstance.Data.useAnimationType);
    }

    /// <summary>
    /// 우클릭(Secondary Use) 시 호출되는 메서드. (예: 조준, 특수 능력 등) (현재 구현 X)
    /// </summary>
    public virtual void SecondaryUse() { }

    /// <summary>
    /// 애니메이션의 특정 프레임(타격 순간 등)에서 호출됩니다.
    /// </summary>
    public virtual void OnAnimationEventTriggered() { }


    private void SetRenderersShadowCastingMode(UnityEngine.Rendering.ShadowCastingMode mode)
    {
        foreach (Renderer rend in renderers)
        {
            if (rend != null)
            {
                rend.shadowCastingMode = mode;
            }
        }
    }
}