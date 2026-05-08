using UnityEngine;
using UnityEngine.Events; // 여전히 필요하다면 둡니다.

public class VendingMachineShop : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("켜고 끌 상점 UI 캔버스 또는 패널")]
    public GameObject shopUI; 
    
    // 이벤트 매니저를 쓸 것이므로 필수는 X
    public UnityEvent onShopOpenEvent; 
    public UnityEvent onShopCloseEvent;

    private bool isPlayerNearby = false;
    private bool isShopOpen = false;

    private void Start()
    {
        if (shopUI != null)
        {
            shopUI.SetActive(false);
        }
    }

    private void Update()
    {
        // 1. 플레이어가 근처에 있고, E키를 눌렀을 때
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            ToggleShop();
        }
    }

    // 2. 상점 열기/닫기 로직
    private void ToggleShop()
    {
        isShopOpen = !isShopOpen; // 상태 반전 (열림 <-> 닫힘)
        
        if (shopUI != null)
        {
            shopUI.SetActive(isShopOpen);
        }

        // 3. 상태에 따라 이벤트 매니저(MascotEventManager) 호출
        if (isShopOpen)
        {
            // 상점이 열렸을 때 (입장) -> 인사 애니메이션 이벤트 발생
            MascotEventManager.TriggerGreeting(); 
            
            onShopOpenEvent?.Invoke(); // 기존 유니티 이벤트도 병행 사용 가능
            Debug.Log("상점 입장: OnGreeting 이벤트 발송 완료");
        }
        else
        {
            // 상점이 닫혔을 때 (퇴장) -> 필요하다면 특정 애니메이션 호출 가능
            // MascotEventManager.TriggerIdle(); (Idle 이벤트가 있다면)
            
            onShopCloseEvent?.Invoke(); 
            Debug.Log("상점 퇴장");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            
            // 멀어지면 강제로 상점 닫기
            if (isShopOpen)
            {
                ToggleShop(); 
            }
        }
    }
}