using FMOD.Studio;
using FMODUnity;
using Unity.Cinemachine;
using UnityEngine;

public class ThrownDynamite : ThrownItem
{
    [Header("Explosion Settings")]
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private float explosionDamageRadius = 3f;
    [SerializeField] private float explosionDamage = 50f;

    [Header("Sound Settings")]
    [SerializeField] private EventReference fuseSoundEvent;
    [SerializeField] private EventReference explosionSoundEvent;

    private EventInstance fuseSoundInstance;
    private CinemachineImpulseSource impulseSource;

    public override void Initialize(ItemInstance instance)
    {
        base.Initialize(instance);

        fuseSoundInstance = RuntimeManager.CreateInstance(fuseSoundEvent);
        RuntimeManager.AttachInstanceToGameObject(fuseSoundInstance, gameObject);
        fuseSoundInstance.start();

        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    protected override void OnLifetimeExpired()
    {
        if (fuseSoundInstance.isValid())
        {
            fuseSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            fuseSoundInstance.release();
        }

        Explode();

        base.OnLifetimeExpired();
    }

    private void Explode()
    {
        RuntimeManager.PlayOneShot(explosionSoundEvent, transform.position);
        NoiseManager.Instance.GenerateNoise(transform.position, NoiseData.NoiseType.Explosion);

        if (explosionVFXPrefab != null)
        {
            // TODO: Object pooling for better performance
            GameObject vfx = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, vfx.GetComponent<ParticleSystem>().main.duration);
        }

        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionDamageRadius);
        foreach (Collider hit in hitColliders)
        {
            if (hit.TryGetComponent(out IDamageable damageable))
            {
                DamageInfo damageInfo = new DamageInfo
                {
                    damageAmount = explosionDamage,
                    hitPoint = hit.ClosestPoint(transform.position),
                    hitNormal = (hit.transform.position - transform.position).normalized,
                    attacker = owner != null ? owner : gameObject
                };
                damageable.TakeDamage(damageInfo);
            }
        }
    }
}
