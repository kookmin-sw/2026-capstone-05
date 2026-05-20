using System;

public static class MascotEventManager
{
    // 1. 인사 (예: 상점에 들어왔을 때)
    public static Action OnGreeting;
    
    // 2. 감사 (예: 아이템 구매를 완료했을 때)
    public static Action OnThankYou;

    // 3. 놀람 (예: 너무 비싼 아이템을 클릭하거나, 잔액이 부족할 때)
    public static Action OnSurprise;

    // 4. 웃음 (예: 특정 대화나 긍정적인 상호작용을 할 때)
    public static Action OnLaugh;

    // 5. 거부 (예: 판매 불가 아이템이거나, 잘못된 조작을 할 때)
    public static Action OnReject;
    public static Action OnPlacementFailed;
    public static Action OnHideDialogue;


    // --- 이벤트를 실행하는 Trigger 함수들 ---
    public static void TriggerGreeting() => OnGreeting?.Invoke();
    public static void TriggerThankYou() => OnThankYou?.Invoke();
    public static void TriggerSurprise() => OnSurprise?.Invoke();
    public static void TriggerLaugh()    => OnLaugh?.Invoke();
    public static void TriggerReject()   => OnReject?.Invoke();
    public static void TriggerPlacementFailed() => OnPlacementFailed?.Invoke();
    public static void TriggerHideDialogue() => OnHideDialogue?.Invoke();
}
