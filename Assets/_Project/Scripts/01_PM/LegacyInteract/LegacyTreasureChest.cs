/*
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어가 상호작용할 수 있는 상자 오브젝트
/// </summary>
public class LegacyTreasureChest : MonoBehaviour, IInteractable
{
    [Header("Chest Settings")]
    [SerializeField] private GameObject[] targetEnemies; // 감지할 적 게임오브젝트들
    [SerializeField] private int keyReward = 1; // 획득할 열쇠 개수
    
    [Header("Visual Settings")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private Transform lidTransform; // 회전할 상자 뚜껑
    [SerializeField] private float openAngle = -90f; // 열릴 각도 (X축 기준)
    [SerializeField] private float openSpeed = 2f; // 열리는 속도

    [Header("Highlight Settings")]
    [SerializeField] private Renderer[] chestRenderers; // 하이라이트할 렌더러들 (Lid, Base 등)
    [ColorUsage(true, true)]
    [SerializeField] private Color lockedHighlightColor = Color.red; // 잠김 상태 하이라이트 색상
    [ColorUsage(true, true)]
    [SerializeField] private Color unlockedHighlightColor = Color.yellow; // 해제 상태 하이라이트 색상
    [Range(0f, 5f)]
    [SerializeField] private float highlightIntensity = 0.2f; // 하이라이트 강도 조절

    private bool isUnlocked = false;
    private bool isOpened = false;
    private bool isOpening = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isPlayerInRange = false;

    // 하이라이트 관련 변수
    private List<Material> chestMaterials = new List<Material>();
    private List<Color> originalEmissionColors = new List<Color>();
    private List<bool> originalEmissionEnabledStates = new List<bool>();
    
    private void Start()
    {
        // 뚜껑의 초기 회전값과 열린 상태의 회전값 설정
        if (lidTransform != null)
        {
            closedRotation = lidTransform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(openAngle, 0, 0);
        }

        // 하이라이트용 머티리얼 초기화
        if (chestRenderers != null)
        {
            foreach (Renderer renderer in chestRenderers)
            {
                if (renderer != null)
                {
                    foreach (Material mat in renderer.materials)
                    {
                        chestMaterials.Add(mat);
                        originalEmissionColors.Add(mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black);
                        originalEmissionEnabledStates.Add(mat.IsKeywordEnabled("_EMISSION"));
                    }
                }
            }
        }
    }
    
    private void Update()
    {
        // 매 프레임마다 적들의 상태를 체크
        if (!isUnlocked)
        {
            CheckEnemiesStatus();
        }

        // 뚜껑 열기 애니메이션 처리
        if (isOpening && lidTransform != null)
        {
            lidTransform.localRotation = Quaternion.Slerp(lidTransform.localRotation, openRotation, Time.deltaTime * openSpeed);
            
            if (Quaternion.Angle(lidTransform.localRotation, openRotation) < 1f)
            {
                lidTransform.localRotation = openRotation;
                isOpening = false;
            }
        }
    }
    
    private void CheckEnemiesStatus()
    {
        // 배열이 비어있으면 바로 언락 (조건 없음)
        if (targetEnemies == null || targetEnemies.Length == 0)
        {
            if (!isUnlocked)
            {
                isUnlocked = true;
            }
            return;
        }
            
        int inactiveCount = 0;
        
        // 배열에 등록된 적들의 비활성화 상태를 확인
        foreach (GameObject enemy in targetEnemies)
        {
            if (enemy != null && !enemy.activeInHierarchy)
            {
                inactiveCount++;
            }
        }
        
        // 모든 적이 비활성화되었으면 상자 언락
        if (inactiveCount >= targetEnemies.Length && !isUnlocked)
        {
            isUnlocked = true;
            Debug.Log($"🎉 모든 적을 처치했습니다! 보물상자가 잠금 해제되었습니다!");
            
            // 상태 변경 시 하이라이트 갱신
            UpdateHighlight();
        }
    }

    private void UpdateHighlight()
    {
        if (isPlayerInRange)
        {
            SetHighlight(true);
        }
    }

    private void SetHighlight(bool active)
    {
        if (chestMaterials.Count == 0) return;

        // 이미 열린 상자는 하이라이트 표시 안 함
        if (isOpened) active = false;

        if (active)
        {
            // 잠김 상태면 빨간색, 해제 상태면 노란색(기본)
            Color targetColor = (!isUnlocked) ? lockedHighlightColor : unlockedHighlightColor;
            
            // 강도 적용 (기존 색상이 너무 밝으면 텍스처가 묻히므로 강도를 조절)
            Color finalColor = targetColor * highlightIntensity;

            foreach (Material mat in chestMaterials)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", finalColor);
            }
        }
        else
        {
            // 원래 상태로 복구
            for (int i = 0; i < chestMaterials.Count; i++)
            {
                Material mat = chestMaterials[i];
                if (originalEmissionEnabledStates[i])
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", originalEmissionColors[i]);
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", originalEmissionColors[i]);
                }
            }
        }
    }
    
    private int GetDefeatedEnemyCount()
    {
        if (targetEnemies == null || targetEnemies.Length == 0)
            return 0;
            
        int count = 0;
        foreach (GameObject enemy in targetEnemies)
        {
            if (enemy != null && !enemy.activeInHierarchy)
            {
                count++;
            }
        }
        return count;
    }
    
    public bool CanInteract(PlayerController player)
    {
        return isUnlocked && !isOpened;
    }
    
    public void OnInteract(PlayerController player)
    {
        if (!CanInteract(player)) 
        {
            return;
        }
        
        isOpened = true;
        
        // 상자가 열리면 하이라이트 끄기
        SetHighlight(false);
        
        // 상자 열기 애니메이션
        if (chestAnimator != null)
        {
            chestAnimator.SetTrigger("Open");
        }
        
        // 뚜껑 회전 시작
        if (lidTransform != null)
        {
            isOpening = true;
        }
        
        // 플레이어에게 열쇠 추가
        GiveKeyToPlayer();
    }
    
    private void GiveKeyToPlayer()
    {
        LegacyInventory playerInventory = FindFirstObjectByType<LegacyInventory>();
        if (playerInventory != null)
        {
            playerInventory.AddKeys(keyReward);
            Debug.Log($"📦 보물상자를 열어 {keyReward}개의 열쇠를 획득했습니다!");
        }
        else
        {
            Debug.LogWarning("❌ PlayerInventory를 찾을 수 없습니다!");
        }
    }
    
    // UI 제거됨 - 로그로만 상태 확인
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            SetHighlight(true);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            SetHighlight(false);
        }
    }
}
*/