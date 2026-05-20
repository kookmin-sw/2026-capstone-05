using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

public class SceneAudioController : MonoBehaviour
{
    [FormerlySerializedAs("defaultAmbienceEvent")]
    [SerializeField] private EventReference ambienceEvent;

    private void Start()
    {
        if (SoundManager.Instance == null || ambienceEvent.IsNull)
        {
            return;
        }

        SoundManager.Instance.PlayAmbience(ambienceEvent);
    }
}
