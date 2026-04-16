using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerConditionUI : MonoBehaviour
{
    [SerializeField] private PlayerCondition player;

    [Header("Circular Gauges (Max 100)")]
    public Image healthFill;
    public Image satietyFill;
    public Image coldnessFill;

    [Header("Stamina Slider")]
    public Image staminaFillLeft; 
    public Image staminaFillRight; 

    [Header("Percentage Texts")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI satietyText;
    public TextMeshProUGUI coldnessText;

    private void Update()
    {
        // 원형 UI 업데이트 (값 / 100)
        UpdateCircle(healthFill, healthText, player.health.currentValue);
        UpdateCircle(satietyFill, satietyText, player.satiety.currentValue);
        UpdateCircle(coldnessFill, coldnessText, player.coldness.currentValue);

        // 스테미나 업데이트 (중앙에서 양옆으로 줄어드는 방식)
        if (staminaFillLeft != null)
        {
            staminaFillLeft.fillAmount = player.stamina.currentValue / 100f;
        }
        if (staminaFillRight != null)
        {
            staminaFillRight.fillAmount = player.stamina.currentValue / 100f;
        }
    }

    private void UpdateCircle(Image img, TextMeshProUGUI txt, float currentVal)
    {
        float ratio = currentVal / 100f;
        img.fillAmount = ratio;
        
        if (txt != null)
        {
            // 정수형태로 % 표시 (예: 98%)
            txt.text = $"{Mathf.CeilToInt(currentVal)}%"; 
        }
    }
}