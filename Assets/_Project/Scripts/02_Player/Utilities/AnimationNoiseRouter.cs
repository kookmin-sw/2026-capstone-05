using UnityEngine;

public class AnimationNoiseRouter : MonoBehaviour
{
    [Header("References")]
    public bool isLogicMaster;
    public NoiseEmitter noiseEmitter;

    public void OnIdleBreath(AnimationEvent e)
    {
        if (e.animatorClipInfo.weight < 0.5f)
        {
            return;
        }

        if (isLogicMaster && noiseEmitter != null)
        {
            noiseEmitter.EmitNoise(NoiseData.NoiseType.Idle);
        }
    }

    public void OnCrouchFootStep(AnimationEvent e)
    {
        if (e.animatorClipInfo.weight < 0.5f)
        {
            return;
        }

        if (isLogicMaster && noiseEmitter != null)
        {
            noiseEmitter.EmitNoise(NoiseData.NoiseType.Crouch);
        }
    }

    public void OnWalkFootStep(AnimationEvent e)
    {
        if (e.animatorClipInfo.weight < 0.5f)
        {
            return;
        }

        if (isLogicMaster && noiseEmitter != null)
        {
            noiseEmitter.EmitNoise(NoiseData.NoiseType.Walk);
        }
    }

    public void OnSprintFootStep(AnimationEvent e)
    {
        if (e.animatorClipInfo.weight < 0.5f)
        {
            return;
        }

        if (isLogicMaster && noiseEmitter != null)
        {
            noiseEmitter.EmitNoise(NoiseData.NoiseType.Sprint);
        }
    }
}
