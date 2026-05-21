using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.GridInventory {
    /// <summary>Runtime UIToolkit quantity dialog; loads UXML from Resources/QuantityPopup.</summary>
    public sealed class QuantityPopupView : MonoBehaviour {
        static QuantityPopupView _instance;

        UIDocument _document;

        VisualElement overlay;
        SliderInt sliderQty;
        Label lblQtyValue;

        Button btnQtyMinus;
        Button btnQtyPlus;
        Button btnQtyConfirm;
        Button btnQtyCancel;

        Action<int> pendingConfirm;

        bool wired;

        void Awake() {
            if (_instance != null && _instance != this) {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();

            EnsureDocumentConfigured();
            WireIfNeeded();
        }

        void OnDestroy() {
            if (_instance == this) _instance = null;
        }

        static UIDocument FindPanelDocument() =>
            UnityEngine.Object.FindFirstObjectByType<UIDocument>();

        void EnsureDocumentConfigured() {
            if (_document.visualTreeAsset == null) {
                var vta = Resources.Load<VisualTreeAsset>("QuantityPopup");
                if (vta == null) {
                    Debug.LogError("[QuantityPopupView] Missing Resources asset 'QuantityPopup'.");
                    return;
                }

                _document.visualTreeAsset = vta;
            }

            if (_document.panelSettings == null) {
                var seed = FindPanelDocument();
                if (seed != null && seed.panelSettings != null) {
                    _document.panelSettings = seed.panelSettings;
                    _document.sortingOrder = Mathf.Max(seed.sortingOrder + 500, _document.sortingOrder);
                } else {
                    Debug.LogWarning("[QuantityPopupView] No PanelSettings; assign in inspector or ensure another UIDocument exists.");
                }
            }
        }

        void WireIfNeeded() {
            if (wired || _document == null) return;

            VisualElement root = _document.rootVisualElement;
            overlay = root.Q<VisualElement>("quantity-popup-overlay") ?? root;
            sliderQty = root.Q<SliderInt>("slider-qty");
            lblQtyValue = root.Q<Label>("lbl-qty-value");
            btnQtyMinus = root.Q<Button>("btn-qty-minus");
            btnQtyPlus = root.Q<Button>("btn-qty-plus");
            btnQtyConfirm = root.Q<Button>("btn-qty-confirm");
            btnQtyCancel = root.Q<Button>("btn-qty-cancel");

            if (sliderQty == null || btnQtyConfirm == null) {
                Debug.LogError("[QuantityPopupView] UXML controls missing.");
                return;
            }

            btnQtyMinus.clicked += () => {
                if (sliderQty.value > sliderQty.lowValue) sliderQty.value--;
            };
            btnQtyPlus.clicked += () => {
                if (sliderQty.value < sliderQty.highValue) sliderQty.value++;
            };
            sliderQty.RegisterValueChangedCallback(evt => {
                if (lblQtyValue != null) lblQtyValue.text = evt.newValue.ToString();
            });
            btnQtyCancel.clicked += Hide;
            btnQtyConfirm.clicked += () => {
                int value = sliderQty.value;
                var cb = pendingConfirm;
                Hide();
                cb?.Invoke(value);
            };

            wired = true;
            if (overlay != null) overlay.style.display = DisplayStyle.None;
            FixedAspectRatioManager.RequestRefresh();
        }

        public static QuantityPopupView Instance => _instance;

        public static void EnsureExists() {
            if (_instance != null) return;

            var seed = FindPanelDocument();
            if (seed == null || seed.panelSettings == null) {
                Debug.LogWarning("[QuantityPopupView] Deferred: open any UI Toolkit document first.");
                return;
            }

            var go = new GameObject(nameof(QuantityPopupView));
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = seed.panelSettings;
            doc.sortingOrder = Mathf.Max(seed.sortingOrder + 500, 8);
            go.AddComponent<QuantityPopupView>();
            FixedAspectRatioManager.RequestRefresh();
        }

        public static void Show(int maxQuantityInclusive, Action<int> onConfirm) {
            if (maxQuantityInclusive < 1 || onConfirm == null) return;

            EnsureExists();
            if (_instance == null) return;

            _instance.ShowInternal(maxQuantityInclusive, onConfirm);
        }

        void ShowInternal(int maxQuantityInclusive, Action<int> onConfirm) {
            EnsureDocumentConfigured();
            WireIfNeeded();
            if (overlay == null || sliderQty == null) return;

            pendingConfirm = onConfirm;
            sliderQty.lowValue = 1;
            sliderQty.highValue = maxQuantityInclusive;
            sliderQty.value = 1;
            if (lblQtyValue != null) lblQtyValue.text = "1";
            overlay.style.display = DisplayStyle.Flex;
            FixedAspectRatioManager.RequestRefresh();
        }

        void Hide() {
            if (overlay != null) overlay.style.display = DisplayStyle.None;
            FixedAspectRatioManager.RequestRefresh();
            pendingConfirm = null;
        }
    }
}
