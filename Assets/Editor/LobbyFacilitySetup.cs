using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.IO;
using System.Reflection;

/// <summary>
/// [메뉴] Tools > 로비 시설 오브젝트 배치
/// 타일맵 바닥 위에 클릭 가능한 시설 오브젝트(임시 스프라이트)를 놓고,
/// 클릭 시 LobbyManager의 해당 메뉴가 열리도록 연결합니다. 예전 하단 버튼 메뉴(BottomMenu)는 제거합니다.
/// 모험가 관리 오브젝트는 [모험가 명단] / [파티 편성]을 고르는 선택창을 엽니다.
/// 이미 있는 시설은 건너뛰므로, 위치를 옮기거나 스프라이트를 바꾼 뒤 다시 실행해도 안전합니다.
/// </summary>
public class LobbyFacilitySetup : Editor
{
    const string SPRITE_FOLDER = "Assets/Art/Lobby/Facilities";
    public const string FONT_PATH = "Assets/Fonts/DungGeunMo SDF.asset";
    public const string ROOT_NAME = "Lobby_Facilities";
    const int    SPRITE_SIZE   = 64;  // 64px = 1월드유닛 (바닥 1칸 크기의 상자)
    const string ADVENTURERS_ID  = "Facility_Adventurers";
    const string MENU_POPUP_NAME = "Popup_AdventurerMenu";
    const string UNDO_NAME       = "로비 시설 오브젝트 배치";
    const int    SORTING_BASE  = 100; // 타일맵(바닥 -10, 벽 -9, 장식 -8)보다 항상 위에 그려지도록 하는 기준값

    // 시설 정의: (오브젝트 이름, 표시 이름, LobbyManager 메서드 이름, 바닥 셀 좌표, 임시 스프라이트 색)
    static readonly (string id, string label, string method, Vector2Int cell, Color color)[] facilities =
    {
        ("Facility_Reception",  "접수처",       "OnClickReception",            new Vector2Int(-5,  3), new Color(0.80f, 0.52f, 0.25f)),
        ("Facility_QuestBoard", "퀘스트 보드",   "OnClickQuestBoardMenu",       new Vector2Int( 0,  3), new Color(0.90f, 0.78f, 0.30f)),
        // 명단 확인과 파티 편성은 하나의 오브젝트에서 선택창으로 고름
        (ADVENTURERS_ID,        "모험가 관리",   "OnClickAdventurerMenu",       new Vector2Int( 5,  3), new Color(0.40f, 0.75f, 0.45f)),
        // 신전은 씬에 SpiritStonePopup이 연결되면 추가:
        // ("Facility_Temple",  "신전",         "OnClickOpenSpiritStonePopup", new Vector2Int( 6, -4), new Color(0.65f, 0.45f, 0.85f)),
        // 주점은 기본 제공 시설 (클릭 시 현재 효과 안내)
        ("Facility_Pub",        "주점",         "OnClickPub",                  new Vector2Int( 2, -3), new Color(0.85f, 0.60f, 0.25f)),
        // 길드 문: 밖으로 나가 건축 공방 / 신전을 고름 (화면은 Tools > 길드 밖 화면 세팅으로 생성)
        ("Facility_Door",       "길드 문 (밖으로)", "OnClickGuildDoor",          new Vector2Int(-8, -5), new Color(0.55f, 0.38f, 0.22f)),
    };

