using UnityEngine;

public class MascotTestSender : MonoBehaviour
{
    // Canvas의 버튼 OnClick에 아래 함수들을 각각 연결하여 테스트
    
    public void Test_Greeting()
    {
        MascotEventManager.TriggerGreeting();
    }

    public void Test_ThankYou()
    {
        MascotEventManager.TriggerThankYou();
    }

    public void Test_Surprise()
    {
        MascotEventManager.TriggerSurprise();
    }

    public void Test_Laugh()
    {
        MascotEventManager.TriggerLaugh();
    }

    public void Test_Reject()
    {
        MascotEventManager.TriggerReject();
    }
}