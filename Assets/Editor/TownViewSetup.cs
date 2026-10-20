using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 길드 밖 화면 세팅
/// 길드 문을 나섰을 때 보이는 길드 밖 화면(TownView)을 만들고 LobbyManager에 연결합니다.
/// 화면에는 [건축 공방](시설 업그레이드), [신전], [치유소](결함 치료), [상점](전리품 판매) 건물과 [길드로 돌아가기] 버튼이 있습니다.
/// 건물은 같은 간격으로 일렬로 놓입니다. 이미 만들어진 화면에는 빠진 건물만 추가하고 간격을 다시 맞춥니다.
/// 화면을 여는 [길드 문] 오브젝트는 Tools > 로비 시설 오브젝트 배치가 만듭니다. 다시 실행해도 안전합니다.
/// </summary>
public class TownViewSetup : Editor
{
    const string VIEW_NAME = "TownView";
    const string UNDO_NAME = "길드 밖 화면 세팅";

    // 건물 배치 (1920x1080 기준): 왼쪽부터 순서대로, 같은 간격으로 일렬
    const float BUILDING_SPACING = 460f;  // 건물 중심 사이 거리 (건물 4채가 1920 폭 안에 들어가게)
    const float BUILDING_WIDTH   = 400f;
    const float BUILDING_HEIGHT  = 380f;
    const float ROOF_OVERHANG    = 30f;   // 지붕이 벽보다 한쪽으로 튀어나오는 폭
    const float BUILDING_BASE_Y  = 260f;  // 땅 위에 서 있도록
    static readonly string[] buildingOrder = { "Building_Workshop", "Building_Temple", "Building_Clinic", "Building_Shop" };

    static TMP_FontAsset font;

    [MenuItem("Tools/길드 밖 화면 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[TownViewSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[TownViewSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TownViewSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        Transform view = canvas.transform.Find(VIEW_NAME);
        if (view == null) view = BuildView(canvas, lobby);
        else Debug.Log($"[TownViewSetup] {VIEW_NAME} 이미 존재, 빠진 건물만 추가");

        EnsureBuildings(view, lobby);

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.townView = view.gameObject;
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 길드 밖 화면 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    // ═══════════════════════════════════════════
    // 화면 구성 (1920x1080 기준, 임시 그래픽)
    // ═══════════════════════════════════════════
    static Transform BuildView(Canvas canvas, LobbyManager lobby)
    {
        // 루트: 화면 전체를 덮는 하늘 (뒤쪽 로비 클릭 차단)
        RectTransform root = CreateRect(VIEW_NAME, canvas.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, UNDO_NAME);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        AddImage(root, new Color(0.36f, 0.55f, 0.75f, 1f), true);

        // 땅
        RectTransform ground = CreateRect("Bg_Ground", root);
        ground.anchorMin = new Vector2(0f, 0f);
        ground.anchorMax = new Vector2(1f, 0f);
        ground.pivot = new Vector2(0.5f, 0f);
        ground.anchoredPosition = Vector2.zero;
        ground.sizeDelta = new Vector2(0f, 360f);
        AddImage(ground, new Color(0.35f, 0.47f, 0.27f, 1f), false);

        TextMeshProUGUI title = CreateText("Txt_Title", root, "길드 앞 거리", 52, Color.white);
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 80f));

        // 건물은 EnsureBuildings가 만들고 배치함

        // 길드로 돌아가기
        RectTransform back = CreateRect("Btn_Back", root);
        SetAnchored(back, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(320f, 70f));
        back.pivot = new Vector2(0f, 1f);
        Image backImg = AddImage(back, LobbyFacilitySetup.MenuButtonColor, true);
        Button backBtn = back.gameObject.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        UnityEventTools.AddPersistentListener(backBtn.onClick, lobby.CloseTown);
        TextMeshProUGUI backLabel = CreateText("Txt_Label", back, "길드로 돌아가기", 30, Color.white);
        Stretch(backLabel.rectTransform);

        root.SetAsLastSibling();
        root.gameObject.SetActive(false);

        Debug.Log($"[TownViewSetup] {VIEW_NAME} 생성 완료");
        return root;
    }