    [MenuItem("Tools/로비 시설 오브젝트 배치")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[LobbyFacilitySetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        LobbyMapGenerator gen = Object.FindFirstObjectByType<LobbyMapGenerator>();
        if (lobby == null || gen == null || gen.floorTilemap == null)
        {
            Debug.LogError("[LobbyFacilitySetup] LobbyManager 또는 LobbyMapGenerator를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        // 1. 시설 부모 오브젝트
        GameObject root = GameObject.Find(ROOT_NAME);
        if (root == null)
        {
            root = new GameObject(ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(root, "로비 시설 오브젝트 배치");
        }

        MergeLegacyAdventurerFacilities(root.transform, lobby);
        ReplaceLegacyFacility(root.transform, lobby, "Facility_Upgrade", "Facility_Pub");

        // 더 이상 쓰지 않는 시설 제거 (탐험: 파견은 퀘스트 보드에서만)
        Transform legacyExplore = root.transform.Find("Facility_Explore");
        if (legacyExplore != null)
        {
            Undo.DestroyObjectImmediate(legacyExplore.gameObject);
            Debug.Log("[LobbyFacilitySetup] Facility_Explore 제거");
        }

        // 2. 시설 오브젝트 생성
        int created = 0;
        foreach (var fac in facilities)
        {
            Transform existingFacility = root.transform.Find(fac.id);
            if (existingFacility != null)
            {
                // 위치/스프라이트는 그대로 두고, 클릭 동작만 현재 정의와 다르면 맞춰 줌
                if (EnsureClickMethod(existingFacility, lobby, fac.method))
                    Debug.Log($"[LobbyFacilitySetup] {fac.id} 클릭 동작을 {fac.method}로 변경");
                else
                    Debug.Log($"[LobbyFacilitySetup] {fac.id} 이미 존재, 건너뜀");
                continue;
            }

            Sprite sprite = CreatePlaceholderSprite(fac.id, fac.color);
            if (sprite == null)
            {
                Debug.LogWarning($"[LobbyFacilitySetup] {fac.id} 임시 스프라이트 생성 실패");
                continue;
            }

            GameObject go = new GameObject(fac.id);
            Undo.RegisterCreatedObjectUndo(go, "로비 시설 오브젝트 배치");
            go.transform.SetParent(root.transform, false);

            Vector3 pos = gen.floorTilemap.GetCellCenterWorld(new Vector3Int(fac.cell.x, fac.cell.y, 0));
            pos.z = 0f;
            go.transform.position = pos;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = GetSortingOrder(pos.y);

            // 클릭 판정 (카메라의 Physics2DRaycaster가 사용)
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.offset = sprite.bounds.center;
            col.size   = sprite.bounds.size;

            InteractiveObject interactive = go.AddComponent<InteractiveObject>();
            interactive.objectName = fac.label;
            interactive.highlightBrightness = 0.3f;
            interactive.onClickAction = new UnityEvent();

            MethodInfo method = typeof(LobbyManager).GetMethod(fac.method, BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                UnityAction action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), lobby, method);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(interactive.onClickAction, action);
            }
            else
            {
                Debug.LogWarning($"[LobbyFacilitySetup] {fac.method} 메서드를 찾을 수 없습니다!");
            }

            created++;
        }

        // 2-1. 기존 시설 포함 전체 정렬 순서 갱신 (위치를 옮긴 뒤 다시 실행하면 앞뒤 순서가 맞춰짐)
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            int order = GetSortingOrder(sr.transform.position.y);
            if (sr.sortingOrder == order) continue;
            Undo.RecordObject(sr, "로비 시설 오브젝트 배치");
            sr.sortingOrder = order;
            EditorUtility.SetDirty(sr);
        }

        // 3. 월드 오브젝트 클릭에 필요한 Physics2DRaycaster 확인
        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null && cam.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
        {
            Undo.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>(cam.gameObject);
            Debug.Log("[LobbyFacilitySetup] Main Camera에 Physics2DRaycaster 추가");
        }

        // 4. 예전 하단 버튼 메뉴(BottomMenu) 제거 + 이름 라벨 UI
        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            // 시설 오브젝트가 역할을 모두 대신하므로 더 이상 쓰지 않음
            Transform bottomMenu = canvas.transform.Find("BottomMenu");
            if (bottomMenu != null)
            {
                Undo.DestroyObjectImmediate(bottomMenu.gameObject);
                Debug.Log("[LobbyFacilitySetup] BottomMenu 제거 완료");
            }

            EnsureAdventurerMenuPopup(canvas, lobby);
            EnsureLabelPanel(canvas, lobby);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"=== 로비 시설 오브젝트 배치 완료! ({created}개 생성) ===\n" +
                  $"{ROOT_NAME} 아래 오브젝트를 원하는 위치로 옮기고, SpriteRenderer의 스프라이트를 실제 에셋으로 교체하세요.\n" +
                  "씬을 저장(Ctrl+S)하세요.");
    }

