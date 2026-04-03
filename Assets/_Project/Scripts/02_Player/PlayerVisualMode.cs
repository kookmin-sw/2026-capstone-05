using UnityEngine;
using UnityEngine.Rendering;

public class PlayerVisualMode : MonoBehaviour, IPlayerNetworkConfigurable
{
    [Header("1st Person")]
    [SerializeField] private GameObject model1P;

    [Header("3rd Person")]
    [SerializeField] private Renderer[] renderers3P;

    public void ConfigureForNetwork(bool isLocalPlayer)
    {
        if (model1P != null)
        {
            model1P.SetActive(isLocalPlayer);
        }

        foreach (Renderer rend in renderers3P)
        {
            if (rend == null) continue;

            if (isLocalPlayer)
            {
                rend.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            else
            {
                rend.shadowCastingMode = ShadowCastingMode.On;
            }
        }
    }
}