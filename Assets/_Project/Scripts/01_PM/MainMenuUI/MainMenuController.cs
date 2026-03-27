using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private UIDocument _uiDocument;

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;

        // 버튼 찾기
        var btnSingle = root.Q<Button>("btn-single");
        var btnMulti = root.Q<Button>("btn-multi");
        var btnSettings = root.Q<Button>("btn-settings");
        var btnQuit = root.Q<Button>("btn-quit");

        // 이벤트 등록
        if (btnSingle != null)
        {
            btnSingle.clicked += () =>
            {
                // 싱글플레이 선택 시 Main1 씬 로드
                SceneManager.LoadScene("_Project/Scenes/00_General/Main1");
            };
        }

        if (btnQuit != null)
        {
            btnQuit.clicked += () =>
            {
                // 게임 종료
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            };
        }
    }
}
