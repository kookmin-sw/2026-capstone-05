using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace Systems.Shop
{
    public class JapankiShop : MonoBehaviour, IInteractable
    {
        [Header("자판기 판매 아이템 목록")]
        [SerializeField] private List<ShopItemEntry> itemsToSell;

        private readonly string objectNameTable = "ObjectNames";
        private readonly string objectNameKey = "VendingMachine";
        private readonly string interactPromptTable = "InteractPrompts";
        private readonly string interactPromptKey = "OpenShop";

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
            return LocalizationSettings.StringDatabase.GetLocalizedString(interactPromptTable, interactPromptKey);
        }

        public string GetObjectName()
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(objectNameTable, objectNameKey);
        }
    }
}
