using UnityEngine;

public class GameExitManager : MonoBehaviour
{
    // 버튼의 OnClick() 이벤트에 연결할 함수입니다.
    public void QuitGame()
    {
        // 1. 디버그 로그 (에디터에서 버튼이 잘 눌렸는지 확인용)
        Debug.Log("게임을 종료합니다... (NUNBORA: Frostblind)");

        // 2. 실제 빌드된 게임(.exe)을 종료하는 핵심 코드
        Application.Quit();

        // 3. 유니티 에디터 환경에서 플레이 모드를 강제로 정지시키는 코드
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}