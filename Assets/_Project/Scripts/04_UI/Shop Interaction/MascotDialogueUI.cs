using UnityEngine;
using System.Collections;
using DG.Tweening;

public class MascotDialogueUI : MonoBehaviour
{
    [Header("말풍선 전체 묶음")]
    public GameObject speechBubbleRoot; 

    [Header("대사별 텍스트 오브젝트")]
    public GameObject textGreeting;
    public GameObject textThankYou;
    public GameObject textSurprise;
    public GameObject textLaugh;
    public GameObject textReject;

    public float displayTime = 3.0f; 
    public float animationDuration = 0.3f;

    private GameObject currentActiveText; 
    private Coroutine hideCoroutine;

    // 💡 1. Start 대신 Awake에서 구독하여 이벤트를 놓치지 않게 함
    private void Awake()
    {
        MascotEventManager.OnGreeting  += ShowGreeting;
        MascotEventManager.OnThankYou  += ShowThankYou;
        MascotEventManager.OnSurprise  += ShowSurprise;
        MascotEventManager.OnLaugh     += ShowLaugh;
        MascotEventManager.OnReject    += ShowReject;
    }

    // 💡 2. 상점 UI가 켜질 때마다 스케일을 0으로 초기화
    private void OnEnable()
    {
        if (speechBubbleRoot != null)
        {
            speechBubbleRoot.transform.localScale = Vector3.zero;
            speechBubbleRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        MascotEventManager.OnGreeting  -= ShowGreeting;
        MascotEventManager.OnThankYou  -= ShowThankYou;
        MascotEventManager.OnSurprise  -= ShowSurprise;
        MascotEventManager.OnLaugh     -= ShowLaugh;
        MascotEventManager.OnReject    -= ShowReject;
    }

    // 외부에서 버튼이나 상점 스크립트가 직접 호출할 수 있도록 public으로 둠
    public void ShowGreeting() => ActivateSpecificText(textGreeting);
    public void ShowThankYou() => ActivateSpecificText(textThankYou);
    public void ShowSurprise() => ActivateSpecificText(textSurprise);
    public void ShowLaugh()    => ActivateSpecificText(textLaugh);
    public void ShowReject()   => ActivateSpecificText(textReject);

    private void ActivateSpecificText(GameObject targetTextObj)
    {
        if (targetTextObj == null) return;

        if (currentActiveText != null) currentActiveText.SetActive(false);

        targetTextObj.SetActive(true);
        currentActiveText = targetTextObj;

        if (speechBubbleRoot != null)
        {
            speechBubbleRoot.transform.DOKill(); 
            speechBubbleRoot.SetActive(true);
            speechBubbleRoot.transform.localScale = Vector3.zero;
            // 등장 애니메이션
            speechBubbleRoot.transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack);
        }

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideDialogueAfterTime());
    }

    private IEnumerator HideDialogueAfterTime()
    {
        yield return new WaitForSeconds(displayTime);
        
        if (speechBubbleRoot != null) 
        {
            speechBubbleRoot.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() => 
            {
                speechBubbleRoot.SetActive(false);
                if (currentActiveText != null) currentActiveText.SetActive(false);
            });
        }
    }
}