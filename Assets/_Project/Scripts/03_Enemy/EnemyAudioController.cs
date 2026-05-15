using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public enum EnemySoundCue : byte
{
    Locomotion = 0,
    Alert = 1,
    Chase = 2,
    Search = 3,
    Attack = 4,
    Hit = 5,
    Dead = 6
}

public class EnemyAudioController : MonoBehaviour
{
    [Header("Audio Toggle")]
    [SerializeField] private bool enableAudio = true;

    [Header("FMOD Events")]
    [SerializeField] private EventReference locomotionEvent;
    [SerializeField] private EventReference alertEvent;
    [SerializeField] private EventReference chaseEvent;
    [SerializeField] private EventReference searchEvent;
    [SerializeField] private EventReference attackEvent;
    [SerializeField] private EventReference hitEvent;
    [SerializeField] private EventReference deadEvent;

    [Header("Playback Guards")]
    [SerializeField, Min(0f)] private float locomotionCooldown = 2f;
    [SerializeField, Min(0f)] private float stateSoundCooldown = 0.1f;

    private readonly float[] lastPlayTimes = new float[Enum.GetValues(typeof(EnemySoundCue)).Length];

    private void Awake()
    {
        for (int i = 0; i < lastPlayTimes.Length; i++)
        {
            lastPlayTimes[i] = float.NegativeInfinity;
        }
    }

    public void Play(EnemySoundCue cue)
    {
        if (!enableAudio)
            return;

        EventReference eventReference = GetEvent(cue);
        if (eventReference.IsNull)
            return;

        int cueIndex = (int)cue;
        if (cueIndex < 0 || cueIndex >= lastPlayTimes.Length)
            return;

        float cooldown = cue == EnemySoundCue.Locomotion ? locomotionCooldown : stateSoundCooldown;
        if (Time.time < lastPlayTimes[cueIndex] + cooldown)
            return;

        lastPlayTimes[cueIndex] = Time.time;

        EventInstance instance = RuntimeManager.CreateInstance(eventReference);
        RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
        instance.start();
        instance.release();
    }

    public void SetAudioEnabled(bool isEnabled)
    {
        enableAudio = isEnabled;
    }

    public bool IsAudioEnabled()
    {
        return enableAudio;
    }

    private EventReference GetEvent(EnemySoundCue cue)
    {
        switch (cue)
        {
            case EnemySoundCue.Locomotion:
                return locomotionEvent;
            case EnemySoundCue.Alert:
                return alertEvent;
            case EnemySoundCue.Chase:
                return chaseEvent;
            case EnemySoundCue.Search:
                return searchEvent;
            case EnemySoundCue.Attack:
                return attackEvent;
            case EnemySoundCue.Hit:
                return hitEvent;
            case EnemySoundCue.Dead:
                return deadEvent;
            default:
                return default;
        }
    }
}
