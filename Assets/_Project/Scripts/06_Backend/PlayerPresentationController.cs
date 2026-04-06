using UnityEngine;

/// <summary>
/// Player.prefab의 기존 1P/3P 계층(예: PlayerVisualMode)을 존중해 로컬/원격 표현만 전환한다.
/// </summary>
public class PlayerPresentationController : MonoBehaviour
{
    private const string LogPresentation = "[PlayerPresentation]";

    private PlayerVisualMode _visualMode;

    private void Awake()
    {
        _visualMode = GetComponent<PlayerVisualMode>();
    }

    public void Apply(bool isLocalPlayer)
    {
        if (_visualMode != null)
            _visualMode.ConfigureForNetwork(isLocalPlayer);

        Debug.Log($"{LogPresentation} Apply. object={name}, local1P={isLocalPlayer}");
    }
}
