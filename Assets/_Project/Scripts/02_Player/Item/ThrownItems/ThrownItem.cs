using UnityEngine;

/// <summary>
/// 던져지는 아이템 프리팹의 최상위 부모 클래스
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class ThrownItem : MonoBehaviour
{
    protected ItemInstance itemInstance;

    [Header("Thrown Item Settings")]
    [SerializeField] protected float lifeTime = 5f;

    protected float currentLifeTimer = 0f;
    protected bool isTriggered = false;

    public virtual void Initialize(ItemInstance instance)
    {
        itemInstance = instance;
        currentLifeTimer = 0f;
        isTriggered = false;
    }

    protected virtual void Update()
    {
        if (isTriggered)
        {
            return;
        }

        currentLifeTimer += Time.deltaTime;
        if (currentLifeTimer >= lifeTime)
        {
            OnLifetimeExpired();
        }
    }

    protected virtual void OnCollisionEnter(Collision collision) { }

    /// <summary>
    /// 수명이 다했을 때 실행되는 함수 (기본값: 조용히 파괴)
    /// </summary>
    protected virtual void OnLifetimeExpired()
    {
        DestroyProjectile();
    }

    /// <summary>
    /// 실제 오브젝트를 파괴하는 최종 함수
    /// </summary>
    protected virtual void DestroyProjectile()
    {
        if (isTriggered)
        {
            return;
        }
        isTriggered = true;

        // TODO: 나중에 오브젝트 풀(Object Pool)을 쓰게 되면 여기서 Destroy 대신 Release 처리
        Destroy(gameObject);
    }
}
