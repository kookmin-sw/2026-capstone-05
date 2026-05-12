using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

public class SceneAudioController : MonoBehaviour
{
    [FormerlySerializedAs("ambienceEvent")]
    [SerializeField] private EventReference defaultAmbienceEvent;
    [SerializeField] private EventReference snowAmbienceEvent;
    [SerializeField] private EventReference blizzardAmbienceEvent;

    private void Start()
    {
        ApplyWeatherAudio(BackendRoundManager.Instance != null
            ? BackendRoundManager.Instance.CurrentWeatherState
            : NetworkWeatherState.Snow);
    }

    private void OnEnable()
    {
        BackendRoundManager.WeatherStateChanged += ApplyWeatherAudio;
    }

    private void OnDisable()
    {
        BackendRoundManager.WeatherStateChanged -= ApplyWeatherAudio;
    }

    private void ApplyWeatherAudio(NetworkWeatherState weatherState)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        EventReference selectedEvent = ResolveWeatherEvent(weatherState);
        if (selectedEvent.IsNull)
        {
            return;
        }

        SoundManager.Instance.PlayAmbience(selectedEvent);
    }

    private EventReference ResolveWeatherEvent(NetworkWeatherState weatherState)
    {
        switch (weatherState)
        {
            case NetworkWeatherState.Blizzard:
                if (!blizzardAmbienceEvent.IsNull)
                {
                    return blizzardAmbienceEvent;
                }

                break;

            case NetworkWeatherState.Snow:
            default:
                if (!snowAmbienceEvent.IsNull)
                {
                    return snowAmbienceEvent;
                }

                break;
        }

        return defaultAmbienceEvent;
    }
}
