using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 모닥불 화면 안의 던전 지도와 방향 버튼 (표현/입력만 담당. 던전 구조와 이동은 BattleManager)
// 지도에는 가 본 방과, 가 본 방에서 길이 보이는 방만 그려짐 (안 가 본 방은 종류를 숨기고 "?"로 표시). BattleManager가 모닥불 화면을 처음 열 때 붙임.
public class DungeonMapPanel : MonoBehaviour
{
    [Header("지도")]
    public float mapHeight = 230f;         // 지도 영역 높이
    public float mapWidth = 780f;          // 지도 영역 너비 (창 안쪽 너비보다 작게)
    public float maxCellSpacing = 72f;     // 방 사이 간격의 최댓값 (방이 많으면 줄어듦)
    [Range(0.3f, 0.9f)]
    public float cellSizeRatio = 0.6f;     // 간격 대비 방 칸 크기
    public Color currentRoomColor  = new Color(1f, 0.85f, 0.3f);    // 지금 있는 방
    public Color clearedRoomColor  = new Color(0.62f, 0.52f, 0.4f); // 정리한 방
    public Color unknownRoomColor  = new Color(0.22f, 0.2f, 0.2f);  // 아직 안 간 방 (종류를 숨김)
    public Color normalRoomColor   = new Color(0.3f, 0.27f, 0.26f); // 가 봤지만 아직 정리하지 않은 방 (종류별)
    public Color restRoomColor     = new Color(0.25f, 0.5f, 0.3f);
    public Color treasureRoomColor = new Color(0.7f, 0.55f, 0.15f);
    public Color waveRoomColor     = new Color(0.45f, 0.3f, 0.6f);
    public Color bossRoomColor     = new Color(0.8f, 0.25f, 0.2f);
    public Color pathColor        = new Color(0.45f, 0.38f, 0.3f);

    [Header("방향 버튼")]
    public float buttonHeight = 90f;
    public float buttonFontSize = 28f;
    public Color buttonColor = new Color(0.42f, 0.26f, 0.13f, 1f);
    public Color bossButtonColor = new Color(0.55f, 0.18f, 0.14f, 1f);

    private RectTransform mapRoot;
    private RectTransform buttonRow;
    private TMP_FontAsset font;
    private BattleManager battle;

    // 지금 방 기준으로 지도와 방향 버튼을 다시 그림
    public void Show(BattleManager manager, TextMeshProUGUI fontSource)
    {
        battle = manager;
        if (fontSource != null) font = fontSource.font;
        EnsureBuilt(fontSource);
        if (battle == null || battle.Layout == null) return;

        DrawMap();
        BuildButtons();
    }

    // 창 안에 지도 영역과 방향 버튼 줄을 만듦 (상태 글 바로 아래)
    void EnsureBuilt(TextMeshProUGUI statusText)
    {
        if (mapRoot != null) return;

        Transform window = transform.Find("Window");
        if (window == null) window = transform;
        int insertIndex = statusText != null && statusText.transform.parent == window
            ? statusText.transform.GetSiblingIndex() + 1
            : window.childCount;

        mapRoot = CreateRect("Map", window);
        mapRoot.SetSiblingIndex(insertIndex);
        mapRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = mapHeight;

        buttonRow = CreateRect("Row_Directions", window);
        buttonRow.SetSiblingIndex(insertIndex + 1);
        buttonRow.gameObject.AddComponent<LayoutElement>().preferredHeight = buttonHeight;
        HorizontalLayoutGroup row = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 12f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        // 방향을 고르지 않는 예전 [다음 방으로] 버튼은 숨김
        Transform oldContinue = window.Find("Btn_Continue");
        if (oldContinue != null) oldContinue.gameObject.SetActive(false);
    }

    // ─── 지도 ───────────────────────────────────────────────

