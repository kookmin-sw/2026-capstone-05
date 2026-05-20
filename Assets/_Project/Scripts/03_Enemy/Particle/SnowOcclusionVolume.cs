using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class SnowOcclusionVolume : MonoBehaviour
{
    public static event Action SnowMaskVolumesChanged;
    public static IReadOnlyList<Collider> SnowMaskColliders => snowMaskColliders;

    [SerializeField, FormerlySerializedAs("blockSnow")] private bool killSnowParticlesInside = true;
    [SerializeField] private bool includeChildColliders = true;

    private static readonly HashSet<SnowOcclusionVolume> activeVolumes = new();
    private static readonly List<Collider> snowMaskColliders = new();
    private Collider[] volumeColliders = Array.Empty<Collider>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        SnowMaskVolumesChanged = null;
        activeVolumes.Clear();
        snowMaskColliders.Clear();
    }

    private void OnEnable()
    {
        CacheVolumeColliders();
        activeVolumes.Add(this);
        RebuildSnowMaskColliders();
    }

    private void Reset()
    {
        CacheVolumeColliders();
        ForceTriggerColliders();
    }

    private void OnValidate()
    {
        CacheVolumeColliders();
        ForceTriggerColliders();

        if (Application.isPlaying && isActiveAndEnabled)
        {
            RebuildSnowMaskColliders();
        }
    }

    private void OnDisable()
    {
        activeVolumes.Remove(this);
        RebuildSnowMaskColliders();
    }

    private void OnTransformChildrenChanged()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        CacheVolumeColliders();
        ForceTriggerColliders();
        RebuildSnowMaskColliders();
    }

    private static void RebuildSnowMaskColliders()
    {
        snowMaskColliders.Clear();

        foreach (SnowOcclusionVolume volume in activeVolumes)
        {
            if (volume == null || !volume.killSnowParticlesInside)
            {
                continue;
            }

            volume.AddMaskColliders(snowMaskColliders);
        }

        SnowMaskVolumesChanged?.Invoke();
    }

    private void CacheVolumeColliders()
    {
        volumeColliders = includeChildColliders
            ? GetComponentsInChildren<Collider>(true)
            : GetComponents<Collider>();
    }

    private void ForceTriggerColliders()
    {
        for (int i = 0; i < volumeColliders.Length; i++)
        {
            if (volumeColliders[i] != null)
            {
                volumeColliders[i].isTrigger = true;
            }
        }
    }

    private void AddMaskColliders(List<Collider> colliders)
    {
        for (int i = 0; i < volumeColliders.Length; i++)
        {
            Collider maskCollider = volumeColliders[i];
            if (maskCollider != null && maskCollider.enabled && maskCollider.gameObject.activeInHierarchy)
            {
                colliders.Add(maskCollider);
            }
        }
    }
}
