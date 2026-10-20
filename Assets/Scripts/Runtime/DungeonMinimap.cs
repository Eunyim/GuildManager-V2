using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 던전 화면 우상단에 항상 떠 있는 미니맵. 지금 방과 바로 옆 방(최대 4개)만 그림
// 아직 들어가 보지 않은 방은 "?", 들어가 본 방은 방 종류 아이콘. 지금 방이 바뀌거나 새 방을 가 보면 다시 그림
// BattleManager가 첫 방에 들어갈 때 화면 캔버스에 붙임 (씬/프리팹은 건드리지 않음)
public class DungeonMinimap : MonoBehaviour
{
    [Header("배치")]
    public Vector2 margin = new Vector2(24f, 24f); // 화면 오른쪽 위 모서리에서 떨어진 거리
    public float cellSize = 56f;                   // 방 칸 크기
    public float cellGap = 22f;                    // 방 칸 사이 간격 (길이 그려지는 자리)
    public float iconFontSize = 34f;

    [Header("색")]
    public Color backgroundColor    = new Color(0f, 0f, 0f, 0.45f);
    public Color unknownRoomColor   = new Color(0.22f, 0.2f, 0.2f, 0.95f);
    public Color normalRoomColor    = new Color(0.4f, 0.34f, 0.3f, 0.95f);
    public Color restRoomColor      = new Color(0.25f, 0.5f, 0.3f, 0.95f);
    public Color treasureRoomColor  = new Color(0.7f, 0.55f, 0.15f, 0.95f);
    public Color waveRoomColor      = new Color(0.45f, 0.3f, 0.6f, 0.95f);
    public Color bossRoomColor      = new Color(0.8f, 0.25f, 0.2f, 0.95f);
    public Color currentBorderColor = new Color(1f, 0.85f, 0.3f, 1f);
    public Color pathColor          = new Color(0.6f, 0.52f, 0.42f, 1f);

    // 아이콘은 던전 UI 글꼴(DungGeunMo, 고정 글자표)에 들어 있는 기호만 씀
    public const string UnknownIcon = "?";
    public static string GetIcon(DungeonRoomType type, bool isStart)
    {
        if (isStart) return "⌂"; // ⌂ 입구
        switch (type)
        {
            case DungeonRoomType.Rest:     return "♨"; // ♨
            case DungeonRoomType.Treasure: return "$";
            case DungeonRoomType.Wave:     return "‼"; // ‼
            case DungeonRoomType.Boss:     return "☠"; // ☠
            default:                       return "†"; // † 전투
        }
    }

    BattleManager battle;
    TMP_FontAsset font;
    RectTransform root;
    int drawnRoom = -2, drawnVisitedCount = -1;

    public void Setup(BattleManager manager, TMP_FontAsset fontAsset)
    {
        battle = manager;
        font = fontAsset;

        // 다른 창(모닥불, 결과창)이 위에 그려지도록 캔버스 맨 아래(첫 번째)에 둠
        root = (RectTransform)transform;
        root.SetAsFirstSibling();
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        float side = cellSize * 3f + cellGap * 2f + 16f;
        root.sizeDelta = new Vector2(side, side);
        root.anchoredPosition = new Vector2(-margin.x, -margin.y);
        Image bg = gameObject.AddComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;
    }

    void Update()
    {
        if (battle == null || battle.Layout == null || battle.Layout.rooms.Count == 0) return;
        int current = battle.currentStageIndex;
        int visited = 0;
        for (int i = 0; i < battle.Layout.rooms.Count; i++) if (battle.IsRoomVisited(i)) visited++;
        if (current == drawnRoom && visited == drawnVisitedCount) return;
        drawnRoom = current;
        drawnVisitedCount = visited;
        Redraw();
    }

    void Redraw()
    {
        foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }

        DungeonLayout layout = battle.Layout;
        int current = battle.currentStageIndex;
        if (current < 0 || current >= layout.rooms.Count) return;
        DungeonRoom here = layout.rooms[current];
        float step = cellSize + cellGap;

        // 길 먼저 (방 칸 뒤에 깔리게)
        for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
        {
            int next = here.neighbors[d];
            if (next < 0) continue;
            Vector2 offset = Offset(here, layout.rooms[next]) * step;
            bool horizontal = d == DungeonLayoutRules.East || d == DungeonLayoutRules.West;
            RectTransform path = CreateRect("Path", root);
            Image pathImage = path.gameObject.AddComponent<Image>();
            pathImage.color = pathColor;
            pathImage.raycastTarget = false;
            path.anchoredPosition = offset * 0.5f;
            path.sizeDelta = horizontal ? new Vector2(step, cellSize * 0.16f) : new Vector2(cellSize * 0.16f, step);
        }

        // 바로 옆 방
        for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
        {
            int next = here.neighbors[d];
            if (next < 0) continue;
            DrawRoom(next, Offset(here, layout.rooms[next]) * step, false);
        }

        // 지금 방 (테두리로 강조)
        DrawRoom(current, Vector2.zero, true);
    }

    static Vector2 Offset(DungeonRoom from, DungeonRoom to) =>
        new Vector2(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));

    void DrawRoom(int index, Vector2 position, bool isCurrent)
    {
        DungeonLayout layout = battle.Layout;
        bool known = isCurrent || battle.IsRoomTypeKnown(index); // 육감이면 이어진 방도 보임
        DungeonRoomType type = layout.rooms[index].type;

        if (isCurrent)
        {
            RectTransform border = CreateRect("Border", root);
            Image borderImage = border.gameObject.AddComponent<Image>();
            borderImage.color = currentBorderColor;
            borderImage.raycastTarget = false;
            border.anchoredPosition = position;
            border.sizeDelta = new Vector2(cellSize + 8f, cellSize + 8f);
        }

        RectTransform cell = CreateRect($"Room_{index}", root);
        Image image = cell.gameObject.AddComponent<Image>();
        image.color = known ? GetTypeColor(type) : unknownRoomColor;
        image.raycastTarget = false;
        cell.anchoredPosition = position;
        cell.sizeDelta = new Vector2(cellSize, cellSize);

        RectTransform textRect = CreateRect("Icon", cell);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = known ? GetIcon(type, index == layout.startRoom) : UnknownIcon;
        text.fontSize = iconFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
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

    static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        return rect;
    }
}
