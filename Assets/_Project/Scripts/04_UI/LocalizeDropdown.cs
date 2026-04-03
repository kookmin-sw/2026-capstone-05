using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;

[RequireComponent(typeof(TMP_Dropdown))]
public class LocalizeDropdown : MonoBehaviour
{
    public TMP_Dropdown dropdown;
    
    [Header("번역할 String Table의 Key들을 순서대로 넣으세요")]
    public List<LocalizedString> localizedOptions;

    private void Reset()
    {
        // 스크립트를 넣으면 자동으로 드롭다운 컴포넌트를 찾아옵니다.
        dropdown = GetComponent<TMP_Dropdown>();
    }

    private void OnEnable()
    {
        // 언어가 바뀔 때마다 UpdateDropdown 함수가 실행되도록 연결
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        UpdateDropdown();
    }

    private void OnDisable()
    {
        // 오브젝트가 꺼질 때 연결 해제 (메모리 누수 방지)
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale locale)
    {
        UpdateDropdown();
    }

    private void UpdateDropdown()
    {
        if (dropdown == null || localizedOptions == null || localizedOptions.Count == 0) return;

        // 현재 선택된 인덱스 저장 (언어가 바뀌어도 선택값이 초기화되지 않게)
        int currentValue = dropdown.value;

        // 기존 옵션 비우기
        dropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> newOptions = new List<TMP_Dropdown.OptionData>();

        // 설정한 로컬라이징 키값들을 가져와서 새로운 옵션으로 추가
        foreach (var locString in localizedOptions)
        {
            // GetLocalizedString()으로 현재 언어에 맞는 텍스트를 가져옴
            newOptions.Add(new TMP_Dropdown.OptionData(locString.GetLocalizedString()));
        }

        dropdown.AddOptions(newOptions);
        dropdown.value = currentValue;
        dropdown.RefreshShownValue();
    }
}