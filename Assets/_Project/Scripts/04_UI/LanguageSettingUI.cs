using UnityEngine;
using TMPro;

public class LanguageSettingsUI : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_Dropdown languageDropdown; // 환경설정의 언어 드롭다운 연결

    void Start()
    {
        // 환경설정 창이 켜질 때 초기 UI 동기화 실행
        SyncDropdownWithCurrentLanguage();
    }

    private void SyncDropdownWithCurrentLanguage()
    {
        // 1. 현재 게임에 적용된 언어 상태를 가져옴
        SystemLanguage currentLanguage = Application.systemLanguage; 

        // 2. 드롭다운의 옵션 순서(Index)에 맞게 매칭
        // Dropdown Option [0] 한국어 / [1] English
        int targetDropdownIndex = 0; // 기본값 인덱스 (한국어)

        if (currentLanguage == SystemLanguage.English)
        {
            targetDropdownIndex = 1; // 영어일 경우 인덱스 1번으로 설정
        }

        // 3. 드롭다운 UI에 값을 적용합니다.
        if (languageDropdown != null)
        {
            // value를 직접 바꾸면 OnValueChanged 이벤트가 발생해 언어 설정 로직이 
            // 중복 실행될 수 있으므로 SetValueWithoutNotify를 사용하는 것이 안전
            languageDropdown.SetValueWithoutNotify(targetDropdownIndex);
        }
    }
}