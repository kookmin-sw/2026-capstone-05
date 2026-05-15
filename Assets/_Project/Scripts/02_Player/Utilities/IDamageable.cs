using UnityEngine;

public struct DamageInfo
{
    public float damageAmount;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public GameObject attacker;
    public DamageInfo(float damageAmount, Vector3 hitPoint, Vector3 hitNormal, GameObject attacker)
    {
        this.damageAmount = damageAmount;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;
        this.attacker = attacker;
    }
}

public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
