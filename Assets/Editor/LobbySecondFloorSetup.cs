using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 로비 2층 여관 세팅
/// 1층 구석에 계단을 놓고, 계단으로 오갈 수 있는 2층 여관(방 문 + 레벨 안내판 + 내려가는 계단)을 만듭니다.
/// 여관 업그레이드는 길드 밖 건축 공방에서 합니다.
/// 방 하나에 파티 하나가 묵으며, 방 수는 GameManager의 partiesPerInnLevel(여관 레벨별)로 정해집니다.
/// 이미 있는 오브젝트는 건너뛰므로, 위치를 옮기거나 스프라이트를 바꾼 뒤 다시 실행해도 안전합니다.
/// (먼저 Tools > 로비 시설 오브젝트 배치가 실행되어 있어야 합니다)
/// </summary>
public class LobbySecondFloorSetup : Editor
{
    const string FLOOR2_NAME   = "Lobby_Floor2";
    const string SWITCHER_NAME = "Lobby_FloorSwitcher";
    const string ROOM_MENU_NAME = "Popup_InnRoomMenu";
    const string UNDO_NAME     = "로비 2층 여관 세팅";
    const int    DEFAULT_ROOMS = 5;
    const int    LABEL_SORTING = 1000; // 방 이름표는 모든 시설 위에

    // 바닥 구석 칸 (아이소메트릭 바닥의 오른쪽 꼭짓점)
    static readonly Vector2Int stairsCell  = new Vector2Int(7, -5);
    static readonly Vector2Int signCell    = new Vector2Int(-2, -3);
    // 방 문은 바닥 위쪽 모서리(y = 4)를 따라 일정 간격으로 배치
    const int ROOM_ROW = 4;
    const int ROOM_X_MIN = -6;
    const int ROOM_X_MAX = 6;

    static readonly Color stairsColor  = new Color(0.60f, 0.60f, 0.65f);
    static readonly Color roomColor    = new Color(0.62f, 0.42f, 0.24f);
    static readonly Color signColor    = new Color(0.85f, 0.65f, 0.30f);

    [MenuItem("Tools/로비 2층 여관 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[LobbySecondFloorSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        LobbyMapGenerator gen = Object.FindFirstObjectByType<LobbyMapGenerator>();
        GameObject floor1 = GameObject.Find(LobbyFacilitySetup.ROOT_NAME);
        if (lobby == null || gen == null || gen.floorTilemap == null || floor1 == null)
        {
            Debug.LogError("[LobbySecondFloorSetup] LobbyManager / LobbyMapGenerator / Lobby_Facilities를 찾을 수 없습니다. " +
                           "LobbyScene을 열고 Tools > 로비 시설 오브젝트 배치를 먼저 실행해주세요.");
            return;
        }

        Scene scene = lobby.gameObject.scene;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        // 1. 2층 묶음 (꺼져 있을 수 있으므로 씬 루트에서 직접 찾음)
        GameObject floor2 = FindRoot(scene, FLOOR2_NAME);
        if (floor2 == null)
        {
            floor2 = new GameObject(FLOOR2_NAME);
            Undo.RegisterCreatedObjectUndo(floor2, UNDO_NAME);
        }
        InnFloor inn = floor2.GetComponent<InnFloor>();
        if (inn == null) inn = Undo.AddComponent<InnFloor>(floor2);

        // 2. 층 전환기
        GameObject switcherGO = FindRoot(scene, SWITCHER_NAME);
        if (switcherGO == null)
        {
            switcherGO = new GameObject(SWITCHER_NAME);
            Undo.RegisterCreatedObjectUndo(switcherGO, UNDO_NAME);
        }
        LobbyFloorSwitcher switcher = switcherGO.GetComponent<LobbyFloorSwitcher>();
        if (switcher == null) switcher = Undo.AddComponent<LobbyFloorSwitcher>(switcherGO);
        switcher.firstFloorRoot  = floor1;
        switcher.secondFloorRoot = floor2;
        switcher.chibiLayer      = lobby.lobbyFloor;
        EditorUtility.SetDirty(switcher);

        // 3. 계단 (1층: 올라가기 / 2층: 내려가기)
        InteractiveObject stairsUp = CreateObject(gen, floor1.transform, "Facility_StairsUp", "계단 (2층 여관으로)", stairsCell, stairsColor, out bool upCreated);
        if (upCreated) UnityEventTools.AddPersistentListener(stairsUp.onClickAction, switcher.GoUpstairs);

        InteractiveObject stairsDown = CreateObject(gen, floor2.transform, "Facility_StairsDown", "계단 (1층 길드 홀로)", stairsCell, stairsColor, out bool downCreated);
        if (downCreated) UnityEventTools.AddPersistentListener(stairsDown.onClickAction, switcher.GoDownstairs);

        // 4. 방 문 (최대 방 수만큼)
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        int roomCount = DEFAULT_ROOMS;
        if (gm != null && gm.partiesPerInnLevel != null && gm.partiesPerInnLevel.Length > 0)
            roomCount = Mathf.Max(gm.partiesPerInnLevel);

        inn.rooms = new InnFloor.Room[roomCount];
        for (int i = 0; i < roomCount; i++)
        {
            float t = roomCount > 1 ? i / (float)(roomCount - 1) : 0.5f;
            Vector2Int cell = new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(ROOM_X_MIN, ROOM_X_MAX, t)), ROOM_ROW);

            InteractiveObject room = CreateObject(gen, floor2.transform, $"Inn_Room{i + 1}", $"{i + 1}번 방", cell, roomColor, out bool roomCreated);
            if (roomCreated) UnityEventTools.AddIntPersistentListener(room.onClickAction, inn.OnClickRoom, i);

            inn.rooms[i] = new InnFloor.Room
            {
                interactive = room,
                label = EnsureWorldLabel(room.transform, $"{i + 1}번 방", font),
            };
        }

