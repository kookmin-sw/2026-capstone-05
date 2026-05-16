using UnityEngine;
using UnityEngine.UI;

public class MouseSensitivityController : MonoBehaviour
{
    [SerializeField] private Slider sensitivitySlider;

    void Start()
    {
        // 1. 기기에 저장된 감도 불러오기 (기본값 1.0)
        float savedSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        sensitivitySlider.value = savedSensitivity;

        // 2. 슬라이더를 움직일 때마다 값 저장
        sensitivitySlider.onValueChanged.AddListener(UpdateSensitivity);
    }

    public void UpdateSensitivity(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        // 즉시 적용을 위해 Save 호출
        PlayerPrefs.Save(); 
    }
}