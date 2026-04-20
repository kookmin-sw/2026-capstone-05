using UnityEngine;
using System.Collections;

public class MascotDialogueUI : MonoBehaviour
{
    [Header("말풍선 배경")]
    public GameObject speechBubbleBackground; 

    [Header("대사별 텍스트 오브젝트 (Localization 적용됨)")]
    public GameObject textGreeting;
    public GameObject textThankYou;
    public GameObject textSurprise;
    public GameObject textLaugh;
    public GameObject textReject;

    [Header("설정")]
    public float displayTime = 3.0f; // 말풍선 표시 시간

    private GameObject currentActiveText; // 현재 켜져 있는 텍스트 추적용
    private Coroutine hideCoroutine;

    private void OnEnable()
    {
        MascotEventManager.OnGreeting  += ShowGreeting;
        MascotEventManager.OnThankYou  += ShowThankYou;
        MascotEventManager.OnSurprise  += ShowSurprise;
        MascotEventManager.OnLaugh     += ShowLaugh;
        MascotEventManager.OnReject    += ShowReject;
    }

    private void OnDisable()
    {
        MascotEventManager.OnGreeting  -= ShowGreeting;
        MascotEventManager.OnThankYou  -= ShowThankYou;
        MascotEventManager.OnSurprise  -= ShowSurprise;
        MascotEventManager.OnLaugh     -= ShowLaugh;
        MascotEventManager.OnReject    -= ShowReject;
    }

    // --- 이벤트 수신 함수 ---
    private void ShowGreeting() => ActivateSpecificText(textGreeting);
    private void ShowThankYou() => ActivateSpecificText(textThankYou);
    private void ShowSurprise() => ActivateSpecificText(textSurprise);
    private void ShowLaugh()    => ActivateSpecificText(textLaugh);
    private void ShowReject()   => ActivateSpecificText(textReject);

    // 핵심 로직: 기존 것을 끄고 원하는 것만 켬
    private void ActivateSpecificText(GameObject targetTextObj)
    {
        if (targetTextObj == null) return;

        // 1. 말풍선 배경 켜기
        if (speechBubbleBackground != null) 
            speechBubbleBackground.SetActive(true);

        // 2. 이전에 켜져 있던 텍스트가 있다면 끄기 (겹침 방지)
        if (currentActiveText != null && currentActiveText != targetTextObj)
        {
            currentActiveText.SetActive(false);
        }

        // 3. 목표 텍스트 켜고 현재 상태 업데이트
        targetTextObj.SetActive(true);
        currentActiveText = targetTextObj;

        // 4. 타이머 초기화
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideDialogueAfterTime());
    }

    // 시간 경과 후 모두 끄기
    private IEnumerator HideDialogueAfterTime()
    {
        yield return new WaitForSeconds(displayTime);
        
        if (speechBubbleBackground != null) speechBubbleBackground.SetActive(false);
        if (currentActiveText != null)      currentActiveText.SetActive(false);
    }
}