using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 전투 결과창 세팅
/// LobbyScene에 전투 결과(결산) 팝업과 파산 팝업을 만들고 LobbyManager에 연결합니다.
/// 결과창의 [확인] 버튼이 한 주를 넘기므로(PassWeek), 이 팝업이 있어야 로비에서 시간이 흐릅니다.
/// 이미 만들어진 팝업은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class BattleResultPopupSetup : Editor
{
    const string RESULT_NAME   = "Popup_BattleResult";
    const string GAMEOVER_NAME = "Popup_GameOver";
    const string UNDO_NAME     = "전투 결과창 세팅";
    const float  WINDOW_WIDTH  = 760f;

    [MenuItem("Tools/전투 결과창 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[BattleResultPopupSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[BattleResultPopupSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[BattleResultPopupSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        Undo.RecordObject(lobby, UNDO_NAME);
        EnsureResultPopup(canvas, lobby, font);
        EnsureGameOverPopup(canvas, lobby, font);
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 전투 결과창 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    // ═══════════════════════════════════════════
    // 전투 결과(결산) 팝업: 제목 / 골드 / 명성 / 전리품 / 레벨업 / [확인]
    // ═══════════════════════════════════════════
    static void EnsureResultPopup(Canvas canvas, LobbyManager lobby, TMP_FontAsset font)
    {
        Transform popup = canvas.transform.Find(RESULT_NAME);
        if (popup == null)
        {
            Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, RESULT_NAME, "던전 클리어!", lobby.normalColor, font, out _);
            SetWindowWidth(window);

            CreateBodyText("Txt_Gold",       window, "골드: +0 G", 32, new Color(1f, 0.85f, 0.3f), font);
            CreateBodyText("Txt_Reputation", window, "명성: +0",   32, new Color(0.6f, 0.85f, 1f), font);
            CreateBodyText("Txt_Items",      window, "전리품 없음", 28, Color.white, font);
            CreateBodyText("Txt_LevelUp",    window, "",           28, new Color(0.6f, 1f, 0.6f), font);
            LobbyFacilitySetup.CreateMenuButton("Btn_Confirm", window, "확인 (다음 주로)", LobbyFacilitySetup.MenuButtonColor, font, lobby.OnClickConfirmBattleResult);

            popup = window.parent;
            Debug.Log("[BattleResultPopupSetup] 전투 결과창 생성 완료");
        }

        lobby.resultPopup          = popup.gameObject;
        lobby.resultTitleText      = FindText(popup, "Window/Txt_Title");
        lobby.resultGoldText       = FindText(popup, "Window/Txt_Gold");
        lobby.resultReputationText = FindText(popup, "Window/Txt_Reputation");
        lobby.resultItemsText      = FindText(popup, "Window/Txt_Items");
        lobby.resultLevelUpText    = FindText(popup, "Window/Txt_LevelUp");
    }

    // ═══════════════════════════════════════════
    // 파산 팝업: 월급을 내지 못해 골드가 마이너스가 되면 표시
    // ═══════════════════════════════════════════
    static void EnsureGameOverPopup(Canvas canvas, LobbyManager lobby, TMP_FontAsset font)
    {
        Transform popup = canvas.transform.Find(GAMEOVER_NAME);
        if (popup == null)
        {
            Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, GAMEOVER_NAME, "길드 파산", lobby.normalColor, font, out TextMeshProUGUI title);
            title.color = Color.red;
            SetWindowWidth(window);

            CreateBodyText("Txt_Message", window, "모험가들의 월급을 지급하지 못해\n길드가 문을 닫았습니다.", 30, Color.white, font);
            LobbyFacilitySetup.CreateMenuButton("Btn_Title", window, "타이틀로 돌아가기", LobbyFacilitySetup.MenuButtonColor, font, lobby.OnClickGameOverConfirm);

            popup = window.parent;
            Debug.Log("[BattleResultPopupSetup] 파산 팝업 생성 완료");
        }

        lobby.gameOverPopup = popup.gameObject;
    }

    static void SetWindowWidth(Transform window)
    {
        RectTransform rect = (RectTransform)window;
        rect.sizeDelta = new Vector2(WINDOW_WIDTH, rect.sizeDelta.y);
    }

    static TextMeshProUGUI CreateBodyText(string name, Transform window, string text, float size, Color color, TMP_FontAsset font)
    {
        TextMeshProUGUI tmp = LobbyFacilitySetup.CreateMenuText(name, window, text, size, font);
        tmp.color = color;
        return tmp;
    }

    static TextMeshProUGUI FindText(Transform popup, string path)
    {
        Transform t = popup.Find(path);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }
}