    // ═══════════════════════════════════════════
    // 예전의 [명단 관리] + [파티 관리] 오브젝트를 [모험가 관리] 하나로 병합
    // 파티 관리 오브젝트를 그 자리에서 모험가 관리로 바꾸고, 명단 관리 오브젝트는 제거합니다.
    // ═══════════════════════════════════════════
    static void MergeLegacyAdventurerFacilities(Transform root, LobbyManager lobby)
    {
        Transform party  = root.Find("Facility_Party");
        Transform manage = root.Find("Facility_Manage");

        if (root.Find(ADVENTURERS_ID) == null)
        {
            Transform keep = party != null ? party : manage;
            if (keep != null)
            {
                Undo.RecordObject(keep.gameObject, UNDO_NAME);
                keep.name = ADVENTURERS_ID;

                InteractiveObject interactive = keep.GetComponent<InteractiveObject>();
                if (interactive != null)
                {
                    Undo.RecordObject(interactive, UNDO_NAME);
                    interactive.objectName = "모험가 관리";

                    UnityAction action = lobby.OnClickAdventurerMenu;
                    if (interactive.onClickAction.GetPersistentEventCount() > 0)
                        UnityEditor.Events.UnityEventTools.RegisterPersistentListener(interactive.onClickAction, 0, action);
                    else
                        UnityEditor.Events.UnityEventTools.AddPersistentListener(interactive.onClickAction, action);
                    EditorUtility.SetDirty(interactive);
                }

                if (keep == party) party = null; else manage = null;
                Debug.Log("[LobbyFacilitySetup] 명단 관리 + 파티 관리 → 모험가 관리로 병합");
            }
        }

        if (party != null)  Undo.DestroyObjectImmediate(party.gameObject);
        if (manage != null) Undo.DestroyObjectImmediate(manage.gameObject);
    }

