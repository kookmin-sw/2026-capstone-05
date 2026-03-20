using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.Inventory {
    [UxmlElement]
    public partial class Slot : VisualElement {
        public int Index => parent?.IndexOf(this) ?? -1;

        public Slot() {
            AddToClassList("slot");
        }
    }
}