        // 5. 예전 업그레이드 카운터 제거 (여관 업그레이드는 건축 공방으로 이동)
        Transform legacyCounter = floor2.transform.Find("Inn_Counter");
        if (legacyCounter != null)
        {
            Undo.DestroyObjectImmediate(legacyCounter.gameObject);
            Debug.Log("[LobbySecondFloorSetup] 예전 여관 업그레이드 카운터 제거");
        }

        // 5-1. 여관 안내판 (현재 레벨과 방 사용 현황 표시)
        InteractiveObject sign = CreateObject(gen, floor2.transform, "Inn_Sign", "여관", signCell, signColor, out bool signCreated);
        if (signCreated) UnityEventTools.AddPersistentListener(sign.onClickAction, inn.OnClickSign);
        inn.signObject = sign;
        inn.signLabel  = EnsureWorldLabel(sign.transform, "여관 Lv.1", font);
        EditorUtility.SetDirty(inn);

        // 6. 정렬 순서: 아래쪽(앞)에 있는 오브젝트가 위에 그려지도록
        foreach (GameObject floorRoot in new[] { floor1, floor2 })
        {
            foreach (SpriteRenderer sr in floorRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder = LobbyFacilitySetup.GetSortingOrder(sr.transform.position.y);
                EditorUtility.SetDirty(sr);
            }
        }

        // 7. 알림 UI (잠긴 방, 빈 방 없음, 업그레이드 결과 등을 화면에 표시)
        EnsureNotificationPanel(lobby, font);

        // 8. 파티가 묵고 있는 방을 눌렀을 때의 선택창 (파티 관리 / 방 비우기)
        EnsureRoomMenuPopup(lobby, font);

