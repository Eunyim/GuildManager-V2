using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 동시 파견 UI 세팅
/// LobbyScene에 퀘스트 보드 선택창([의뢰 수행] / [동시 파견])과 동시 파견 창을 만들고 LobbyManager에 연결합니다.
/// 퀘스트 보드 오브젝트의 클릭 동작은 Tools > 로비 시설 오브젝트 배치가 선택창으로 바꿔 줍니다.
/// 이미 만들어진 창은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class MultiDispatchSetup : Editor
{
    const string MENU_NAME    = "Popup_QuestBoardMenu";
    const string PANEL_NAME   = "Popup_MultiDispatch";
    const string UNDO_NAME    = "동시 파견 UI 세팅";
    const float  PANEL_WIDTH  = 1200f;
    const int    DEFAULT_ROWS = 5; // 퀘스트 보드를 찾지 못했을 때의 의뢰 줄 수

    [MenuItem("Tools/동시 파견 UI 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[MultiDispatchSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[MultiDispatchSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[MultiDispatchSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        // 1. 퀘스트 보드 선택창
        Transform menu = canvas.transform.Find(MENU_NAME);
        if (menu == null)
        {
            Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, MENU_NAME, "퀘스트 보드", lobby.normalColor, font, out _);
            RectTransform windowRect = (RectTransform)window;
            windowRect.sizeDelta = new Vector2(700f, windowRect.sizeDelta.y);

            LobbyFacilitySetup.CreateMenuButton("Btn_Direct", window, "의뢰 수행 (직접 지휘)", LobbyFacilitySetup.MenuButtonColor, font, lobby.OnClickMenuDirectQuest);
            LobbyFacilitySetup.CreateMenuButton("Btn_Multi",  window, "동시 파견 (결과만 확인)", LobbyFacilitySetup.MenuButtonColor, font, lobby.OnClickMenuMultiDispatch);
            LobbyFacilitySetup.CreateMenuButton("Btn_Close",  window, "닫기", LobbyFacilitySetup.MenuCloseColor, font, lobby.CloseQuestBoardMenu);

            menu = window.parent;
            Debug.Log($"[MultiDispatchSetup] {MENU_NAME} 생성 완료");
        }

        // 2. 동시 파견 창
        Transform panelTransform = canvas.transform.Find(PANEL_NAME);
        MultiDispatchPanel panel = panelTransform != null ? panelTransform.GetComponent<MultiDispatchPanel>() : null;
        if (panel == null) panel = BuildPanel(canvas, lobby, font);
        else Debug.Log($"[MultiDispatchSetup] {PANEL_NAME} 이미 존재, 건너뜀");

        // 의뢰 생성기는 씬에 있는 것을 그대로 사용
        if (panel.questGenerator == null)
        {
            QuestGenerator[] generators = Object.FindObjectsByType<QuestGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (generators.Length > 0)
            {
                panel.questGenerator = generators[0];
                EditorUtility.SetDirty(panel);
            }
            else
            {
                Debug.LogWarning("[MultiDispatchSetup] 씬에서 QuestGenerator를 찾지 못했습니다. MultiDispatchPanel.questGenerator를 직접 연결해주세요.");
            }
        }

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.questBoardMenuPopup = menu.gameObject;
        lobby.multiDispatchPanel = panel;
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 동시 파견 UI 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    static MultiDispatchPanel BuildPanel(Canvas canvas, LobbyManager lobby, TMP_FontAsset font)
    {
        Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, PANEL_NAME, "동시 파견", lobby.normalColor, font, out _);
        RectTransform windowRect = (RectTransform)window;
        windowRect.sizeDelta = new Vector2(PANEL_WIDTH, windowRect.sizeDelta.y);
        window.GetComponent<VerticalLayoutGroup>().spacing = 12f;

        MultiDispatchPanel panel = window.parent.gameObject.AddComponent<MultiDispatchPanel>();

        TextMeshProUGUI desc = LobbyFacilitySetup.CreateMenuText("Txt_Desc", window,
            "여러 파티를 각각 다른 의뢰에 보냅니다. 전투는 보지 않고 결과만 확인합니다.", 24, font);
        desc.color = new Color(0.85f, 0.8f, 0.7f);

        // 의뢰 줄 수는 퀘스트 보드에 표시되는 의뢰 수와 맞춤
        QuestBoardPopup[] boards = Object.FindObjectsByType<QuestBoardPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int rowCount = boards.Length > 0 ? Mathf.Max(1, boards[0].questSlotCount) : DEFAULT_ROWS;

        panel.rows = new MultiDispatchPanel.Row[rowCount];
        for (int i = 0; i < rowCount; i++)
            panel.rows[i] = CreateRow(window, panel, i, font);

        panel.statusText = LobbyFacilitySetup.CreateMenuText("Txt_Status", window, "선택한 파티: 0팀", 26, font);

        LobbyFacilitySetup.CreateMenuButton("Btn_Start", window, "출발", LobbyFacilitySetup.MenuButtonColor, font, panel.OnClickStart);
        Transform startButton = window.Find("Btn_Start");
        panel.startButton = startButton != null ? startButton.GetComponent<Button>() : null;

        LobbyFacilitySetup.CreateMenuButton("Btn_Close", window, "닫기", LobbyFacilitySetup.MenuCloseColor, font, panel.Close);

        Debug.Log($"[MultiDispatchSetup] {PANEL_NAME} 생성 완료 (의뢰 {rowCount}줄)");
        return panel;
    }

    // 한 줄: [의뢰 정보 텍스트 ........] [파티 선택 버튼]
    static MultiDispatchPanel.Row CreateRow(Transform window, MultiDispatchPanel panel, int index, TMP_FontAsset font)
    {
        GameObject rowGO = new GameObject($"Row_{index + 1}", typeof(RectTransform));
        rowGO.layer = window.gameObject.layer;
        rowGO.transform.SetParent(window, false);

        Image rowBg = rowGO.AddComponent<Image>();
        rowBg.color = new Color(0f, 0f, 0f, 0.3f);
        rowBg.raycastTarget = false;

        HorizontalLayoutGroup layout = rowGO.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 10, 10);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 의뢰 정보 (남는 폭을 모두 차지)
        TextMeshProUGUI info = LobbyFacilitySetup.CreateMenuText("Txt_Info", rowGO.transform, "[D] 의뢰 이름\n보상 0 G", 24, font);
        info.alignment = TextAlignmentOptions.Left;
        info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // 파티 선택 버튼 (누를 때마다 다음 파티로 바뀜)
        GameObject btnGO = new GameObject("Btn_Party", typeof(RectTransform));
        btnGO.layer = rowGO.layer;
        btnGO.transform.SetParent(rowGO.transform, false);

        LayoutElement btnLayout = btnGO.AddComponent<LayoutElement>();
        btnLayout.preferredWidth = 340f;
        btnLayout.preferredHeight = 84f;

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = LobbyFacilitySetup.MenuButtonColor;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        UnityEventTools.AddIntPersistentListener(btn.onClick, panel.OnClickRowParty, index);

        TextMeshProUGUI label = LobbyFacilitySetup.CreateMenuText("Txt_Label", btnGO.transform, "파티 선택", 24, font);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return new MultiDispatchPanel.Row
        {
            root = rowGO,
            infoText = info,
            buttonLabel = label,
        };
    }
}
