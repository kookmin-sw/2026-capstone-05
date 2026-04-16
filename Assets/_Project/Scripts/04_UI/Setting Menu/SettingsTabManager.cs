using UnityEngine;
using UnityEngine.UI;

public class SettingsTabManager : MonoBehaviour
{
    [Header("설정 패널들을 순서대로 넣으세요")]
    public GameObject[] settingPanels;

    [Header("상단 탭 버튼들을 순서대로 넣으세요")]
    public Image[] tabButtons; // 버튼의 Image 컴포넌트를 담을 배열

    [Header("버튼 이미지 소스")]
    public Sprite normalSprite; // 기본 상태 이미지 (회색)
    public Sprite activeSprite; // 활성화 상태 이미지 (파란색)

    private void OnEnable()
    {
        OpenTab(0); // 0번 인덱스(음량 탭)를 기본으로 열어줍니다.
    }
    
    public void OpenTab(int tabIndex)
    {
        for (int i = 0; i < settingPanels.Length; i++)
        {
            if (i == tabIndex)
            {
                // 1. 해당 탭의 패널 켜기
                settingPanels[i].SetActive(true);
                // 2. 해당 탭의 버튼 이미지를 파란색으로 변경
                tabButtons[i].sprite = activeSprite; 
            }
            else
            {
                // 1. 나머지 패널 끄기
                settingPanels[i].SetActive(false);
                // 2. 나머지 버튼 이미지를 기본색으로 변경
                tabButtons[i].sprite = normalSprite;
            }
        }
    }
}