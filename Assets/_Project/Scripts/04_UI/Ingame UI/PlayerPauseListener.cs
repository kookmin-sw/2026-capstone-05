using UnityEngine;

public class PlayerPauseListener : MonoBehaviour
{
    private PlayerInputHandler inputHandler;

    void Awake()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    void OnEnable()
    {
        PauseMenuManager.OnPauseStateChanged += HandlePauseState;
    }

    void OnDisable()
    {
        PauseMenuManager.OnPauseStateChanged -= HandlePauseState;
    }

    private void HandlePauseState(bool isPaused)
    {
        if (inputHandler != null)
        {
            // 💡 핵심: 핸들러를 끄는 대신, 팀원이 만든 Override 함수를 사용합니다.
            // isPaused가 true면 입력을 강제로 차단하고(Override), false면 다시 켭니다.
            inputHandler.SetNetworkInputOverride(isPaused);
        }
    }
}