    // 시설 오브젝트의 클릭 동작이 methodName이 아니면 교체. 바꿨으면 true
    static bool EnsureClickMethod(Transform facility, LobbyManager lobby, string methodName)
    {
        InteractiveObject interactive = facility.GetComponent<InteractiveObject>();
        if (interactive == null || interactive.onClickAction == null) return false;

        int count = interactive.onClickAction.GetPersistentEventCount();
        if (count > 0 && interactive.onClickAction.GetPersistentMethodName(0) == methodName) return false;

        MethodInfo method = typeof(LobbyManager).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (method == null) return false;

        Undo.RecordObject(interactive, UNDO_NAME);
        UnityAction action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), lobby, method);
        if (count > 0)
            UnityEditor.Events.UnityEventTools.RegisterPersistentListener(interactive.onClickAction, 0, action);
        else
            UnityEditor.Events.UnityEventTools.AddPersistentListener(interactive.onClickAction, action);
        EditorUtility.SetDirty(interactive);
        return true;
    }

    // ═══════════════════════════════════════════
    // 예전 시설 오브젝트를 그 자리에서 새 시설로 교체 (이름, 라벨, 클릭 동작, 임시 스프라이트)
    // ═══════════════════════════════════════════
    static void ReplaceLegacyFacility(Transform root, LobbyManager lobby, string oldId, string newId)
    {
        Transform old = root.Find(oldId);
        if (old == null) return;

        // 새 시설이 이미 있으면 예전 것은 그냥 제거
        if (root.Find(newId) != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);
            return;
        }

        foreach (var fac in facilities)
        {
            if (fac.id != newId) continue;

            Undo.RecordObject(old.gameObject, UNDO_NAME);
            old.name = newId;

            SpriteRenderer sr = old.GetComponent<SpriteRenderer>();
            Sprite sprite = CreatePlaceholderSprite(fac.id, fac.color);
            if (sr != null && sprite != null)
            {
                Undo.RecordObject(sr, UNDO_NAME);
                sr.sprite = sprite;
                EditorUtility.SetDirty(sr);
            }

            InteractiveObject interactive = old.GetComponent<InteractiveObject>();
            MethodInfo method = typeof(LobbyManager).GetMethod(fac.method, BindingFlags.Public | BindingFlags.Instance);
            if (interactive != null && method != null)
            {
                Undo.RecordObject(interactive, UNDO_NAME);
                interactive.objectName = fac.label;

                UnityAction action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), lobby, method);
                if (interactive.onClickAction.GetPersistentEventCount() > 0)
                    UnityEditor.Events.UnityEventTools.RegisterPersistentListener(interactive.onClickAction, 0, action);
                else
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(interactive.onClickAction, action);
                EditorUtility.SetDirty(interactive);
            }

            Debug.Log($"[LobbyFacilitySetup] {oldId} → {newId} 교체");
            return;
        }
    }

    // ═══════════════════════════════════════════
    // 모험가 관리 선택창: [모험가 명단] / [파티 편성]
    // ═══════════════════════════════════════════
    static void EnsureAdventurerMenuPopup(Canvas canvas, LobbyManager lobby)
    {
        Transform popup = canvas.transform.Find(MENU_POPUP_NAME);
        if (popup == null)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);

            Transform window = CreateMenuWindow(canvas, MENU_POPUP_NAME, "모험가 관리", lobby.normalColor, font, out _);
            CreateMenuButton("Btn_Roster", window, "모험가 명단", MenuButtonColor, font, lobby.OnClickMenuRoster);
            CreateMenuButton("Btn_Party",  window, "파티 편성",   MenuButtonColor, font, lobby.OnClickMenuParty);
            CreateMenuButton("Btn_Close",  window, "닫기",        MenuCloseColor,  font, lobby.CloseAdventurerMenu);

            popup = window.parent;
            Debug.Log("[LobbyFacilitySetup] 모험가 관리 선택창 생성 완료");
        }

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.adventurerMenuPopup = popup.gameObject;
        EditorUtility.SetDirty(lobby);
    }

    public static readonly Color MenuButtonColor = new Color(0.42f, 0.26f, 0.13f, 1f);
    public static readonly Color MenuCloseColor  = new Color(0.25f, 0.2f, 0.18f, 1f);

    // 선택창 뼈대: 화면을 덮는 어두운 배경 + 제목이 있는 창. 반환값(창)에 버튼을 추가하면 됩니다. 꺼진 상태로 만들어집니다.
    public static Transform CreateMenuWindow(Canvas canvas, string popupName, string title, Color windowColor,
                                             TMP_FontAsset font, out TextMeshProUGUI titleText)
    {
        // 화면 전체를 덮는 어두운 배경 (뒤쪽 클릭 차단)
        GameObject popupGO = new GameObject(popupName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(popupGO, UNDO_NAME);
        popupGO.layer = LayerMask.NameToLayer("UI");
        popupGO.transform.SetParent(canvas.transform, false);
        RectTransform popupRect = popupGO.GetComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;
        popupGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // 창
        GameObject windowGO = new GameObject("Window", typeof(RectTransform));
        windowGO.layer = popupGO.layer;
        windowGO.transform.SetParent(popupGO.transform, false);
        windowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 0f);
        windowGO.AddComponent<Image>().color = windowColor;

        VerticalLayoutGroup layout = windowGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 32, 36);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = windowGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = CreateMenuText("Txt_Title", windowGO.transform, title, 44, font);
        titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        popupGO.SetActive(false);
        return windowGO.transform;
    }

    public static TextMeshProUGUI CreateMenuText(string name, Transform parent, string text, float size, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static void CreateMenuButton(string name, Transform parent, string label, Color color, TMP_FontAsset font, UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 90f;

        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, onClick);

        TextMeshProUGUI text = CreateMenuText("Txt_Label", go.transform, label, 34, font);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    // 아래쪽(앞)에 있는 시설이 위에 그려지되, 바닥 타일맵에 가려지지 않게 함
    public static int GetSortingOrder(float worldY)
    {
        return SORTING_BASE + Mathf.RoundToInt(-worldY * 10f);
    }

    // ═══════════════════════════════════════════
    // 임시 스프라이트: 바닥 1칸 크기의 아이소메트릭 상자
    // ═══════════════════════════════════════════
    public static Sprite CreatePlaceholderSprite(string id, Color color)
    {
        EnsureFolder(SPRITE_FOLDER);
        string path = $"{SPRITE_FOLDER}/temp_{id}.png";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        int size = SPRITE_SIZE;
        float half = size * 0.5f;
        float quarter = size * 0.25f;

        Color top   = color;
        Color left  = new Color(color.r * 0.75f, color.g * 0.75f, color.b * 0.75f, 1f);
        Color right = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
        Color clear = new Color(0f, 0f, 0f, 0f);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float t = 1f - Mathf.Abs(px - half) / half;   // 가운데 1 → 양 끝 0
                float bottomEdge = quarter - quarter * t;     // 바닥 마름모의 아래쪽 경계

                Color c = clear;
                if (Mathf.Abs(py - (size - quarter)) <= quarter * t)
                    c = top;                                   // 윗면 마름모
                else if (py >= bottomEdge && py < bottomEdge + half)
                    c = px < half ? left : right;              // 옆면
                tex.SetPixel(x, y, c);
            }
        }

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = size;

        // 피벗 = 바닥 마름모의 중심 → 오브젝트 위치가 곧 서 있는 칸의 중심
        TextureImporterSettings texSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(texSettings);
        texSettings.spriteAlignment = (int)SpriteAlignment.Custom;
        texSettings.spritePivot     = new Vector2(0.5f, 0.25f);
        importer.SetTextureSettings(texSettings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ═══════════════════════════════════════════
    // 마우스를 올렸을 때 뜨는 시설 이름 라벨
    // ═══════════════════════════════════════════
    static void EnsureLabelPanel(Canvas canvas, LobbyManager lobby)
    {
        Transform labelPanel = canvas.transform.Find("ObjectLabelPanel");
        if (labelPanel == null)
        {
            GameObject panelGO = new GameObject("ObjectLabelPanel");
            Undo.RegisterCreatedObjectUndo(panelGO, "로비 시설 오브젝트 배치");
            panelGO.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(200, 40);
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 0);
            panelRect.pivot = new Vector2(0, 0);

            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.08f, 0.05f, 0.85f);
            panelImg.raycastTarget = false;

            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = panelGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;

            GameObject textGO = new GameObject("Txt_Label");
            textGO.transform.SetParent(panelGO.transform, false);
            textGO.AddComponent<RectTransform>().sizeDelta = new Vector2(180, 30);

            TextMeshProUGUI labelText = textGO.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (font != null) labelText.font = font;
            labelText.text = "시설 이름";
            labelText.fontSize = 20;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;

            panelGO.transform.SetAsLastSibling();
            panelGO.SetActive(false);
            labelPanel = panelGO.transform;
            Debug.Log("[LobbyFacilitySetup] 오브젝트 라벨 UI 생성 완료");
        }

        Undo.RecordObject(lobby, "로비 시설 오브젝트 배치");
        lobby.objectLabelPanel = labelPanel.gameObject;
        lobby.objectLabelText  = labelPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        EditorUtility.SetDirty(lobby);
    }

    static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
