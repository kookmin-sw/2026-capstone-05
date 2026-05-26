using UnityEngine;
using UnityEngine.UI;

public class CastingBarUI : MonoBehaviour
{
    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        CastingSystem.OnCastingUpdated += UpdateCastingBar;
        UpdateCastingBar(0f, false, Color.white);

    }

    private void OnDisable()
    {
        CastingSystem.OnCastingUpdated -= UpdateCastingBar;
    }

    private void UpdateCastingBar(float progress, bool isActive, Color color)
    {
        image.fillAmount = progress;
        image.color = color;
        image.enabled = isActive;
    }
}
