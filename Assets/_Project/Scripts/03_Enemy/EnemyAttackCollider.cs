using UnityEngine;

public class EnemyAttackCollider : MonoBehaviour
{
    [SerializeField] private int colliderIndex = 0;

    public int ColliderIndex => colliderIndex;

    private EnemyAI enemy;
    private Collider attackCollider;
    private bool canDamage = false;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyAI>();
        attackCollider = GetComponent<Collider>();
        attackCollider.isTrigger = true;
        attackCollider.enabled = false;
    }

    public void EnableAttackCollider()
    {
        canDamage = true;
        attackCollider.enabled = true;
    }

    public void DisableAttackCollider()
    {
        canDamage = false;
        attackCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canDamage) return;

        if (other.CompareTag("Player") && other.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(enemy.Data.attackDamage);
            canDamage = false;
        }
    }
}
