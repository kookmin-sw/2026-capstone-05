using Fusion;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(NetworkObject))]
public class EnemyHealth : NetworkBehaviour, IDamageable
{
    [Header("Hit Effects")]
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private float hitParticleLifetimeFallback = 5f;
    [SerializeField] private int hitParticlePoolDefaultCapacity = 10;
    [SerializeField] private int hitParticlePoolMaxSize = 20;

    private EnemyAI enemy;
    private float localCurrentHealth;
    private ObjectPool<GameObject> hitParticlePool;

    [Networked] private float NetworkCurrentHealth { get; set; }

    public event Action<EnemyHealth> Died;
    public static event Action<EnemyHealth, GameObject> AnyDied;

    public float CurrentHealth => enemy != null && enemy.IsLocalSimulationActive
        ? localCurrentHealth
        : NetworkCurrentHealth;

    private void Awake()
    {
        enemy = GetComponent<EnemyAI>();
        InitializeHitParticlePool();
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetworkCurrentHealth = enemy.Data.maxHealth;
        }
    }

    public void InitializeLocalHealth()
    {
        enemy = enemy != null ? enemy : GetComponent<EnemyAI>();
        if (enemy == null || enemy.Data == null)
            return;

        localCurrentHealth = enemy.Data.maxHealth;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (enemy != null && enemy.IsLocalSimulationActive)
        {
            TakeLocalDamage(info);
            return;
        }

        if (!HasStateAuthority || NetworkCurrentHealth <= 0f) return;

        float damage = Mathf.Min(info.damageAmount, NetworkCurrentHealth);
        SpawnHitParticleForNetwork(info);
        NetworkCurrentHealth -= damage;

        if (NetworkCurrentHealth <= 0f)
        {
            NetworkCurrentHealth = 0f;
            Died?.Invoke(this);
            NotifyAnyDiedForNetwork(info);
            enemy.StateMachine.ChangeState(enemy.DeadState);
            return;
        }

        enemy.RegisterHitReaction();
    }

    public void RequestDamage(DamageInfo info)
    {
        if (enemy != null && enemy.IsLocalSimulationActive)
        {
            TakeLocalDamage(info);
            return;
        }

        if (HasStateAuthority || !Runner || Object == null || !Object.IsValid)
        {
            TakeDamage(info);
            return;
        }

        RpcRequestDamage(info.damageAmount, info.hitPoint, info.hitNormal);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcRequestDamage(float damageAmount, Vector3 hitPoint, Vector3 hitNormal, RpcInfo rpcInfo = default)
    {
        TakeDamage(new DamageInfo
        {
            damageAmount = damageAmount,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            attacker = ResolveAttacker(rpcInfo.Source)
        });
    }

    private void TakeLocalDamage(DamageInfo info)
    {
        if (localCurrentHealth <= 0f) return;

        float damage = Mathf.Min(info.damageAmount, localCurrentHealth);
        SpawnHitParticle(info.hitPoint, info.hitNormal);
        localCurrentHealth -= damage;

        if (localCurrentHealth <= 0f)
        {
            localCurrentHealth = 0f;
            Died?.Invoke(this);
            AnyDied?.Invoke(this, info.attacker);
            enemy.StateMachine.ChangeState(enemy.DeadState);
            return;
        }

        enemy.RegisterHitReaction();
    }

    private void NotifyAnyDiedForNetwork(DamageInfo info)
    {
        if (!Runner || Object == null || !Object.IsValid)
        {
            AnyDied?.Invoke(this, info.attacker);
            return;
        }

        RpcNotifyAnyDied(ResolveAttackerPlayerRef(info.attacker));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcNotifyAnyDied(PlayerRef attackerRef)
    {
        AnyDied?.Invoke(this, ResolveAttacker(attackerRef));
    }

    private GameObject ResolveAttacker(PlayerRef playerRef)
    {
        if (Runner != null &&
            playerRef != PlayerRef.None &&
            Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) &&
            playerObject != null)
        {
            return playerObject.gameObject;
        }

        return null;
    }

    private PlayerRef ResolveAttackerPlayerRef(GameObject attacker)
    {
        if (attacker == null)
        {
            return PlayerRef.None;
        }

        NetworkObject attackerNetworkObject = attacker.GetComponent<NetworkObject>()
            ?? attacker.GetComponentInParent<NetworkObject>()
            ?? attacker.GetComponentInChildren<NetworkObject>();

        return attackerNetworkObject != null ? attackerNetworkObject.InputAuthority : PlayerRef.None;
    }

    private void SpawnHitParticleForNetwork(DamageInfo info)
    {
        if (!Runner || !Object || !Object.IsValid)
        {
            SpawnHitParticle(info.hitPoint, info.hitNormal);
            return;
        }

        RpcSpawnHitParticle(info.hitPoint, info.hitNormal);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSpawnHitParticle(Vector3 hitPoint, Vector3 hitNormal)
    {
        SpawnHitParticle(hitPoint, hitNormal);
    }

    private void SpawnHitParticle(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitParticlePool == null)
            return;

        Vector3 spawnPosition = hitPoint;
        if (!IsFinite(spawnPosition))
        {
            spawnPosition = transform.position;
        }

        Quaternion spawnRotation = transform.rotation;
        if (IsFinite(hitNormal) && hitNormal.sqrMagnitude > 0.0001f)
        {
            spawnRotation = Quaternion.LookRotation(hitNormal.normalized);
        }

        GameObject particle = hitParticlePool.Get();
        particle.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        ParticleSystem[] particleSystems = particle.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        StartCoroutine(ReturnHitParticleToPoolAfterDelay(particle, GetParticleLifetime(particleSystems)));
    }

    private void InitializeHitParticlePool()
    {
        if (hitParticlePrefab == null)
            return;

        hitParticlePool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(hitParticlePrefab, transform),
            actionOnGet: obj => obj.SetActive(true),
            actionOnRelease: obj => obj.SetActive(false),
            actionOnDestroy: obj => Destroy(obj),
            collectionCheck: false,
            defaultCapacity: Mathf.Max(0, hitParticlePoolDefaultCapacity),
            maxSize: Mathf.Max(1, hitParticlePoolMaxSize));
    }

    private IEnumerator ReturnHitParticleToPoolAfterDelay(GameObject particle, float delay)
    {
        yield return new WaitForSeconds(delay);
        hitParticlePool?.Release(particle);
    }

    private float GetParticleLifetime(ParticleSystem[] particleSystems)
    {
        float lifetime = 0f;
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
        }

        return lifetime > 0f ? lifetime : Mathf.Max(0.1f, hitParticleLifetimeFallback);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
