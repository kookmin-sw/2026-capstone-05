using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동을 위해 필수
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using System;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance;

    [Header("UI Panels")]
    public GameObject pauseMenuPanel;
    public GameObject settingsPanel;
    public GameObject mainMenuConfirmPanel; //  추가: 메인메뉴 확인 창

    public static bool isPaused = false;
    public static event Action<bool> OnPauseStateChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuConfirmPanel != null) mainMenuConfirmPanel.SetActive(false);
        isPaused = false;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // 환경설정이나 확인창이 켜져있을 때는 ESC 무시
            if ((settingsPanel != null && settingsPanel.activeSelf) || 
                (mainMenuConfirmPanel != null && mainMenuConfirmPanel.activeSelf)) 
                return;

            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;
        isPaused = true;
        pauseMenuPanel.SetActive(true);
        pauseMenuPanel.transform.localScale = Vector3.zero;
        pauseMenuPanel.transform.DOScale(1f, 0.2f).SetUpdate(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        OnPauseStateChanged?.Invoke(true); 
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        OnPauseStateChanged?.Invoke(false); 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        pauseMenuPanel.transform.DOScale(0f, 0.15f).OnComplete(() => pauseMenuPanel.SetActive(false)).SetUpdate(true);
    }

    // --- 메인 메뉴 복귀 로직 ---

    // 1. "메인 메뉴로" 버튼을 눌렀을 때 확인 창 띄우기
    public void OpenMainMenuConfirm()
    {
        if (mainMenuConfirmPanel != null)
        {
            mainMenuConfirmPanel.SetActive(true);
            pauseMenuPanel.SetActive(false); // 기존 일시정지 창은 잠시 끔
        }
    }

    // 2. 확인 창에서 "아니오"를 눌렀을 때 다시 일시정지 창으로
    public void CloseMainMenuConfirm()
    {
        if (mainMenuConfirmPanel != null)
        {
            mainMenuConfirmPanel.SetActive(false);
            pauseMenuPanel.SetActive(true);
        }
    }

    // 3. 확인 창에서 "예"를 눌렀을 때 실제 이동 (씬 0번)
    public async void ConfirmGoToMainMenu()
    {
        // 중요: 이동 전 모든 잠금 상태를 수동으로 풀어줘야 메인메뉴에서 조작이 가능합니다.
        isPaused = false;
        OnPauseStateChanged?.Invoke(false);
        
        // 멀티플레이어 환경일 경우 여기서 네트워크 연결 해제 로직이 추가될 수 있습니다.
        
        // 씬 리스트의 0번(메인메뉴)을 불러옵니다.
        try
        {
            await RoomLauncher.ShutdownActiveRunnerAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PauseMenuManager] Failed to shut down network session before returning to main menu. {ex}");
        }

        SceneManager.LoadScene(0);
    }

    // (기존 OpenSettings / CloseSettings 생략...)
}
