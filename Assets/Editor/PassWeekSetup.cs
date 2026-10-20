using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 한 주 보내기 버튼 세팅
/// 로비 화면 오른쪽 아래에 [한 주 보내기] 버튼과 확인창을 만들고 LobbyManager에 연결합니다.
/// 파견을 보내지 않아도 시간을 넘길 수 있게 해 줍니다.
/// 이미 만들어진 버튼/확인창은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class PassWeekSetup : Editor
{
    const string BUTTON_NAME = "Btn_PassWeek";
    const string POPUP_NAME  = "Popup_PassWeek";
    const string UNDO_NAME   = "한 주 보내기 버튼 세팅";

    [MenuItem("Tools/한 주 보내기 버튼 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[PassWeekSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[PassWeekSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PassWeekSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        // 1. 로비 화면의 버튼
        if (canvas.transform.Find(BUTTON_NAME) == null)
            CreateButton(canvas, lobby, font);
        else
            Debug.Log($"[PassWeekSetup] {BUTTON_NAME} 이미 존재, 건너뜀");

        // 2. 확인창
        Transform popup = canvas.transform.Find(POPUP_NAME);
        if (popup == null)
        {
            Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, POPUP_NAME, "한 주 보내기", lobby.normalColor, font, out _);
            RectTransform windowRect = (RectTransform)window;
            windowRect.sizeDelta = new Vector2(860f, windowRect.sizeDelta.y);

            LobbyFacilitySetup.CreateMenuText("Txt_Message", window, "다음 주로 넘어갑니다.", 28, font);
            LobbyFacilitySetup.CreateMenuButton("Btn_Confirm", window, "보내기", LobbyFacilitySetup.MenuButtonColor, font, lobby.OnClickConfirmPassWeek);
            LobbyFacilitySetup.CreateMenuButton("Btn_Cancel",  window, "취소",   LobbyFacilitySetup.MenuCloseColor,  font, lobby.ClosePassWeekPopup);

            popup = window.parent;
            Debug.Log($"[PassWeekSetup] {POPUP_NAME} 생성 완료");
        }

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.passWeekPopup = popup.gameObject;
        Transform message = popup.Find("Window/Txt_Message");
        lobby.passWeekMessageText = message != null ? message.GetComponent<TextMeshProUGUI>() : null;
        EditorUtility.SetDirty(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 한 주 보내기 버튼 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    static void CreateButton(Canvas canvas, LobbyManager lobby, TMP_FontAsset font)
    {
        GameObject go = new GameObject(BUTTON_NAME, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(canvas.transform, false);

        // 화면 오른쪽 아래
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        rect.sizeDelta = new Vector2(280f, 84f);

        Image img = go.AddComponent<Image>();
        img.color = LobbyFacilitySetup.MenuButtonColor;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        UnityEventTools.AddPersistentListener(btn.onClick, lobby.OnClickPassWeek);

        TextMeshProUGUI label = LobbyFacilitySetup.CreateMenuText("Txt_Label", go.transform, "한 주 보내기", 32, font);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        // 상단 바 바로 뒤에 두어, 팝업과 전체 화면(접수처, 길드 밖)이 이 버튼을 덮도록 함
        Transform topBar = lobby.goldText != null ? lobby.goldText.transform.parent : null;
        if (topBar != null && topBar.parent == canvas.transform)
            go.transform.SetSiblingIndex(topBar.GetSiblingIndex() + 1);

        Debug.Log($"[PassWeekSetup] {BUTTON_NAME} 생성 완료");
    }
}
