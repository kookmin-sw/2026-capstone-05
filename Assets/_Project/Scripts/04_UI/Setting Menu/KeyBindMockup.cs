using UnityEngine;
using TMPro; 

public class KeyBindMockup : MonoBehaviour
{
    [Header("버튼 안의 텍스트 컴포넌트")]
    public TextMeshProUGUI keyText; 

    private bool isWaitingForInput = false;

    // 인스펙터의 OnClick() 이벤트에 연결할 함수
    public void StartListening()
    {
        isWaitingForInput = true;
        keyText.text = "..."; // 입력 대기 중임을 보여주는 텍스트 (예: 깜빡임이나 물음표)
    }

    // OnGUI는 키보드 이벤트를 가장 직관적으로 잡아낼 수 있는 유니티 내장 함수입니다.
    void OnGUI()
    {
        if (isWaitingForInput)
        {
            Event e = Event.current;

            // 키보드가 눌렸는지 확인
            if (e != null && e.isKey && e.type == EventType.KeyDown)
            {
                // 누른 키의 이름을 텍스트로 변경 (예: Space 누르면 "Space" 로 변경)
                keyText.text = e.keyCode.ToString();
                
                // 대기 상태 종료
                isWaitingForInput = false; 
            }
        }
    }
}