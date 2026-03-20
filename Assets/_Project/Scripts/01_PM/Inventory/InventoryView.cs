using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.Inventory {
    public class InventoryView : StorageView {
        [SerializeField] string panelName = "Inventory";

        public override IEnumerator InitializeView(int size = 20) {
            Slots = new Slot[size];
            root = document.rootVisualElement;

            container = root.Q<VisualElement>(className: "container");
            if (container == null) {
                // 대비책 (UXML이 비어있을 경우)
                root.Clear();
                root.styleSheets.Add(styleSheet);
                container = root.CreateChild("container");
                var inv = container.CreateChild("inventory-window");
                inv.CreateChild("inventoryHeader").Add(new Label(panelName.ToUpper()));
                var sContainer = inv.CreateChild("slotsContainer");
                sContainer.style.width = (currentColumns * 69f) + 22f;
                int rows = Mathf.CeilToInt((float)size / currentColumns);
                sContainer.style.height = (rows * 69f) + 22f;
                for (int i = 0; i < size; i++) {
                    var slot = new Slot();
                    slot.name = "slot";
                    slot.AddToClassList("slot");
                    sContainer.Add(slot);
                    Slots[i] = slot;
                }

                // Append the specialized overlay itemsContainer inside the slotsContainer
                itemsContainer = sContainer.CreateChild("itemsContainer");
                itemsContainer.style.position = Position.Absolute;
                itemsContainer.style.top = 0;
                itemsContainer.style.left = 0;
                itemsContainer.style.right = 0;
                itemsContainer.style.bottom = 0;

                ghostIcon = container.CreateChild("ghostIcon");
                yield break;
            }
            
            var inventory = container.Q<VisualElement>(name: "inventory-window");
            
            var headerLabel = inventory.Q<Label>(name: "inventoryHeader");
            if (headerLabel != null) {
                headerLabel.text = panelName.ToUpper();
            }

            var slotsContainer = inventory.Q<VisualElement>(name: "slotsContainer");
            
            // 동적으로 가로 폭 계산 (슬롯크기 65 + 양옆마진 4 = 69) * 컬럼수 + 패딩 20 + 테두리 2 = 딱 맞는 사이즈
            slotsContainer.style.width = (currentColumns * 69f) + 22f;
            
            // 세로 폭 동적 계산 (빈 공간 최소화)
            int rowsForUpdate = Mathf.CeilToInt((float)size / currentColumns);
            slotsContainer.style.height = (rowsForUpdate * 69f) + 22f;
            
            var existingSlots = slotsContainer.Query<Slot>().ToList();

            if (existingSlots.Count >= size) {
                for (int i = 0; i < size; i++) {
                    Slots[i] = existingSlots[i];
                }
            } else {
                for (int i = 0; i < existingSlots.Count; i++) {
                    Slots[i] = existingSlots[i];
                }
                for (int i = existingSlots.Count; i < size; i++) {
                    var slot = new Slot();
                    slot.name = "slot";
                    slot.AddToClassList("slot");
                    slotsContainer.Add(slot);
                    Slots[i] = slot;
                }
            }

            // Also attach/re-attach the itemsContainer
            itemsContainer = slotsContainer.Q<VisualElement>("itemsContainer") ?? slotsContainer.CreateChild("itemsContainer");
            itemsContainer.style.position = Position.Absolute;
            itemsContainer.style.top = 0;
            itemsContainer.style.left = 0;
            
            ghostIcon = container.Q<VisualElement>(className: "ghostIcon");
            
            // 인벤토리 창 게임 시작 시 안 보이도록 숨기기
            container.style.display = DisplayStyle.None;
            
            yield return null; 
        }

        void Update() {
            // Tab 키를 누르면 인벤토리 토글 (표시/숨김)
            if (Input.GetKeyDown(KeyCode.Tab) && container != null) {
                bool isHidden = container.style.display == DisplayStyle.None;
                container.style.display = isHidden ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}

