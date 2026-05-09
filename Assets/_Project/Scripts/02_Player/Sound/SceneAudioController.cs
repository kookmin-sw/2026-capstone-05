using FMODUnity;
using UnityEngine;

public class SceneAudioController : MonoBehaviour
{
    public EventReference ambienceEvent;

    private void Start()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayAmbience(ambienceEvent);
        }
    }
}
