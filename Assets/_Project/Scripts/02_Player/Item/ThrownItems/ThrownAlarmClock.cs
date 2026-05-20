using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
using System.Collections;
using UnityEngine;

public class ThrownAlarmClock : ThrownItem
{
    [SerializeField] private float alarmWaitTime = 3f;

    [Header("Visual Effects")]
    [SerializeField] private Transform visualModel;

    [Header("Sound Settings")]
    [SerializeField] private EventReference alarmSoundEvent;

    private bool isAlarmActive = false;
    private float alarmTimer = 0f;

    private EventInstance alarmSoundInstance;

    public override void Initialize(ItemInstance instance)
    {
        base.Initialize(instance);
        isAlarmActive = false;
        alarmTimer = 0f;
    }

    protected override void OnLifetimeExpired()
    {
        if (alarmSoundInstance.isValid())
        {
            alarmSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            alarmSoundInstance.release();
        }

        if (visualModel != null)
        {
            visualModel.DOKill();
        }

        base.OnLifetimeExpired();
    }

    protected override void Update()
    {
        base.Update();

        if (!isAlarmActive)
        {
            alarmTimer += Time.deltaTime;
            if (alarmTimer >= alarmWaitTime)
            {
                ActivateAlarm();
            }
        }
    }

    private void ActivateAlarm()
    {
        isAlarmActive = true;

        StartCoroutine(GenerateAlarmNoise());

        alarmSoundInstance = RuntimeManager.CreateInstance(alarmSoundEvent);
        RuntimeManager.AttachInstanceToGameObject(alarmSoundInstance, gameObject);
        alarmSoundInstance.start();

        if (visualModel != null)
        {
            visualModel.DOShakePosition(duration: 0.1f, strength: 0.05f, vibrato: 20)
                       .SetLoops(-1, LoopType.Restart);

            visualModel.DOShakeRotation(duration: 0.1f, strength: new Vector3(0, 0, 15f), vibrato: 20)
                       .SetLoops(-1, LoopType.Restart);
        }
    }

    private IEnumerator GenerateAlarmNoise()
    {
        while (isAlarmActive)
        {
            NoiseManager.Instance.GenerateNoise(transform.position, NoiseData.NoiseType.Alarm);
            yield return new WaitForSeconds(1f);
        }
    }
}