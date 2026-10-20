using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 치유소 세팅
/// LobbyScene에 치유소 창(결함 치료)을 만들고 LobbyManager.clinicPanel에 연결합니다.
/// 목록 한 줄은 신전 슬롯 프리팹(Slot_Temple)을 함께 씁니다 (없으면 Tools > 신전 화면 세팅을 먼저 실행).
/// 치유소 건물은 Tools > 길드 밖 화면 세팅이 만듭니다. 이미 만들어진 창은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class ClinicSetup : Editor
{
    const string POPUP_NAME  = "Popup_Clinic";
    const string SLOT_PATH   = "Assets/Prefabs/UI/Slot_Temple.prefab";
    const string UNDO_NAME   = "치유소 세팅";
    const float  PANEL_WIDTH = 1100f;

    [MenuItem("Tools/치유소 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[ClinicSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[ClinicSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ClinicSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        Transform popupTransform = canvas.transform.Find(POPUP_NAME);
        ClinicPanel panel = popupTransform != null ? popupTransform.GetComponent<ClinicPanel>() : null;
        if (panel == null) panel = BuildPanel(canvas, lobby, font);
        else Debug.Log($"[ClinicSetup] {POPUP_NAME} 이미 존재, 건너뜀");

        if (panel.slotPrefab == null)
        {
            panel.slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SLOT_PATH);
            if (panel.slotPrefab == null) Debug.LogError($"[ClinicSetup] {SLOT_PATH}가 없습니다. Tools > 신전 화면 세팅을 먼저 실행하세요.");
            EditorUtility.SetDirty(panel);
        }

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.clinicPanel = panel;
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 치유소 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    static ClinicPanel BuildPanel(Canvas canvas, LobbyManager lobby, TMP_FontAsset font)
    {
        Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, POPUP_NAME, "치유소", lobby.normalColor, font, out _);
        RectTransform windowRect = (RectTransform)window;
        windowRect.sizeDelta = new Vector2(PANEL_WIDTH, windowRect.sizeDelta.y);
        window.GetComponent<VerticalLayoutGroup>().spacing = 12f;

        ClinicPanel panel = window.parent.gameObject.AddComponent<ClinicPanel>();

        TextMeshProUGUI desc = LobbyFacilitySetup.CreateMenuText("Txt_Desc", window,
            "몸에 남은 결함(외팔이·절름발이·외눈박이·약골)을 치료합니다.", 24, font);
        desc.color = new Color(0.85f, 0.8f, 0.7f);

        panel.goldText = LobbyFacilitySetup.CreateMenuText("Txt_Gold", window, "보유 골드: 0 G", 28, font);
        panel.goldText.color = new Color(1f, 0.85f, 0.4f);

        TempleSetup.CreateListPanel("Panel_Defects", window, font, "치료할 결함을 가진 모험가가 없습니다.",
            out panel.emptyText, out panel.listContent);

        panel.messageText = LobbyFacilitySetup.CreateMenuText("Txt_Message", window, "", 24, font);
        panel.messageText.color = new Color(0.9f, 0.9f, 0.9f);

        LobbyFacilitySetup.CreateMenuButton("Btn_Close", window, "닫기", LobbyFacilitySetup.MenuCloseColor, font, panel.Close);

        Debug.Log($"[ClinicSetup] {POPUP_NAME} 생성 완료");
        return panel;
    }
}
