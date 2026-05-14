using UnityEngine;

public class IndoorsTrigger : MonoBehaviour
{
    public bool isIndoors = true;

    private int overlapCount = 0;

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
                }
            }
        }
    }
}
