using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 신전 화면 세팅
/// LobbyScene에 신전 창(부활 / 특성 치유 탭)을 만들고 LobbyManager.spiritStonePopup에 연결합니다.
/// 길드 밖 화면의 [신전] 건물을 누르면 이 창이 열립니다. 목록 한 줄에 쓰는 슬롯 프리팹도 함께 만듭니다.
/// 이미 만들어진 창은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class TempleSetup : Editor
{
    const string POPUP_NAME   = "Popup_Temple";
    const string SLOT_PATH    = "Assets/Prefabs/UI/Slot_Temple.prefab";
    const string UNDO_NAME    = "신전 화면 세팅";
    const float  PANEL_WIDTH  = 1100f;
    const float  LIST_HEIGHT  = 440f;

    [MenuItem("Tools/신전 화면 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[TempleSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[TempleSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TempleSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        Transform popupTransform = canvas.transform.Find(POPUP_NAME);
        SpiritStonePopup popup = popupTransform != null ? popupTransform.GetComponent<SpiritStonePopup>() : null;
        if (popup == null) popup = BuildPopup(canvas, lobby, font);
        else Debug.Log($"[TempleSetup] {POPUP_NAME} 이미 존재, 건너뜀");

        if (popup.slotPrefab == null)
        {
            popup.slotPrefab = GetOrCreateSlotPrefab(font);
            EditorUtility.SetDirty(popup);
        }

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.spiritStonePopup = popup;
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 신전 화면 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    static SpiritStonePopup BuildPopup(Canvas canvas, LobbyManager lobby, TMP_FontAsset font)
    {
        Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, POPUP_NAME, "신전", lobby.normalColor, font, out _);
        RectTransform windowRect = (RectTransform)window;
        windowRect.sizeDelta = new Vector2(PANEL_WIDTH, windowRect.sizeDelta.y);
        window.GetComponent<VerticalLayoutGroup>().spacing = 12f;

        SpiritStonePopup popup = window.parent.gameObject.AddComponent<SpiritStonePopup>();

        // 보유 영혼석 / 골드
        Transform header = CreateRow("Row_Header", window, 40f);
        popup.stoneCountText = LobbyFacilitySetup.CreateMenuText("Txt_Stone", header, "보유 영혼석: 0개", 26, font);
        popup.stoneCountText.color = new Color(0.8f, 0.7f, 1f);
        popup.goldText = LobbyFacilitySetup.CreateMenuText("Txt_Gold", header, "보유 골드: 0 G", 26, font);
        popup.goldText.color = new Color(1f, 0.85f, 0.4f);

        // 탭 버튼
        Transform tabs = CreateRow("Row_Tabs", window, 90f);
        LobbyFacilitySetup.CreateMenuButton("Btn_TabRevive", tabs, "부활", LobbyFacilitySetup.MenuButtonColor, font, popup.ShowReviveTab);
        LobbyFacilitySetup.CreateMenuButton("Btn_TabTrait",  tabs, "특성 치유", LobbyFacilitySetup.MenuButtonColor, font, popup.ShowTraitRemoveTab);
        popup.reviveTabButton      = tabs.Find("Btn_TabRevive").GetComponent<Button>();
        popup.traitRemoveTabButton = tabs.Find("Btn_TabTrait").GetComponent<Button>();

        // 탭 내용
        popup.revivePanel = CreateListPanel("Panel_Revive", window, font,
            "전사한 모험가가 없습니다.", out popup.emptyText, out popup.listContent);
        popup.traitRemovePanel = CreateListPanel("Panel_Trait", window, font,
            "치유할 특성을 가진 모험가가 없습니다.", out popup.traitEmptyText, out popup.traitListContent);
        popup.traitRemovePanel.SetActive(false);

        LobbyFacilitySetup.CreateMenuButton("Btn_Close", window, "닫기", LobbyFacilitySetup.MenuCloseColor, font, popup.OnClickClose);

        Debug.Log($"[TempleSetup] {POPUP_NAME} 생성 완료");
        return popup;
    }

    // 자식을 가로로 균등하게 나열하는 줄
    public static Transform CreateRow(string name, Transform parent, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = height;

        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return go.transform;
    }

    // 탭 하나의 내용: "없음" 안내 문구 + 스크롤되는 목록
    public static GameObject CreateListPanel(string name, Transform parent, TMP_FontAsset font, string emptyMessage,
                                      out TextMeshProUGUI emptyText, out Transform content)
    {
        GameObject panelGO = new GameObject(name, typeof(RectTransform));
        panelGO.layer = parent.gameObject.layer;
        panelGO.transform.SetParent(parent, false);
        panelGO.AddComponent<LayoutElement>().preferredHeight = LIST_HEIGHT;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.3f); // 스크롤 드래그를 받아야 하므로 raycastTarget 유지
        panelGO.AddComponent<RectMask2D>();

        // 목록 (위에서부터 쌓이고, 길어지면 스크롤)
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.layer = panelGO.layer;
        contentGO.transform.SetParent(panelGO.transform, false);
        RectTransform contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = panelGO.AddComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = panelGO.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // 안내 문구 (목록이 비었을 때만 보임, 패널 한가운데)
        emptyText = LobbyFacilitySetup.CreateMenuText("Txt_Empty", panelGO.transform, emptyMessage, 28, font);
        emptyText.color = new Color(0.75f, 0.75f, 0.75f);
        RectTransform emptyRect = emptyText.rectTransform;
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = Vector2.zero;
        emptyRect.offsetMax = Vector2.zero;

        content = contentGO.transform;
        return panelGO;
    }

    // 목록 한 줄: [이름 / 정보 / 상태 / 비용 (텍스트 4줄)] [선택 버튼]
    // SpiritStonePopup이 텍스트를 순서대로 채우므로 텍스트 4개가 버튼보다 먼저 와야 함
    static GameObject GetOrCreateSlotPrefab(TMP_FontAsset font)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SLOT_PATH);
        if (existing != null) return existing;

        GameObject slotGO = new GameObject("Slot_Temple", typeof(RectTransform));
        slotGO.layer = LayerMask.NameToLayer("UI");

        Image bg = slotGO.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);
        bg.raycastTarget = false;

        HorizontalLayoutGroup layout = slotGO.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 12, 12);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 텍스트 4줄
        GameObject textsGO = new GameObject("Texts", typeof(RectTransform));
        textsGO.layer = slotGO.layer;
        textsGO.transform.SetParent(slotGO.transform, false);
        textsGO.AddComponent<LayoutElement>().flexibleWidth = 1f;

        VerticalLayoutGroup textLayout = textsGO.AddComponent<VerticalLayoutGroup>();
        textLayout.spacing = 4f;
        textLayout.childAlignment = TextAnchor.MiddleLeft;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;

        CreateSlotText("Txt_Name",   textsGO.transform, "이름", 30, font);
        CreateSlotText("Txt_Info",   textsGO.transform, "직업 | 랭크", 22, font);
        CreateSlotText("Txt_Status", textsGO.transform, "상태", 22, font);
        CreateSlotText("Txt_Cost",   textsGO.transform, "비용: 0 G", 22, font);

        // 선택 버튼
        GameObject btnGO = new GameObject("Btn_Select", typeof(RectTransform));
        btnGO.layer = slotGO.layer;
        btnGO.transform.SetParent(slotGO.transform, false);

        LayoutElement btnLayout = btnGO.AddComponent<LayoutElement>();
        btnLayout.preferredWidth = 180f;
        btnLayout.preferredHeight = 80f;

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = LobbyFacilitySetup.MenuButtonColor;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        TextMeshProUGUI label = LobbyFacilitySetup.CreateMenuText("Txt_Label", btnGO.transform, "선택", 28, font);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(slotGO, SLOT_PATH);
        Object.DestroyImmediate(slotGO);

        Debug.Log($"[TempleSetup] 슬롯 프리팹 생성: {SLOT_PATH}");
        return prefab;
    }

    static void CreateSlotText(string name, Transform parent, string text, float size, TMP_FontAsset font)
    {
        TextMeshProUGUI tmp = LobbyFacilitySetup.CreateMenuText(name, parent, text, size, font);
        tmp.alignment = TextAlignmentOptions.Left;
    }
}
