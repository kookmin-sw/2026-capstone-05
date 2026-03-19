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
                var inv = container.CreateChild("inventory").WithManipulator(new PanelDragManipulator());
                inv.CreateChild("inventoryFrame");
                inv.CreateChild("inventoryHeader").Add(new Label(panelName));
                var sContainer = inv.CreateChild("slotsContainer");
                for (int i = 0; i < size; i++) {
                    var slot = sContainer.CreateChild<Slot>("slot");
                    Slots[i] = slot;
                }
                ghostIcon = container.CreateChild("ghostIcon");
                yield break;
            }
            
            var inventory = container.Q<VisualElement>(className: "inventory");
            inventory.AddManipulator(new PanelDragManipulator());
            
            var headerLabel = inventory.Q<Label>(className: "inventoryHeader");
            if (headerLabel != null) {
                headerLabel.text = panelName;
            }

            var slotsContainer = inventory.Q<VisualElement>(className: "slotsContainer");
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
    }
}
