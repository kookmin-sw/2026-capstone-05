using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class LoadGameMenuController
{
    private readonly RoomLauncher _roomLauncher;
    private readonly Dictionary<int, Button> _loadSlotButtons = new();
    private readonly Dictionary<int, List<TMP_Text>> _loadSlotDateTmpTexts = new();
    private readonly Dictionary<int, List<TMP_Text>> _loadSlotDayTmpTexts = new();
    private readonly Dictionary<int, List<Text>> _loadSlotDateLegacyTexts = new();
    private readonly Dictionary<int, List<Text>> _loadSlotDayLegacyTexts = new();

    internal LoadGameMenuController(RoomLauncher roomLauncher)
    {
        _roomLauncher = roomLauncher;
    }

    internal void BindAndRefresh()
    {
        BindLoadFlowUi();
        RefreshSlotUi();
    }

    internal void RefreshSlotUi()
    {
        for (int slot = 1; slot <= 3; slot++)
        {
            int round = Mathf.Max(1, PlayerPrefs.GetInt(RoomLauncher.BuildHostRoundCountPrefKey(slot), 1));
            string date = PlayerPrefs.GetString(RoomLauncher.BuildHostSaveDatePrefKey(slot), "-");

            string dateLabel = date == "-" ? "Date -" : $"Date {date}";
            string dayLabel = $"Day {round}";

            SetAllText(_loadSlotDateTmpTexts, slot, dateLabel);
            SetAllText(_loadSlotDayTmpTexts, slot, dayLabel);
            SetAllText(_loadSlotDateLegacyTexts, slot, dateLabel);
            SetAllText(_loadSlotDayLegacyTexts, slot, dayLabel);
        }
    }

    private void BindLoadFlowUi()
    {
        _loadSlotButtons.Clear();
        _loadSlotDateTmpTexts.Clear();
        _loadSlotDayTmpTexts.Clear();
        _loadSlotDateLegacyTexts.Clear();
        _loadSlotDayLegacyTexts.Clear();

        BindLoadSlotUi(1, "Gmae1 Load Box", "Start Game Box1", "Gmae1 Start Box", "Main_menu1");
        BindLoadSlotUi(2, "Gmae2 Load Box", "Start Game Box2", "Gmae2 Start Box", "Main_menu2");
        BindLoadSlotUi(3, "Gmae3 Load Box", "Start Game Box3", "Gmae3 Start Box", "Main_menu3");
    }

    private void BindLoadSlotUi(int slot, params string[] containerCandidates)
    {
        IReadOnlyList<Transform> containers = FindNamedContainers(containerCandidates);
        if (containers.Count == 0) return;

        foreach (Transform container in containers)
        {
            Button button = container.GetComponentInChildren<Button>(true);
            if (button != null && !_loadSlotButtons.ContainsKey(slot))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnLoadSlotButtonClicked(slot));
                _loadSlotButtons[slot] = button;
            }

            foreach (TMP_Text text in container.GetComponentsInChildren<TMP_Text>(true))
            {
                if (ContainsAny(text.name, new[] { "Date" })) AddText(_loadSlotDateTmpTexts, slot, text);
                else if (ContainsAny(text.name, new[] { "Day" })) AddText(_loadSlotDayTmpTexts, slot, text);
            }

            foreach (Text text in container.GetComponentsInChildren<Text>(true))
            {
                if (ContainsAny(text.name, new[] { "Date" })) AddText(_loadSlotDateLegacyTexts, slot, text);
                else if (ContainsAny(text.name, new[] { "Day" })) AddText(_loadSlotDayLegacyTexts, slot, text);
            }
        }
    }

    private void OnLoadSlotButtonClicked(int slot)
    {
        if (!_roomLauncher.CanProceedMenuAction($"불러오기 슬롯 {slot}"))
            return;

        _roomLauncher.StartLoadedHostGameForSlot(slot);
    }


    private static IReadOnlyList<Transform> FindNamedContainers(params string[] containerCandidates)
    {
        List<Transform> results = new();
        if (containerCandidates == null || containerCandidates.Length == 0)
            return results;

        Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
        foreach (Transform target in transforms)
        {
            if (ContainsAny(target.name, containerCandidates))
                results.Add(target);
        }

        return results;
    }

    private static bool ContainsAny(string source, IEnumerable<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (source.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static void AddText<T>(Dictionary<int, List<T>> map, int slot, T text)
    {
        if (!map.TryGetValue(slot, out List<T> list))
        {
            list = new List<T>();
            map[slot] = list;
        }

        list.Add(text);
    }

    private static void SetAllText<T>(Dictionary<int, List<T>> map, int slot, string value) where T : Component
    {
        if (!map.TryGetValue(slot, out List<T> list))
            return;

        foreach (T item in list)
        {
            switch (item)
            {
                case TMP_Text tmp:
                    tmp.text = value;
                    break;
                case Text legacy:
                    legacy.text = value;
                    break;
            }
        }
    }
}
