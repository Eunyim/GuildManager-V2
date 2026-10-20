using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 상단 정보 표시 세팅
/// 로비 상단 바(TopBar)에 명성 / 영혼석 표시를 추가하고, 그 아래에 시설 레벨을 한 줄로 보여주는 바를 만듭니다.
/// 만든 텍스트는 LobbyManager에 연결되어 RefreshUI()에서 갱신됩니다.
/// 상단 바와 시설 바 모두 화면을 같은 폭의 칸으로 나눠 가운데 정렬하므로, 위아래 항목이 같은 세로줄에 놓입니다.
/// 이미 연결된 항목은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class TopBarSetup : Editor
{
    const string FACILITY_BAR_NAME = "FacilityBar";
    const string UNDO_NAME = "상단 정보 표시 세팅";

    [MenuItem("Tools/상단 정보 표시 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[TopBarSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null || lobby.goldText == null)
        {
            Debug.LogError("[TopBarSetup] LobbyManager 또는 상단 바의 골드 텍스트를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        // 기존 골드 텍스트가 있는 곳이 상단 바. 글꼴과 크기도 거기에 맞춤
        RectTransform topBar = (RectTransform)lobby.goldText.transform.parent;
        TMP_FontAsset font = lobby.goldText.font;
        float fontSize = lobby.goldText.fontSize;

        Undo.RecordObject(lobby, UNDO_NAME);

        // 1. 골드 양옆에 명성 / 영혼석
        if (lobby.reputationText == null)
            lobby.reputationText = CreateBarText(topBar, "Txt_Reputation", "명성: 0", 340f, fontSize, new Color(0.6f, 0.85f, 1f), font);
        if (lobby.spiritStoneText == null)
            lobby.spiritStoneText = CreateBarText(topBar, "Txt_SpiritStone", "영혼석 x0", -340f, fontSize, new Color(0.8f, 0.7f, 1f), font);

        // 2. 상단 바 바로 아래에 시설 레벨 한 줄
        Transform facilityBar = topBar.Find(FACILITY_BAR_NAME);
        if (facilityBar == null) facilityBar = CreateFacilityBar(topBar);

        if (lobby.receptionLevelText == null)  lobby.receptionLevelText  = CreateFacilityText(facilityBar, "Txt_Reception",  "접수처 Lv.1", font);
        if (lobby.questBoardLevelText == null) lobby.questBoardLevelText = CreateFacilityText(facilityBar, "Txt_QuestBoard", "퀘스트 보드 Lv.1", font);
        if (lobby.innLevelText == null)        lobby.innLevelText        = CreateFacilityText(facilityBar, "Txt_Inn",        "여관 Lv.1", font);
        if (lobby.pubLevelText == null)        lobby.pubLevelText        = CreateFacilityText(facilityBar, "Txt_Pub",        "주점 Lv.1", font);
        if (lobby.templeLevelText == null)     lobby.templeLevelText     = CreateFacilityText(facilityBar, "Txt_Temple",     "신전 Lv.1", font);

        // 3. 간격 정렬: 두 줄 모두 같은 폭의 칸으로 나눠 가운데 정렬
        HorizontalLayoutGroup layoutGroup = facilityBar.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null) Undo.DestroyObjectImmediate(layoutGroup); // 글자 길이에 따라 간격이 달라지므로 사용하지 않음

        AlignInColumns(new[] { lobby.guildNameText, lobby.spiritStoneText, lobby.goldText, lobby.reputationText, lobby.dayText });
        AlignInColumns(new[] { lobby.receptionLevelText, lobby.questBoardLevelText, lobby.innLevelText, lobby.pubLevelText, lobby.templeLevelText });

        EditorUtility.SetDirty(lobby);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log("=== 상단 정보 표시 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    // 부모를 텍스트 개수만큼 같은 폭의 칸으로 나누고, 각 텍스트를 자기 칸에 꽉 채워 가운데 정렬
    static void AlignInColumns(TextMeshProUGUI[] texts)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI tmp = texts[i];
            if (tmp == null) continue;

            RectTransform rect = tmp.rectTransform;
            Undo.RecordObject(rect, UNDO_NAME);
            rect.anchorMin = new Vector2(i / (float)texts.Length, 0f);
            rect.anchorMax = new Vector2((i + 1) / (float)texts.Length, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rect);

            Undo.RecordObject(tmp, UNDO_NAME);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            EditorUtility.SetDirty(tmp);
        }
    }

    // 상단 바 가운데(골드) 기준으로 x만큼 떨어진 텍스트
    static TextMeshProUGUI CreateBarText(RectTransform topBar, string name, string text, float x, float fontSize, Color color, TMP_FontAsset font)
    {
        TextMeshProUGUI tmp = CreateText(topBar, name, text, fontSize, color, font);
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(320f, 50f);
        return tmp;
    }

    // 상단 바 아래쪽에 붙는 얇은 바 (시설 레벨 텍스트의 배치는 AlignInColumns가 담당)
    static Transform CreateFacilityBar(RectTransform topBar)
    {
        GameObject barGO = new GameObject(FACILITY_BAR_NAME, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(barGO, UNDO_NAME);
        barGO.layer = topBar.gameObject.layer;
        barGO.transform.SetParent(topBar, false);

        RectTransform rect = barGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 40f);

        Image bg = barGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.4f);
        bg.raycastTarget = false;

        return barGO.transform;
    }

    static TextMeshProUGUI CreateFacilityText(Transform bar, string name, string text, TMP_FontAsset font)
    {
        TextMeshProUGUI tmp = CreateText(bar, name, text, 22f, new Color(0.9f, 0.86f, 0.75f), font);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }
}
