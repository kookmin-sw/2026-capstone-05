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
                for (int i = 0; i < size; i++) {
                    var slot = sContainer.CreateChild<Slot>("slot");
                    Slots[i] = slot;
                }
                ghostIcon = container.CreateChild("ghostIcon");
                yield break;
            }
            
            var inventory = container.Q<VisualElement>(name: "inventory-window");
            // inventory.AddManipulator(new PanelDragManipulator()); // 전체화면 중앙 UI이므로 드래그는 임시 비활성화
            
            var headerLabel = inventory.Q<Label>(name: "inventoryHeader");
            if (headerLabel != null) {
                headerLabel.text = panelName.ToUpper(); // INVENTORY 등 대문자로
            }

            var slotsContainer = inventory.Q<VisualElement>(name: "slotsContainer");
            var existingSlots = slotsContainer.Query<Slot>().ToList();

            if (existingSlots.Count == size) {
                for (int i = 0; i < size; i++) {
                    Slots[i] = existingSlots[i];
                }
            } else {
                slotsContainer.Clear();
                for (int i = 0; i < size; i++) {
                    var slot = slotsContainer.CreateChild<Slot>("slot");
                    Slots[i] = slot;
                }
            }
            
            ghostIcon = container.Q<VisualElement>(className: "ghostIcon");
            
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

