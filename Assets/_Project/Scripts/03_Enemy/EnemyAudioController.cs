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
    Dead = 6,
    Footstep = 7,
    Landing = 8
}

public class EnemyAudioController : MonoBehaviour
{
    private const string EnemyMovementTypeParameter = "EnemyMovementType";

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
    [SerializeField] private EventReference footstepEvent;
    [SerializeField] private EventReference landingEvent;

    [Header("Playback Guards")]
    [SerializeField, Min(0f)] private float locomotionCooldown = 2f;
    [SerializeField, Min(0f)] private float footstepCooldown = 0.05f;
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

        float cooldown = GetCooldown(cue);
        if (Time.time < lastPlayTimes[cueIndex] + cooldown)
            return;

        lastPlayTimes[cueIndex] = Time.time;

        EventInstance instance = RuntimeManager.CreateInstance(eventReference);
        RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
        instance.start();
        instance.release();
    }

    public void PlayFootstep(float movementType)
    {
        if (!enableAudio)
            return;

        if (footstepEvent.IsNull)
            return;

        int cueIndex = (int)EnemySoundCue.Footstep;
        if (Time.time < lastPlayTimes[cueIndex] + footstepCooldown)
            return;

        lastPlayTimes[cueIndex] = Time.time;

        EventInstance instance = RuntimeManager.CreateInstance(footstepEvent);
        RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
        instance.setParameterByName(EnemyMovementTypeParameter, movementType);
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
            case EnemySoundCue.Footstep:
                return footstepEvent;
            case EnemySoundCue.Landing:
                return landingEvent;
            default:
                return default;
        }
    }

    private float GetCooldown(EnemySoundCue cue)
    {
        switch (cue)
        {
            case EnemySoundCue.Locomotion:
                return locomotionCooldown;
            case EnemySoundCue.Footstep:
                return footstepCooldown;
            default:
                return stateSoundCooldown;
        }
    }
}
