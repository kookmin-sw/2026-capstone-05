using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NoiseData", menuName = "Noise Data")]
public class NoiseData : ScriptableObject
{
    public enum NoiseType
    {
        // Movement
        Idle, Crouch, Walk, Sprint, Jump, Fall,
        // Combat
        MeleeSwing, MeleeHit, GunShot, GunReload,
        // Interaction
        ItemPickup, ItemDrop, Door, Container, Box,
        // Status
        Eat, Drink, Hunger, Cough, Pain,
        UnarmedSwing, UnarmedHit,
        Throw, Explosion, Alarm, ThrowImpact
    }

    [System.Serializable]
    public struct NoiseInfo
    {
        public NoiseType type;
        public float decibel;
    }
    public List<NoiseInfo> noiseInfo = new List<NoiseInfo>();
}