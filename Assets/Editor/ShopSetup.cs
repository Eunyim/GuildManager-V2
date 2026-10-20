using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 상점 세팅
/// LobbyScene에 상점 창(전리품 판매)을 만들고 LobbyManager.shopPanel에 연결합니다.
/// 매입 품목은 프로젝트의 아이템 데이터(MonsterDropData) 전체로 채우며, 판매가는 각 아이템 데이터의 price를 씁니다.
/// 상점 건물은 Tools > 길드 밖 화면 세팅이 만듭니다. 이미 만들어진 창은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class ShopSetup : Editor
{
    const string POPUP_NAME  = "Popup_Shop";
    const string SLOT_PATH   = "Assets/Prefabs/UI/Slot_Shop.prefab";
    const string UNDO_NAME   = "상점 세팅";
    const float  PANEL_WIDTH = 1000f;

    [MenuItem("Tools/상점 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[ShopSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[ShopSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ShopSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        Transform popupTransform = canvas.transform.Find(POPUP_NAME);
        ShopPanel panel = popupTransform != null ? popupTransform.GetComponent<ShopPanel>() : null;
        if (panel == null) panel = BuildPanel(canvas, lobby, font);
        else Debug.Log($"[ShopSetup] {POPUP_NAME} 이미 존재, 건너뜀");

        if (panel.slotPrefab == null) panel.slotPrefab = GetOrCreateSlotPrefab(font);

        // 매입 품목: 프로젝트에 있는 아이템 데이터 전체 (새 아이템을 만든 뒤 다시 실행하면 추가됨)
        List<MonsterDropData> catalog = new List<MonsterDropData>();
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDropData"))
        {
            MonsterDropData item = AssetDatabase.LoadAssetAtPath<MonsterDropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) catalog.Add(item);
        }
        catalog.Sort((a, b) => string.Compare(a.itemName, b.itemName, System.StringComparison.Ordinal));
        panel.catalog = catalog;
        EditorUtility.SetDirty(panel);

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.shopPanel = panel;
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log($"=== 상점 세팅 완료! (매입 품목 {catalog.Count}종) ===\n씬을 저장(Ctrl+S)하세요.");
    }

    static ShopPanel BuildPanel(Canvas canvas, LobbyManager lobby, TMP_FontAsset font)
    {
        Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, POPUP_NAME, "상점", lobby.normalColor, font, out _);
        RectTransform windowRect = (RectTransform)window;
        windowRect.sizeDelta = new Vector2(PANEL_WIDTH, windowRect.sizeDelta.y);
        window.GetComponent<VerticalLayoutGroup>().spacing = 12f;

        ShopPanel panel = window.parent.gameObject.AddComponent<ShopPanel>();

        TextMeshProUGUI desc = LobbyFacilitySetup.CreateMenuText("Txt_Desc", window, "던전에서 가져온 전리품을 골드로 바꿉니다.", 24, font);
        desc.color = new Color(0.85f, 0.8f, 0.7f);

        panel.goldText = LobbyFacilitySetup.CreateMenuText("Txt_Gold", window, "보유 골드: 0 G", 28, font);
        panel.goldText.color = new Color(1f, 0.85f, 0.4f);

        TempleSetup.CreateListPanel("Panel_Items", window, font, "팔 수 있는 전리품이 없습니다.",
            out panel.emptyText, out panel.listContent);

        LobbyFacilitySetup.CreateMenuButton("Btn_SellAll", window, "전부 판매", LobbyFacilitySetup.MenuButtonColor, font, panel.OnClickSellEverything);
        Transform sellAll = window.Find("Btn_SellAll");
        panel.sellEverythingButton = sellAll.GetComponent<Button>();
        panel.sellEverythingLabel = sellAll.GetComponentInChildren<TextMeshProUGUI>();

        LobbyFacilitySetup.CreateMenuButton("Btn_Close", window, "닫기", LobbyFacilitySetup.MenuCloseColor, font, panel.Close);

        Debug.Log($"[ShopSetup] {POPUP_NAME} 생성 완료");
        return panel;
    }

    // 목록 한 줄: [이름 x수량 / 가격 (텍스트 2줄)] [1개 판매] [모두 판매]
    // ShopPanel이 텍스트와 버튼을 순서대로 쓰므로 순서를 바꾸면 안 됨
    static GameObject GetOrCreateSlotPrefab(TMP_FontAsset font)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SLOT_PATH);
        if (existing != null) return existing;

        GameObject slotGO = new GameObject("Slot_Shop", typeof(RectTransform));
        slotGO.layer = LayerMask.NameToLayer("UI");

        Image bg = slotGO.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);
        bg.raycastTarget = false;

        HorizontalLayoutGroup layout = slotGO.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 12, 12);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

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

        LobbyFacilitySetup.CreateMenuText("Txt_Name", textsGO.transform, "아이템 x0", 30, font).alignment = TextAlignmentOptions.Left;
        TextMeshProUGUI price = LobbyFacilitySetup.CreateMenuText("Txt_Price", textsGO.transform, "개당 0 G", 22, font);
        price.alignment = TextAlignmentOptions.Left;
        price.color = new Color(1f, 0.85f, 0.4f);

        CreateSlotButton("Btn_SellOne", slotGO.transform, "1개 판매", font);
        CreateSlotButton("Btn_SellAll", slotGO.transform, "모두 판매", font);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(slotGO, SLOT_PATH);
        Object.DestroyImmediate(slotGO);

        Debug.Log($"[ShopSetup] 슬롯 프리팹 생성: {SLOT_PATH}");
        return prefab;
    }

    static void CreateSlotButton(string name, Transform parent, string label, TMP_FontAsset font)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform));
        btnGO.layer = parent.gameObject.layer;
        btnGO.transform.SetParent(parent, false);

        LayoutElement btnLayout = btnGO.AddComponent<LayoutElement>();
        btnLayout.preferredWidth = 170f;
        btnLayout.preferredHeight = 76f;

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = LobbyFacilitySetup.MenuButtonColor;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        TextMeshProUGUI text = LobbyFacilitySetup.CreateMenuText("Txt_Label", btnGO.transform, label, 26, font);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