    // 빠진 건물을 만들고(클릭해서 선택), 모든 건물을 같은 간격으로 일렬 배치
    static void EnsureBuildings(Transform root, LobbyManager lobby)
    {
        if (root.Find("Building_Workshop") == null)
            CreateBuilding(root, "Building_Workshop", "건축 공방", "길드 시설 업그레이드",
                new Color(0.62f, 0.42f, 0.24f), new Color(0.45f, 0.22f, 0.15f), lobby.OnClickFacilityUpgrade);
        if (root.Find("Building_Temple") == null)
            CreateBuilding(root, "Building_Temple", "신전", "부활 / 특성 치유",
                new Color(0.85f, 0.85f, 0.80f), new Color(0.45f, 0.50f, 0.70f), lobby.OnClickTemple);
        if (root.Find("Building_Clinic") == null)
            CreateBuilding(root, "Building_Clinic", "치유소", "결함 치료",
                new Color(0.92f, 0.92f, 0.92f), new Color(0.70f, 0.25f, 0.25f), lobby.OnClickClinic);
        if (root.Find("Building_Shop") == null)
            CreateBuilding(root, "Building_Shop", "상점", "전리품 판매",
                new Color(0.80f, 0.68f, 0.38f), new Color(0.30f, 0.45f, 0.30f), lobby.OnClickShop);

        for (int i = 0; i < buildingOrder.Length; i++)
        {
            RectTransform wall = root.Find(buildingOrder[i]) as RectTransform;
            if (wall == null) continue;

            Undo.RecordObject(wall, UNDO_NAME);
            float x = (i - (buildingOrder.Length - 1) * 0.5f) * BUILDING_SPACING;
            ApplyBuildingLayout(wall, x);
            EditorUtility.SetDirty(wall);
        }

        // [길드로 돌아가기] 버튼이 건물에 가리지 않도록 맨 위로
        Transform back = root.Find("Btn_Back");
        if (back != null) back.SetAsLastSibling();
    }

    // 건물 하나의 위치와 크기 (지붕, 이름표, 글자 폭 포함)
    static void ApplyBuildingLayout(RectTransform wall, float x)
    {
        wall.anchoredPosition = new Vector2(x, BUILDING_BASE_Y);
        wall.sizeDelta = new Vector2(BUILDING_WIDTH, BUILDING_HEIGHT);

        float plateWidth = BUILDING_WIDTH - 60f;
        SetWidth(wall.Find("Roof"), BUILDING_WIDTH + ROOF_OVERHANG * 2f);
        SetWidth(wall.Find("NamePlate"), plateWidth);
        SetWidth(wall.Find("NamePlate/Txt_Name"), plateWidth - 20f);
        SetWidth(wall.Find("NamePlate/Txt_Desc"), plateWidth - 20f);
    }

    static void SetWidth(Transform target, float width)
    {
        RectTransform rect = target as RectTransform;
        if (rect == null) return;

        Undo.RecordObject(rect, UNDO_NAME);
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        EditorUtility.SetDirty(rect);
    }

    // 건물 하나: 지붕 + 벽(클릭 영역) + 이름 / 설명. 위치와 폭은 ApplyBuildingLayout이 정함
    static void CreateBuilding(Transform parent, string name, string label, string description,
                               Color wallColor, Color roofColor, UnityAction onClick)
    {
        RectTransform wall = CreateRect(name, parent);
        Undo.RegisterCreatedObjectUndo(wall.gameObject, UNDO_NAME);
        wall.anchorMin = new Vector2(0.5f, 0f);
        wall.anchorMax = new Vector2(0.5f, 0f);
        wall.pivot = new Vector2(0.5f, 0f);
        wall.anchoredPosition = new Vector2(0f, BUILDING_BASE_Y);
        wall.sizeDelta = new Vector2(BUILDING_WIDTH, BUILDING_HEIGHT);

        Image wallImg = AddImage(wall, wallColor, true);
        Button btn = wall.gameObject.AddComponent<Button>();
        btn.targetGraphic = wallImg;
        UnityEventTools.AddPersistentListener(btn.onClick, onClick);

        RectTransform roof = CreateRect("Roof", wall);
        roof.anchorMin = new Vector2(0.5f, 1f);
        roof.anchorMax = new Vector2(0.5f, 1f);
        roof.pivot = new Vector2(0.5f, 0f);
        roof.anchoredPosition = Vector2.zero;
        roof.sizeDelta = new Vector2(600f, 110f);
        AddImage(roof, roofColor, false);

        // 이름표 (어두운 판 위에 글씨)
        RectTransform plate = CreateRect("NamePlate", wall);
        plate.anchorMin = new Vector2(0.5f, 0.5f);
        plate.anchorMax = new Vector2(0.5f, 0.5f);
        plate.pivot = new Vector2(0.5f, 0.5f);
        plate.anchoredPosition = Vector2.zero;
        plate.sizeDelta = new Vector2(440f, 150f);
        AddImage(plate, new Color(0.1f, 0.08f, 0.05f, 0.85f), false);

        TextMeshProUGUI nameText = CreateText("Txt_Name", plate, label, 48, Color.white);
        SetAnchored(nameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(420f, 70f));
        nameText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI descText = CreateText("Txt_Desc", plate, description, 26, new Color(0.85f, 0.8f, 0.7f));
        SetAnchored(descText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(420f, 44f));
        descText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    // ═══════════════════════════════════════════
    // UI 생성 도우미
    // ═══════════════════════════════════════════
    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // anchor를 한 점에 두고 위치/크기 지정 (pivot은 위쪽 가운데)
    static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static Image AddImage(RectTransform rect, Color color, bool raycastTarget)
    {
        Image img = rect.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }
}
