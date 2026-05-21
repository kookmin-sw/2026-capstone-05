using System.Collections.Generic;
using TMPro; // TextMeshPro 드롭다운을 사용하기 위한 필수 선언
using UnityEngine;

public class GraphicSettingsController : MonoBehaviour
{
    [Header("UI Dropdowns")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown windowModeDropdown;

    private readonly int[] widths = { 1280, 1600, 1920, 2560, 3840, 1280, 1440, 1680, 1920, 2560, 3840 };
    private readonly int[] heights = { 720, 900, 1080, 1440, 2160, 800, 900, 1050, 1200, 1600, 2400 };
    private readonly string[] aspectLabels = { "16:9", "16:9", "16:9", "16:9", "16:9", "16:10", "16:10", "16:10", "16:10", "16:10", "16:10" };

    void Start()
    {
        // 1. 기기에 저장된 그래픽 설정 불러오기
        // 저장된 값이 없다면 기본값으로 FHD(1920x1080, 인덱스 2), 전체화면(인덱스 0)을 사용합니다.
        int savedResIndex = PlayerPrefs.GetInt("ResolutionIndex", 2);
        int savedModeIndex = PlayerPrefs.GetInt("WindowModeIndex", 0);
        if (savedResIndex < 0 || savedResIndex >= widths.Length)
        {
            savedResIndex = 2;
        }

        // 2. 드롭다운 UI의 선택 항목을 저장된 값으로 맞춰줍니다.
        PopulateResolutionDropdown();
        resolutionDropdown.SetValueWithoutNotify(savedResIndex);
        windowModeDropdown.SetValueWithoutNotify(savedModeIndex);

        // 3. 불러온 설정으로 실제 화면을 즉시 변경합니다.
        ApplyGraphicSettings(savedResIndex, savedModeIndex);

        // 4. 유저가 드롭다운 값을 바꿀 때마다 자동으로 실행되도록 연결해 줍니다.
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        windowModeDropdown.onValueChanged.AddListener(SetWindowMode);
    }

    // 해상도 드롭다운 변경 시 실행
    public void SetResolution(int index)
    {
        PlayerPrefs.SetInt("ResolutionIndex", index);
        ApplyGraphicSettings(index, windowModeDropdown.value); // 현재 창모드 상태를 함께 넘김
    }

    // 창모드 드롭다운 변경 시 실행
    public void SetWindowMode(int index)
    {
        PlayerPrefs.SetInt("WindowModeIndex", index);
        ApplyGraphicSettings(resolutionDropdown.value, index); // 현재 해상도 상태를 함께 넘김
    }

    // 실제 유니티 시스템에 화면 크기를 적용하는 핵심 함수
    private void ApplyGraphicSettings(int resIndex, int modeIndex)
    {
        // 안전 장치: 인덱스가 배열 범위를 벗어나면 기본값(FHD)으로 강제 고정
        if (resIndex < 0 || resIndex >= widths.Length) resIndex = 2;

        int width = widths[resIndex];
        int height = heights[resIndex];

        // 유니티의 창모드 열거형(Enum)으로 변환
        FullScreenMode screenMode = FullScreenMode.ExclusiveFullScreen;
        switch (modeIndex)
        {
            case 0:
                screenMode = FullScreenMode.ExclusiveFullScreen; // 전체화면
                break;
            case 1:
                screenMode = FullScreenMode.Windowed; // 창모드
                break;
            case 2:
                screenMode = FullScreenMode.FullScreenWindow; // 테두리 없는 전체화면
                break;
        }

        // 유니티 엔진에 해상도 및 창모드 적용!
        Screen.SetResolution(width, height, screenMode);
        
        // 즉시 로컬에 데이터 저장
        PlayerPrefs.Save();
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null)
        {
            return;
        }

        var options = new List<TMP_Dropdown.OptionData>(widths.Length);
        for (var index = 0; index < widths.Length; index++)
        {
            options.Add(new TMP_Dropdown.OptionData($"{widths[index]} x {heights[index]} ({aspectLabels[index]})"));
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
    }
}
