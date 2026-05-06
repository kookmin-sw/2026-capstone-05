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
    [Header("Network Test")]
    public bool isLocalPlayerTest = true;

    private void Start()
    {
        // 테스트 코드
        InitializeNetworkState(true);
    }

    public void InitializeNetworkState(bool isLocalPlayer)
    {
        isLocalPlayerTest = isLocalPlayer;

        IPlayerNetworkConfigurable[] configurables = GetComponentsInChildren<IPlayerNetworkConfigurable>(true);

        foreach (var config in configurables)
        {
            config.ConfigureForNetwork(isLocalPlayerTest);
        }
    }

    [ContextMenu("Test Local Player Setup")]
    private void TestLocalPlayerSetup()
    {
        InitializeNetworkState(true);
    }
}
