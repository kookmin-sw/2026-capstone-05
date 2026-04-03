using UnityEngine;
using UnityEngine.Localization.Settings;
using System.Collections;

public class LanguageManager : MonoBehaviour
{
    private bool isChanging = false;

    // 드롭다운의 OnValueChanged 이벤트에 연결할 함수
    public void ChangeLanguage(int localeID)
    {
        if (isChanging) return;
        StartCoroutine(SetLocale(localeID));
    }

    IEnumerator SetLocale(int localeID)
    {
        isChanging = true;
        
        // 유니티 언어 시스템이 준비될 때까지 잠시 대기
        yield return LocalizationSettings.InitializationOperation;

        // 선택한 인덱스(0: 한국어, 1: 영어 등)로 언어 강제 변경
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeID];
        
        isChanging = false;
    }
}