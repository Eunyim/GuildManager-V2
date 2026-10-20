using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 던전 전투 UI 세팅
/// DungeonScene의 캔버스에 유닛 정보 말풍선(마우스를 올리면 이름/직업/레벨, 적은 종류 표시)을 만듭니다.
/// 이미 만들어져 있으면 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class DungeonBattleUISetup : Editor
{
    const string ROOT_NAME = "UnitHoverTooltip";
    const string UNDO_NAME = "던전 전투 UI 세팅";

    [MenuItem("Tools/던전 전투 UI 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[DungeonBattleUISetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        BattleManager bm = Object.FindFirstObjectByType<BattleManager>();
        if (bm == null)
        {
            Debug.LogError("[DungeonBattleUISetup] BattleManager를 찾을 수 없습니다. DungeonScene을 열고 실행해주세요.");
            return;
        }

        // 승리/패배 패널이 있는 캔버스를 사용
        Canvas canvas = bm.victoryPanel != null ? bm.victoryPanel.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DungeonBattleUISetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        if (canvas.transform.Find(ROOT_NAME) != null)
        {
            Debug.Log($"[DungeonBattleUISetup] {ROOT_NAME} 이미 존재, 건너뜀");
            return;
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);
        int uiLayer = LayerMask.NameToLayer("UI");

        // 화면 전체를 덮는 빈 부모 (말풍선 위치 계산의 기준). 결과 패널보다 뒤에 그려지도록 맨 앞 순서에 둠
        GameObject rootGO = new GameObject(ROOT_NAME, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(rootGO, UNDO_NAME);
        rootGO.layer = uiLayer;
        rootGO.transform.SetParent(canvas.transform, false);
        rootGO.transform.SetAsFirstSibling();
        RectTransform rootRect = rootGO.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // 말풍선 (글자 길이에 맞춰 크기가 정해짐)
        GameObject panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.layer = uiLayer;
        panelGO.transform.SetParent(rootGO.transform, false);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        bg.raycastTarget = false; // 마우스 판정을 가로채지 않도록

        VerticalLayoutGroup layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 10, 10);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI label = LobbyFacilitySetup.CreateMenuText("Txt_Info", panelGO.transform, "이름\n직업  Lv.1\nHP 100 / 100", 26, font);
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        UnitHoverTooltip tooltip = rootGO.AddComponent<UnitHoverTooltip>();
        tooltip.panel = panelRect;
        tooltip.label = label;

        panelGO.SetActive(false);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGO.scene);
        Debug.Log("=== 던전 전투 UI 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }
}
