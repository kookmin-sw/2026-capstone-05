using UnityEngine;

/// <summary>
/// mover/입력/카메라 갱신 책임을 authority 기준으로 분리해
/// 틱마다 Transform이 되돌아가는 문제를 방지한다.
/// </summary>
public class AuthorityStateController : MonoBehaviour
{
    private const string LogAuthority = "[AuthorityStateController]";

    private CharacterController _characterController;
    private PlayerController _playerController;
    private PlayerCameraHandler _cameraHandler;
    private PlayerInputHandler _inputHandler;
    private bool _allowLocalMovementOverride;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();
        _cameraHandler = GetComponent<PlayerCameraHandler>();
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    public void Apply(bool isLocalPlayer, bool isStateAuthority)
    {
        // 기본 원칙은 state authority가 이동/회전을 확정한다.
        // direct sync 테스트 모드에서는 input authority 로컬도 임시로 이동/회전을 허용해
        // 클라이언트 좌표를 서버에 직접 전송해 비교할 수 있게 한다.
        bool allowLocalMovement = _allowLocalMovementOverride && isLocalPlayer;
        bool shouldMove = isStateAuthority || isLocalPlayer || allowLocalMovement;
        bool shouldLook = isStateAuthority || isLocalPlayer || allowLocalMovement;
        bool shouldRunController = true;

        if (_characterController != null)
            _characterController.enabled = shouldRunController;

        if (_playerController != null)
        {
            _playerController.enabled = shouldRunController;
            _playerController.ConfigureAuthority(shouldMove, shouldLook);
        }

        if (_cameraHandler != null)
            _cameraHandler.enabled = isLocalPlayer;

        if (_inputHandler != null)
            _inputHandler.enabled = isLocalPlayer || isStateAuthority;

        IPlayerNetworkConfigurable[] configurables = GetComponentsInChildren<IPlayerNetworkConfigurable>(true);
        for (int i = 0; i < configurables.Length; i++)
        {
            if (configurables[i] is PlayerCameraHandler)
                continue;

            configurables[i].ConfigureForNetwork(isLocalPlayer);
        }

        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Debug.Log($"{LogAuthority} Apply. object={name}, local={isLocalPlayer}, state={isStateAuthority}, moveEnabled={shouldMove}, lookEnabled={shouldLook}, controllerEnabled={shouldRunController}");
    }

    public void SetAllowLocalMovementOverride(bool allowLocalMovementOverride)
    {
        _allowLocalMovementOverride = allowLocalMovementOverride;
    }
}
