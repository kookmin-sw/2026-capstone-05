using UnityEngine;

public class HitEffectTester : MonoBehaviour
{
    [Header("테스트 대상 플레이어")]
    [SerializeField] private PlayerCondition playerCondition;

    [Header("테스트 설정")]
    [Tooltip("H 키를 누를 때마다 깎일 체력량")]
    public float damageAmount = 10f; 

    void Update()
    {
        // H 키를 눌렀을 때 작동
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (playerCondition != null)
            {
                // PlayerCondition의 health 스탯을 감소시킴
                // (ConditionStat 클래스의 Subtract 메서드 사용)
                playerCondition.health.Subtract(damageAmount);

                Debug.Log($"[테스트] 피격! {damageAmount}의 데미지를 입었습니다. 남은 체력: {playerCondition.health.currentValue}");
            }
            else
            {
                Debug.LogWarning("[테스트] PlayerCondition이 연결되지 않았습니다!");
            }
        }
    }
}