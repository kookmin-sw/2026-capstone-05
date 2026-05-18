using UnityEngine;

public class FlashlightBehaviour : EquippedItemBehaviour
{
    public Light flashlightLight;

    public override void Initialize(PlayerController owner, ItemInstance instance, bool is1P)
    {
        base.Initialize(owner, instance, is1P);
        if (flashlightLight == null)
        {
            flashlightLight = GetComponentInChildren<Light>();

            FlashlightData flashlightData = instance.Data as FlashlightData;

            if (flashlightData != null && flashlightLight != null)
            {
                flashlightLight.range = flashlightData.lightRange;
                flashlightLight.intensity = flashlightData.lightIntensity;
                flashlightLight.color = flashlightData.lightColor;
                flashlightLight.enabled = false;
            }
        }
    }

    public override bool Use()
    {
        if (!base.Use())
        {
            return false;
        }

        if (flashlightLight != null)
        {
            flashlightLight.enabled = !flashlightLight.enabled;
        }
        return true;
    }
}
