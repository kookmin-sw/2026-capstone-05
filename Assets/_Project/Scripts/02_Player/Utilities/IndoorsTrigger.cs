using UnityEngine;

public class IndoorsTrigger : MonoBehaviour
{
    public bool isIndoors = true;

    private int overlapCount = 0;

    [Header("FMOD Settings")]
    [SerializeField] private string fmodParameterName = "IsIndoors";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null && player.IsLocalPlayer)
            {
                overlapCount++;

                if (overlapCount == 1)
                {
                    player.GetComponent<PlayerCondition>().SetIndoors(isIndoors);
                    SoundManager.Instance.SetAmbienceParameter(fmodParameterName, isIndoors ? 1f : 0f);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null && player.IsLocalPlayer)
            {
                overlapCount--;

                if (overlapCount <= 0)
                {
                    overlapCount = 0;
                    player.GetComponent<PlayerCondition>().SetIndoors(false);
                    SoundManager.Instance.SetAmbienceParameter(fmodParameterName, 0f);
                }
            }
        }
    }

    public void OnPlayerForceExit(PlayerController player)
    {
        overlapCount = 0;
        if (player != null && player.IsLocalPlayer)
        {
            player.GetComponent<PlayerCondition>().SetIndoors(false);
            SoundManager.Instance.SetAmbienceParameter(fmodParameterName, 0f);
        }
    }
}
