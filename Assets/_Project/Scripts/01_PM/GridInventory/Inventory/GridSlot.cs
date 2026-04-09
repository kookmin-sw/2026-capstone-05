using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    [UxmlElement]
    public partial class GridSlot : VisualElement {
        public int Index => parent?.IndexOf(this) ?? -1;

        public GridSlot() {
            AddToClassList("slot");
        }
    }
}

