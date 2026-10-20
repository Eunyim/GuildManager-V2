using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 시설 업그레이드 UI 세팅
/// LobbyScene에 건축 공방 창(시설별 레벨/효과/비용 + 업그레이드 버튼)을 만들고 LobbyManager에 연결합니다.
/// 창은 길드 밖 화면(Tools > 길드 밖 화면 세팅)의 [건축 공방]에서 엽니다.
/// 이미 만들어진 창은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class FacilityUpgradeSetup : Editor
{
    const string POPUP_NAME   = "Popup_FacilityUpgrade";
    const string UNDO_NAME    = "시설 업그레이드 UI 세팅";
    const string WINDOW_TITLE = "건축 공방";
    const float  WINDOW_WIDTH = 1100f;

    // 창에 표시할 시설 (위에서부터 순서대로)
    static readonly string[] facilityTypes = { "reception", "questboard", "inn", "temple", "pub" };

    [MenuItem("Tools/시설 업그레이드 UI 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[FacilityUpgradeSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[FacilityUpgradeSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[FacilityUpgradeSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        Transform popup = canvas.transform.Find(POPUP_NAME);
        FacilityUpgradePanel panel = popup != null ? popup.GetComponent<FacilityUpgradePanel>() : null;
        if (panel == null) panel = BuildPanel(canvas, lobby);
        else Debug.Log($"[FacilityUpgradeSetup] {POPUP_NAME} 이미 존재, 건너뜀");

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.facilityUpgradePanel = panel;

        // 예전 제목("길드 시설")으로 만들어진 창도 맞춰 줌
        Transform title = panel.transform.Find("Window/Txt_Title");
        TextMeshProUGUI titleText = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
        if (titleText != null && titleText.text != WINDOW_TITLE)
        {
            Undo.RecordObject(titleText, UNDO_NAME);
            titleText.text = WINDOW_TITLE;
            EditorUtility.SetDirty(titleText);
        }
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 시설 업그레이드 UI 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    static FacilityUpgradePanel BuildPanel(Canvas canvas, LobbyManager lobby)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, POPUP_NAME, WINDOW_TITLE, lobby.normalColor, font, out _);
        RectTransform windowRect = (RectTransform)window;
        windowRect.sizeDelta = new Vector2(WINDOW_WIDTH, windowRect.sizeDelta.y);
        window.GetComponent<VerticalLayoutGroup>().spacing = 14f;

        FacilityUpgradePanel panel = window.parent.gameObject.AddComponent<FacilityUpgradePanel>();

        TextMeshProUGUI goldText = LobbyFacilitySetup.CreateMenuText("Txt_Gold", window, "보유 골드: 0 G", 30, font);
        goldText.color = new Color(1f, 0.85f, 0.3f);
        panel.goldText = goldText;

        panel.rows = new FacilityUpgradePanel.Row[facilityTypes.Length];
        for (int i = 0; i < facilityTypes.Length; i++)
            panel.rows[i] = CreateRow(window, panel, facilityTypes[i], font);

        LobbyFacilitySetup.CreateMenuButton("Btn_Close", window, "닫기", LobbyFacilitySetup.MenuCloseColor, font, panel.Close);

        Debug.Log($"[FacilityUpgradeSetup] {POPUP_NAME} 생성 완료");
        return panel;
    }

    // 한 줄: [시설 정보 텍스트 ........] [업그레이드 버튼]
    static FacilityUpgradePanel.Row CreateRow(Transform window, FacilityUpgradePanel panel, string facilityType, TMP_FontAsset font)
    {
        GameObject rowGO = new GameObject($"Row_{facilityType}", typeof(RectTransform));
        rowGO.layer = window.gameObject.layer;
        rowGO.transform.SetParent(window, false);

        Image rowBg = rowGO.AddComponent<Image>();
        rowBg.color = new Color(0f, 0f, 0f, 0.3f);
        rowBg.raycastTarget = false;

        HorizontalLayoutGroup layout = rowGO.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 12, 12);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 정보 텍스트 (남는 폭을 모두 차지)
        TextMeshProUGUI info = LobbyFacilitySetup.CreateMenuText("Txt_Info", rowGO.transform, "시설 Lv.1\n현재: -\n다음: -", 24, font);
        info.alignment = TextAlignmentOptions.Left;
        info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // 업그레이드 버튼
        GameObject btnGO = new GameObject("Btn_Upgrade", typeof(RectTransform));
        btnGO.layer = rowGO.layer;
        btnGO.transform.SetParent(rowGO.transform, false);

        LayoutElement btnLayout = btnGO.AddComponent<LayoutElement>();
        btnLayout.preferredWidth = 260f;
        btnLayout.preferredHeight = 96f;

        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = LobbyFacilitySetup.MenuButtonColor;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        UnityEventTools.AddStringPersistentListener(btn.onClick, panel.OnClickUpgrade, facilityType);

        TextMeshProUGUI label = LobbyFacilitySetup.CreateMenuText("Txt_Label", btnGO.transform, "업그레이드\n0 G", 26, font);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return new FacilityUpgradePanel.Row
        {
            facilityType = facilityType,
            infoText = info,
            button = btn,
            buttonLabel = label,
        };
    }
}
