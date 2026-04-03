using UnityEngine;
using TMPro;

/// <summary>
/// 열쇠로 열 수 있는 문 오브젝트
/// </summary>
public class LockedDoor : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f; // 문이 열릴 각도
    [SerializeField] private float openSpeed = 2f; // 문 열리는 속도
    
    [Header("Visual Settings")]
    [SerializeField] private Transform doorTransform; // 회전할 문 Transform
    [SerializeField] private GameObject doorBlocker; // 문을 막는 콜라이더
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip openSound;

    [Header("UI Settings")]
    [SerializeField] private GameObject interactionHintUI;
    
    private bool isOpened = false;
    // private bool playerInRange = false; // 사용되지 않음 - PlayerInteraction에서 범위 체크
    private bool isOpening = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    
    [Header("Interaction Settings")]
    [SerializeField] private bool canInteractDirectly = true; // 직접 상호작용 가능 여부 (다른 스크립트에서 제어할 때 false)

    private void Start()
    {
        // 문의 초기 회전값과 열린 상태의 회전값 설정
        if (doorTransform == null)
            doorTransform = transform;
            
        closedRotation = doorTransform.rotation;
        openRotation = closedRotation * Quaternion.Euler(0, openAngle, 0);

        if (interactionHintUI != null)
        {
            interactionHintUI.SetActive(false);
        }
    }
    
    private void Update()
    {
        // 문 열기 애니메이션 처리
        if (isOpening && !isOpened)
        {
            doorTransform.rotation = Quaternion.Slerp(doorTransform.rotation, openRotation, Time.deltaTime * openSpeed);
            
            // 회전이 거의 완료되면 열린 상태로 설정
            if (Quaternion.Angle(doorTransform.rotation, openRotation) < 1f)
            {
                doorTransform.rotation = openRotation;
                isOpened = true;
                isOpening = false;
                
                // 문 블로커 제거
                if (doorBlocker != null)
                {
                    doorBlocker.SetActive(false);
                }
            }
        }
    }
    
    public bool CanInteract(PlayerController player)
    {
        // 직접 상호작용이 꺼져있으면 false 반환 (BoatInteraction 등이 대신 처리)
        if (!canInteractDirectly) return false;

        return !isOpened && !isOpening;
    }
    
    public void OnInteract(PlayerController player)
    {
        if (!CanInteract(player)) 
        {
            return;
        }
        
        PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerInventory == null)
        {
            return;
        }
        
        // 열쇠가 있는지 확인하고 사용
        if (playerInventory.HasKey())
        {
            playerInventory.UseKey();
            Debug.Log("🚪 문이 열렸습니다!");
            OpenDoor();
        }
        else
        {
            Debug.Log("🔒 이 문을 열려면 열쇠가 필요합니다!");
        }
    }
    
    public void OpenDoor()
    {
        if (isOpened || isOpening) return;
        
        isOpening = true;
        
        // 잠금 해제 사운드
        if (audioSource != null && unlockSound != null)
        {
            audioSource.PlayOneShot(unlockSound);
        }
        
        // 문 열기 사운드
        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

    }
    
    // UI 제거됨 - 로그로만 상태 확인
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // UI 표시는 canInteractDirectly와 상관없이 수행 (SceneTransitionInteraction이 있어도 UI는 LockedDoor가 담당)
            if (interactionHintUI != null)
            {
                UpdateHintText();
                interactionHintUI.SetActive(true);
            }
        }
    }

    private void UpdateHintText()
    {
        TextMeshProUGUI textUI = interactionHintUI.GetComponentInChildren<TextMeshProUGUI>();
        if (textUI != null)
        {
            // 자체 열쇠 로직 확인
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            bool hasKey = inventory != null && inventory.HasKey();
            textUI.text = hasKey ? "문 열기     E" : "문 잠김     E";
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (interactionHintUI != null)
            {
                interactionHintUI.SetActive(false);
            }
        }
    }
}