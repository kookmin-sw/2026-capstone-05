using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

public class FMODVolumeController : MonoBehaviour
{
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private FMOD.Studio.Bus masterBus;
    private FMOD.Studio.Bus bgmBus;
    private FMOD.Studio.Bus sfxBus;

    void Start()
    {
        // 1. FMOD 버스 연결
        masterBus = RuntimeManager.GetBus("bus:/");
        bgmBus = RuntimeManager.GetBus("bus:/BGM_Bus");
        sfxBus = RuntimeManager.GetBus("bus:/SFX_Bus");

        // 2. PlayerPrefs에서 기존에 저장된 볼륨 값 불러오기
        // 만약 게임을 처음 켜서 저장된 값이 없다면 기본값인 0.8f를 사용합니다.
        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        float savedBGM = PlayerPrefs.GetFloat("BGMVolume", 0.8f);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

        // 3. 불러온 값으로 슬라이더 UI의 위치를 먼저 맞춰줍니다. (★핵심)
        masterSlider.value = savedMaster;
        bgmSlider.value = savedBGM;
        sfxSlider.value = savedSFX;

        // 4. FMOD 버스 볼륨도 실제 저장되었던 값으로 초기화합니다.
        UpdateMasterVolume(savedMaster);
        UpdateBGMVolume(savedBGM);
        UpdateSFXVolume(savedSFX);

        // 5. 슬라이더를 조작할 때 실행될 리스너 연결
        masterSlider.onValueChanged.AddListener(UpdateMasterVolume);
        bgmSlider.onValueChanged.AddListener(UpdateBGMVolume);
        sfxSlider.onValueChanged.AddListener(UpdateSFXVolume);
    }

    // 볼륨 변경 및 실시간 저장 함수들
    public void UpdateMasterVolume(float volume)
    {
        masterBus.setVolume(volume);
        PlayerPrefs.SetFloat("MasterVolume", volume); // 로컬에 값 저장
    }

    public void UpdateBGMVolume(float volume)
    {
        bgmBus.setVolume(volume);
        PlayerPrefs.SetFloat("BGMVolume", volume); // 로컬에 값 저장
    }

    public void UpdateSFXVolume(float volume)
    {
        sfxBus.setVolume(volume);
        PlayerPrefs.SetFloat("SFXVolume", volume); // 로컬에 값 저장
    }

    private void OnDestroy()
    {
        // 씬이 닫히거나 게임이 꺼질 때 변경된 데이터를 확실하게 저장하라고 명령합니다.
        PlayerPrefs.Save();
    }
}