        // 2층은 계단을 오르기 전까지 꺼 둠
        floor2.SetActive(false);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"=== 로비 2층 여관 세팅 완료! (방 {roomCount}개) ===\n" +
                  $"{FLOOR2_NAME}를 켜서 방/계단 위치를 조정할 수 있습니다. 씬을 저장(Ctrl+S)하세요.");
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    // ═══════════════════════════════════════════
    // 클릭 가능한 월드 오브젝트 (임시 상자 스프라이트). 이미 있으면 그대로 반환
    // ═══════════════════════════════════════════
    static InteractiveObject CreateObject(LobbyMapGenerator gen, Transform parent, string id, string label,
                                          Vector2Int cell, Color color, out bool created)
    {
        created = false;

        Transform existing = parent.Find(id);
        if (existing != null)
        {
            InteractiveObject found = existing.GetComponent<InteractiveObject>();
            if (found != null) return found;
        }

        GameObject go = existing != null ? existing.gameObject : new GameObject(id);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
            go.transform.SetParent(parent, false);

            Vector3 pos = gen.floorTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            pos.z = 0f;
            go.transform.position = pos;
        }

        Sprite sprite = LobbyFacilitySetup.CreatePlaceholderSprite(id, color);

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = sprite;

        // 클릭 판정 (카메라의 Physics2DRaycaster가 사용)
        if (go.GetComponent<Collider2D>() == null && sr.sprite != null)
        {
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.offset = sr.sprite.bounds.center;
            col.size   = sr.sprite.bounds.size;
        }

        InteractiveObject interactive = go.AddComponent<InteractiveObject>();
        interactive.objectName = label;
        interactive.highlightBrightness = 0.3f;
        interactive.onClickAction = new UnityEvent();

        created = true;
        return interactive;
    }

    // ═══════════════════════════════════════════
    // 오브젝트 위에 떠 있는 이름표 (월드 공간 텍스트)
    // ═══════════════════════════════════════════
    static TextMeshPro EnsureWorldLabel(Transform parent, string text, TMP_FontAsset font)
    {
        Transform existing = parent.Find("Label");
        if (existing != null)
        {
            TextMeshPro found = existing.GetComponent<TextMeshPro>();
            if (found != null) return found;
        }

        GameObject go = new GameObject("Label");
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = 2.6f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
        tmp.GetComponent<MeshRenderer>().sortingOrder = LABEL_SORTING;
        return tmp;
    }

    // ═══════════════════════════════════════════
    // 여관 방 선택창: [파티 관리] / [방 비우기]
    // ═══════════════════════════════════════════
    static void EnsureRoomMenuPopup(LobbyManager lobby, TMP_FontAsset font)
    {
        Canvas canvas = GetLobbyCanvas(lobby);
        if (canvas == null) return;

        Transform popup = canvas.transform.Find(ROOM_MENU_NAME);
        if (popup == null)
        {
            Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, ROOM_MENU_NAME, "1번 방", lobby.normalColor, font, out _);
            LobbyFacilitySetup.CreateMenuButton("Btn_Detail", window, "파티 관리", LobbyFacilitySetup.MenuButtonColor, font, lobby.OnClickRoomMenuDetail);
            LobbyFacilitySetup.CreateMenuButton("Btn_Vacate", window, "방 비우기", LobbyFacilitySetup.MenuButtonColor, font, lobby.OnClickRoomMenuVacate);
            LobbyFacilitySetup.CreateMenuButton("Btn_Close",  window, "닫기",      LobbyFacilitySetup.MenuCloseColor,  font, lobby.CloseRoomMenu);
            popup = window.parent;
            Debug.Log("[LobbySecondFloorSetup] 여관 방 선택창 생성 완료");
        }

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.roomMenuPopup = popup.gameObject;
        Transform title = popup.Find("Window/Txt_Title");
        lobby.roomMenuTitleText = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
        EditorUtility.SetDirty(lobby);
    }

    static Canvas GetLobbyCanvas(LobbyManager lobby)
    {
        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.rootCanvas : null;
    }

    // ═══════════════════════════════════════════
    // 화면 상단 알림 UI (LobbyManager.ShowNotification이 사용)
    // ═══════════════════════════════════════════
    static void EnsureNotificationPanel(LobbyManager lobby, TMP_FontAsset font)
    {
        if (lobby.notificationPanel != null && lobby.notificationText != null) return;

        Canvas canvas = GetLobbyCanvas(lobby);
        if (canvas == null) return;

        GameObject panelGO = new GameObject("NotificationPanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panelGO, UNDO_NAME);
        panelGO.layer = LayerMask.NameToLayer("UI");
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -150f);
        panelRect.sizeDelta = new Vector2(1000f, 0f);

        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.08f, 0.05f, 0.9f);
        panelImg.raycastTarget = false;

        VerticalLayoutGroup layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 14, 14);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGO = new GameObject("Txt_Notification", typeof(RectTransform));
        textGO.layer = panelGO.layer;
        textGO.transform.SetParent(panelGO.transform, false);

        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = "";
        text.fontSize = 30;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        panelGO.SetActive(false);

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.notificationPanel = panelGO;
        lobby.notificationText = text;
        EditorUtility.SetDirty(lobby);
        Debug.Log("[LobbySecondFloorSetup] 알림 UI 생성 완료");
    }
}
