using UnityEngine;
using System.Collections.Generic;

namespace Systems.Shop
{
    public class VendingMachineShop : MonoBehaviour, IInteractable
    {
        [Header("자판기 판매 아이템 목록")]
        [SerializeField] private List<ShopItemEntry> itemsToSell;
        
        [Header("상호작용 설정")]
        [SerializeField] private string interactPrompt = "Open Shop";
        [SerializeField] private string objectName = "Pokopia Vending Machine";

        public bool CanInteract(PlayerController player)
        {
            // 상점이 열려있지 않을 때만 상호작용 가능하도록 할 수 있습니다.
            return true;
        }

        public void OnInteract(PlayerController player)
        {
            // 전역 ShopController 싱글톤을 호출하여 상점을 엽니다.
            if (ShopController.Instance != null)
            {
                ShopController.Instance.OpenShop(itemsToSell);
            }
            else
            {
                Debug.LogWarning("ShopController.Instance is null! 상점 UI 관리자가 씬에 없습니다.");
            }
        }

        public string GetInteractPrompt()
        {
            return interactPrompt;
        }

        public string GetObjectName()
        {
            return objectName;
        }
    }
}
