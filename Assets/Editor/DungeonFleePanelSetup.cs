using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// [메뉴] Tools > 던전 도주 패널 세팅
/// DungeonScene에서 실행하면 Panel_Flee를 자동 생성하고
/// BattleManager.fleePanel에 연결합니다.
/// </summary>
public class DungeonFleePanelSetup : Editor
{
    [MenuItem("Tools/던전 도주 패널 세팅")]
    static void Setup()
    {
        // 1. Canvas 찾기
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.gameObject.name == "Canvas_UI") { canvas = c; break; }
        }
        if (canvas == null)
        {
            Debug.LogError("Canvas_UI를 찾을 수 없습니다! DungeonScene을 열고 실행해주세요.");
            return;
        }

        // 2. BattleManager 찾기
        BattleManager bm = FindFirstObjectByType<BattleManager>();
        if (bm == null)
        {
            Debug.LogError("BattleManager를 찾을 수 없습니다!");
            return;
        }

        // 3. 이미 있으면 건너뛰기
        Transform existing = canvas.transform.Find("Panel_Flee");
        if (existing != null)
        {
            Debug.Log("Panel_Flee가 이미 존재합니다. BattleManager 연결만 확인합니다.");
            bm.fleePanel = existing.gameObject;
            EditorUtility.SetDirty(bm);
            return;
        }

        // ──────────────────────────────────────────────
        // 4. Panel_Flee 생성 (Panel_Victory/Defeat과 동일한 구조)
        // ──────────────────────────────────────────────

        // 4-1. 반투명 배경 패널
        GameObject panelGO = new GameObject("Panel_Flee");
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.75f); // 어두운 반투명
        panelBg.raycastTarget = true;

        // 4-2. 내부 컨테이너 (중앙 정렬)
        GameObject containerGO = new GameObject("Container");
        containerGO.transform.SetParent(panelGO.transform, false);

        RectTransform containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(500, 400);
        containerRect.anchoredPosition = Vector2.zero;

        Image containerBg = containerGO.AddComponent<Image>();
        containerBg.color = new Color(0.15f, 0.12f, 0.08f, 0.95f); // 어두운 갈색

        VerticalLayoutGroup layout = containerGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.spacing = 15;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 4-3. 제목 텍스트
        GameObject titleGO = new GameObject("Txt_FleeTitle");
        titleGO.transform.SetParent(containerGO.transform, false);

        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 50);

        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "도주 성공!";
        titleText.fontSize = 36;
        titleText.color = new Color(1f, 0.85f, 0.4f); // 금색
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        titleText.raycastTarget = false;

        // 4-4. 결과 텍스트 (BattleManager가 동적으로 채움)
        GameObject resultGO = new GameObject("Txt_FleeResult");
        resultGO.transform.SetParent(containerGO.transform, false);

        RectTransform resultRect = resultGO.AddComponent<RectTransform>();
        resultRect.sizeDelta = new Vector2(0, 200);

        TextMeshProUGUI resultText = resultGO.AddComponent<TextMeshProUGUI>();
        resultText.text = "도주 결과가 여기에 표시됩니다.";
        resultText.fontSize = 22;
        resultText.color = Color.white;
        resultText.alignment = TextAlignmentOptions.TopLeft;
        resultText.enableWordWrapping = true;
        resultText.richText = true;
        resultText.raycastTarget = false;

        // 4-5. 로비 복귀 버튼
        GameObject btnGO = new GameObject("Btn_ReturnToLobby");
        btnGO.transform.SetParent(containerGO.transform, false);

        RectTransform btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(0, 55);

        Image btnImage = btnGO.AddComponent<Image>();
        btnImage.color = new Color(0.6f, 0.35f, 0.15f); // 갈색 버튼

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.75f, 0.45f, 0.2f);
        colors.pressedColor = new Color(0.5f, 0.28f, 0.1f);
        btn.colors = colors;

        // 버튼 텍스트
        GameObject btnTextGO = new GameObject("Text (TMP)");
        btnTextGO.transform.SetParent(btnGO.transform, false);

        RectTransform btnTextRect = btnTextGO.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnText.text = "로비로 돌아가기";
        btnText.fontSize = 24;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.raycastTarget = false;

        // 버튼 클릭 → BattleManager.OnClick_ReturnToLobby 연결
        UnityAction returnAction = new UnityAction(bm.OnClick_ReturnToLobby);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, returnAction);

        // ──────────────────────────────────────────────
        // 5. BattleManager에 연결
        // ──────────────────────────────────────────────
        bm.fleePanel = panelGO;

        // 6. 기본 비활성화
        panelGO.SetActive(false);

        // 7. Victory/Defeat 패널 바로 뒤에 배치
        Transform defeatPanel = canvas.transform.Find("Panel_Defeat");
        if (defeatPanel != null)
            panelGO.transform.SetSiblingIndex(defeatPanel.GetSiblingIndex() + 1);

        // Dirty 표시
        EditorUtility.SetDirty(panelGO);
        EditorUtility.SetDirty(bm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("=== 던전 도주 패널 세팅 완료! ===\n" +
                  "Panel_Flee 생성 → BattleManager.fleePanel 연결 완료.\n" +
                  "씬을 저장(Ctrl+S)하세요.");
    }
}
