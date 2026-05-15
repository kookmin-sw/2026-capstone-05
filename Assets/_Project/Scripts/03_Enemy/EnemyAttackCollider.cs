using UnityEngine;

public class EnemyAttackCollider : MonoBehaviour
{
    [SerializeField] private int colliderIndex = 0;
    [SerializeField] private bool isBodyCollider = false;

    public int ColliderIndex => colliderIndex;

    private EnemyAI enemy;
    private Collider attackCollider;
    private bool canDamage = false;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyAI>();
        attackCollider = GetComponent<Collider>();
        attackCollider.isTrigger = !isBodyCollider;
        attackCollider.enabled = isBodyCollider;
    }

    public void EnableAttackCollider()
    {
        if (isBodyCollider) return;
        canDamage = true;
        attackCollider.enabled = true;
    }

    public void DisableAttackCollider()
    {
        if (isBodyCollider) return;
        canDamage = false;
        attackCollider.enabled = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isBodyCollider) return;
        if (!collision.collider.CompareTag("Player")) return;
        if (!collision.collider.TryGetComponent(out IDamageable damageable)) return;

        DamageInfo damageInfo = new DamageInfo
        {
            damageAmount = enemy.Data.attackDamage,
            hitPoint = collision.GetContact(0).point,
            hitNormal = collision.GetContact(0).normal,
            attacker = enemy.gameObject
        };
        damageable.TakeDamage(damageInfo);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBodyCollider) return;
        if (!canDamage) return;
        if (!other.CompareTag("Player")) return;
        if (!other.TryGetComponent(out IDamageable damageable)) return;

        DamageInfo damageInfo = new DamageInfo
        {
            damageAmount = enemy.Data.attackDamage,
            hitPoint = other.ClosestPoint(transform.position),
            hitNormal = (other.transform.position - transform.position).normalized,
            attacker = enemy.gameObject
        };
        damageable.TakeDamage(damageInfo);
        canDamage = false;
    }
}
