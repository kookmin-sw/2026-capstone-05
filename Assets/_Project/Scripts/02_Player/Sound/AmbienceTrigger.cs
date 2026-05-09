using UnityEngine;

public class AmbienceTrigger : MonoBehaviour
{
    [Header("FMOD Settings")]
    [SerializeField] private string parameterName = "IsIndoors";
    [SerializeField] private float targetValue = 1f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null && player.IsLocalPlayer)
            {
                SoundManager.Instance.SetAmbienceParameter(parameterName, targetValue);
            }
        }
    }
}