    void DrawMap()
    {
        Clear(mapRoot);

        DungeonLayout layout = battle.Layout;
        int current = battle.currentStageIndex;

        // 보이는 방: 가 본 방 + 가 본 방에서 길이 이어진 방
        HashSet<int> shown = new HashSet<int>();
        for (int i = 0; i < layout.rooms.Count; i++)
        {
            if (!battle.IsRoomVisited(i)) continue;
            shown.Add(i);
            foreach (int next in layout.rooms[i].neighbors)
                if (next >= 0) shown.Add(next);
        }
        if (shown.Count == 0) return;

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (int i in shown)
        {
            DungeonRoom room = layout.rooms[i];
            if (room.x < minX) minX = room.x;
            if (room.x > maxX) maxX = room.x;
            if (room.y < minY) minY = room.y;
            if (room.y > maxY) maxY = room.y;
        }

        // 지도 영역에 다 들어가도록 간격을 정하고 가운데 정렬
        float spacing = Mathf.Min(maxCellSpacing, mapWidth / (maxX - minX + 1), mapHeight / (maxY - minY + 1));
        float cell = spacing * cellSizeRatio;
        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 Position(DungeonRoom room) => new Vector2((room.x - center.x) * spacing, (room.y - center.y) * spacing);

        // 길 (가 본 방에서 나가는 길만. 방 칸보다 먼저 그려 뒤에 깔리게 함)
        foreach (int i in shown)
        {
            if (!battle.IsRoomVisited(i)) continue;
            for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
            {
                int next = layout.rooms[i].neighbors[d];
                if (next < 0 || (battle.IsRoomVisited(next) && next < i)) continue; // 양쪽 다 가 본 길은 한 번만

                Vector2 a = Position(layout.rooms[i]), b = Position(layout.rooms[next]);
                bool horizontal = d == DungeonLayoutRules.East || d == DungeonLayoutRules.West;
                RectTransform path = CreateRect("Path", mapRoot);
                path.gameObject.AddComponent<Image>().color = pathColor;
                path.anchoredPosition = (a + b) * 0.5f;
                path.sizeDelta = horizontal ? new Vector2(spacing, cell * 0.18f) : new Vector2(cell * 0.18f, spacing);
            }
        }

        // 방
        foreach (int i in shown)
        {
            DungeonRoomType type = layout.rooms[i].type;
            bool known = i == current || battle.IsRoomTypeKnown(i); // 육감이면 이어진 방도 보임
            Color color = i == current ? currentRoomColor
                        : battle.IsRoomCleared(i) ? clearedRoomColor
                        : known ? GetTypeColor(type)
                        : unknownRoomColor;
            string label = known ? DungeonLayoutRules.GetRoomTypeName(type) : "?";

            RectTransform room = CreateRect($"Room_{i}", mapRoot);
            room.gameObject.AddComponent<Image>().color = color;
            room.anchoredPosition = Position(layout.rooms[i]);
            room.sizeDelta = new Vector2(cell, cell);

            // 글자 수가 달라도 칸 안에 들어가도록 크기를 자동으로 맞춤
            TextMeshProUGUI text = CreateText("Txt", room, label, cell * 0.4f);
            text.color = i == current ? Color.black : Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 6f;
            text.fontSizeMax = cell * 0.4f;
            Stretch(text.rectTransform);
        }
    }

    Color GetTypeColor(DungeonRoomType type)
    {
        switch (type)
        {
            case DungeonRoomType.Rest:     return restRoomColor;
            case DungeonRoomType.Treasure: return treasureRoomColor;
            case DungeonRoomType.Wave:     return waveRoomColor;
            case DungeonRoomType.Boss:     return bossRoomColor;
            default:                       return normalRoomColor;
        }
    }

    // ─── 방향 버튼 ──────────────────────────────────────────

    void BuildButtons()
    {
        Clear(buttonRow);

        DungeonLayout layout = battle.Layout;
        int current = battle.currentStageIndex;

        for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
        {
            int next = layout.GetNeighbor(current, d);
            if (next < 0) continue;

            // 안 가 본 방은 종류를 숨김 (보스방도 들어가기 전까지는 모름)
            DungeonRoomType type = layout.rooms[next].type;
            bool known = battle.IsRoomTypeKnown(next);
            bool boss = known && type == DungeonRoomType.Boss;
            string typeName = DungeonLayoutRules.GetRoomTypeName(type);
            string what = !known ? "?" : battle.IsRoomCleared(next) ? $"지나온 방 ({typeName})" : typeName;

            RectTransform rect = CreateRect($"Btn_{d}", buttonRow);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = boss ? bossButtonColor : buttonColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            int direction = d;
            button.onClick.AddListener(() => battle.OnClickMove(direction));

            TextMeshProUGUI text = CreateText("Txt_Label", rect, $"{DungeonLayoutRules.GetDirectionName(d)}\n<size=75%>{what}</size>", buttonFontSize);
            Stretch(text.rectTransform);
        }
    }

    // ─── 도우미 ─────────────────────────────────────────────

    // 지워질 때까지 한 프레임이 걸리므로, 그 사이 배치에 끼어들지 않게 먼저 끔
    static void Clear(Transform parent)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    TextMeshProUGUI CreateText(string objectName, Transform parent, string message, float size)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = message;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
