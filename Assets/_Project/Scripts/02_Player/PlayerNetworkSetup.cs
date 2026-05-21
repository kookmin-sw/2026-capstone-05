using UnityEngine;

/// <summary>
/// 네트워크 스폰 시 로컬/원격 상태에 따라 초기화가 필요한 컴포넌트들이 구현해야 하는 인터페이스
/// </summary>
public interface IPlayerNetworkConfigurable
{
    void ConfigureForNetwork(bool isLocalPlayer);
}

public class PlayerNetworkSetup : MonoBehaviour
{
    private bool isLocalPlayer = false;

    /*
    [Header("Network Test")]
    public bool isLocalPlayerTest = true;

    public static bool IsOfflineTestMode { get; private set; } = false;

    private void Awake()
    {
        // 테스트 코드
        if (isLocalPlayerTest)
            IsOfflineTestMode = true;
    }

    private void Start()
    {
        if (isLocalPlayerTest)
        {
            InitializeNetworkState(true);
            GetComponent<PlayerCameraHandler>().TestLocalCameraSetup();
        }
    }
    */

    public void InitializeNetworkState(bool _isLocalPlayer)
    {
        isLocalPlayer = _isLocalPlayer;

        IPlayerNetworkConfigurable[] configurables = GetComponentsInChildren<IPlayerNetworkConfigurable>(true);

        foreach (var config in configurables)
        {
            config.ConfigureForNetwork(isLocalPlayer);
        }
    }

    [ContextMenu("Test Local Player Setup")]
    private void TestLocalPlayerSetup()
    {
        InitializeNetworkState(true);
    }
}
