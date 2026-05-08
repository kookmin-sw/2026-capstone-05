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
    private readonly Dictionary<int, List<TMP_Text>> _startSlotDateTmpTexts = new();
    private readonly Dictionary<int, List<TMP_Text>> _startSlotDayTmpTexts = new();
    private readonly Dictionary<int, List<Text>> _startSlotDateLegacyTexts = new();
    private readonly Dictionary<int, List<Text>> _startSlotDayLegacyTexts = new();

    internal LoadGameMenuController(RoomLauncher roomLauncher)
    {
        _roomLauncher = roomLauncher;
    }

    internal bool BindAndRefresh()
    {
        bool hasLoadUi = BindLoadFlowUi();
        RefreshSlotUi();
        return hasLoadUi;
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

            SetAllText(_startSlotDateTmpTexts, slot, dateLabel);
            SetAllText(_startSlotDayTmpTexts, slot, dayLabel);
            SetAllText(_startSlotDateLegacyTexts, slot, dateLabel);
            SetAllText(_startSlotDayLegacyTexts, slot, dayLabel);
        }
    }

    private bool BindLoadFlowUi()
    {
        _loadSlotButtons.Clear();
        _loadSlotDateTmpTexts.Clear();
        _loadSlotDayTmpTexts.Clear();
        _loadSlotDateLegacyTexts.Clear();
        _loadSlotDayLegacyTexts.Clear();
        _startSlotDateTmpTexts.Clear();
        _startSlotDayTmpTexts.Clear();
        _startSlotDateLegacyTexts.Clear();
        _startSlotDayLegacyTexts.Clear();

        BindLoadSlotUi(1, "Gmae1 Load Box", "Game1 Load Box", "Main_menu1", "Main_Menu1");
        BindLoadSlotUi(2, "Gmae2 Load Box", "Game2 Load Box", "Main_menu2", "Main_Menu2");
        BindLoadSlotUi(3, "Gmae3 Load Box", "Game3 Load Box", "Main_menu3", "Main_Menu3");

        BindDisplaySlotUi(_startSlotDateTmpTexts, _startSlotDayTmpTexts, _startSlotDateLegacyTexts, _startSlotDayLegacyTexts, 1, "Gmae1 Start Box", "Game1 Start Box", "Start Game Box1", "Main_menu1", "Main_Menu1");
        BindDisplaySlotUi(_startSlotDateTmpTexts, _startSlotDayTmpTexts, _startSlotDateLegacyTexts, _startSlotDayLegacyTexts, 2, "Gmae2 Start Box", "Game2 Start Box", "Start Game Box2", "Main_menu2", "Main_Menu2");
        BindDisplaySlotUi(_startSlotDateTmpTexts, _startSlotDayTmpTexts, _startSlotDateLegacyTexts, _startSlotDayLegacyTexts, 3, "Gmae3 Start Box", "Game3 Start Box", "Start Game Box3", "Main_menu3", "Main_Menu3");

        return _loadSlotButtons.Count > 0 ||
               _loadSlotDateTmpTexts.Count > 0 ||
               _loadSlotDayTmpTexts.Count > 0 ||
               _loadSlotDateLegacyTexts.Count > 0 ||
               _loadSlotDayLegacyTexts.Count > 0 ||
               _startSlotDateTmpTexts.Count > 0 ||
               _startSlotDayTmpTexts.Count > 0 ||
               _startSlotDateLegacyTexts.Count > 0 ||
               _startSlotDayLegacyTexts.Count > 0;
    }

    private void BindLoadSlotUi(int slot, params string[] containerCandidates)
    {
        IReadOnlyList<Transform> containers = FindNamedContainers(containerCandidates);
        if (containers.Count == 0) return;

        foreach (Transform container in containers)
        {
            Button button = FindLoadButton(container);
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

    private static void BindDisplaySlotUi(
        Dictionary<int, List<TMP_Text>> dateTmpMap,
        Dictionary<int, List<TMP_Text>> dayTmpMap,
        Dictionary<int, List<Text>> dateLegacyMap,
        Dictionary<int, List<Text>> dayLegacyMap,
        int slot,
        params string[] containerCandidates)
    {
        IReadOnlyList<Transform> containers = FindNamedContainers(containerCandidates);
        if (containers.Count == 0) return;

        foreach (Transform container in containers)
        {
            foreach (TMP_Text text in container.GetComponentsInChildren<TMP_Text>(true))
            {
                if (ContainsAny(text.name, new[] { "Date" })) AddText(dateTmpMap, slot, text);
                else if (ContainsAny(text.name, new[] { "Day" })) AddText(dayTmpMap, slot, text);
            }

            foreach (Text text in container.GetComponentsInChildren<Text>(true))
            {
                if (ContainsAny(text.name, new[] { "Date" })) AddText(dateLegacyMap, slot, text);
                else if (ContainsAny(text.name, new[] { "Day" })) AddText(dayLegacyMap, slot, text);
            }
        }
    }


    private static Button FindLoadButton(Transform container)
    {
        if (container == null)
            return null;

        Button[] buttons = container.GetComponentsInChildren<Button>(true);
        foreach (Button candidate in buttons)
        {
            if (ContainsAny(candidate.name, new[] { "Load", "불러오기" }))
                return candidate;

            TMP_Text label = candidate.GetComponentInChildren<TMP_Text>(true);
            if (label != null && ContainsAny(label.text, new[] { "Load", "불러오기" }))
                return candidate;

            Text legacyLabel = candidate.GetComponentInChildren<Text>(true);
            if (legacyLabel != null && ContainsAny(legacyLabel.text, new[] { "Load", "불러오기" }))
                return candidate;
        }

        return null;
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
