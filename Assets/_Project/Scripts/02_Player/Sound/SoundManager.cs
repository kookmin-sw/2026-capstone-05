using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("VCA Paths")]
    [SerializeField] private string masterVCAPath = "vca:/Master";
    [SerializeField] private string bgmVCAPath = "vca:/BGM";
    [SerializeField] private string sfxVCAPath = "vca:/SFX";

    private VCA masterVCA;
    private VCA bgmVCA;
    private VCA sfxVCA;

    public float MasterVolume { get; private set; }
    public float BGMVolume { get; private set; }
    public float SFXVolume { get; private set; }

    private EventInstance currentBGM;
    private EventInstance currentAmbience;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFMOD();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeFMOD()
    {
        masterVCA = RuntimeManager.GetVCA(masterVCAPath);
        bgmVCA = RuntimeManager.GetVCA(bgmVCAPath);
        sfxVCA = RuntimeManager.GetVCA(sfxVCAPath);

        SetMasterVolume(PlayerPrefs.GetFloat("MasterVolume", 1f));
        SetBGMVolume(PlayerPrefs.GetFloat("BGMVolume", 1f));
        SetSFXVolume(PlayerPrefs.GetFloat("SFXVolume", 1f));
    }

    public void SetMasterVolume(float volume)
    {
        MasterVolume = volume;
        masterVCA.setVolume(volume);
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    public void SetBGMVolume(float volume)
    {
        BGMVolume = volume;
        bgmVCA.setVolume(volume);
        PlayerPrefs.SetFloat("BGMVolume", volume);
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
        sfxVCA.setVolume(volume);
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    public void PlayBGM(EventReference bgmEvent)
    {
        StopBGM();

        currentBGM = RuntimeManager.CreateInstance(bgmEvent);
        currentBGM.start();
    }

    public void StopBGM()
    {
        if (currentBGM.isValid())
        {
            currentBGM.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            currentBGM.release();
        }
    }

    public void PlayAmbience(EventReference ambienceEvent)
    {
        StopAmbience();

        currentAmbience = RuntimeManager.CreateInstance(ambienceEvent);

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            RuntimeManager.AttachInstanceToGameObject(currentAmbience, mainCam.gameObject);
        }
        else
        {
            Debug.LogWarning("Can't find main camera for 3D ambience.");
        }

        currentAmbience.start();
    }

    public void StopAmbience()
    {
        if (currentAmbience.isValid())
        {
            currentAmbience.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            currentAmbience.release();
        }
    }
     
    public void SetAmbienceParameter(string paramName, float value)
    {
        if (currentAmbience.isValid())
        {
            currentAmbience.setParameterByName(paramName, value);
        }
    }
}