using System;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    public event Action OnAttackEvent;
    //public event Action OnConsumableUsedEvent;
    //public event Action OnToolUsedEvent;

    public void UseCurrentItem()
    {
        OnAttackEvent?.Invoke();

        Debug.Log("UseCurrentItem called - Attack event triggered");
    }
}
