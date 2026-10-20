using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

// UI 배치 정리 (한 번 실행하면 프리팹·씬에 저장됨. 여러 번 실행해도 결과는 같음)
//  1. 의뢰 칸(Slot_Quest): 이름이 두 줄로 꺾이고 오른쪽 칸이 칸 밖으로 잘리던 문제
//     → 이름(위) / 설명(아래, 몬스터·권장 레벨) / 보상(가운데) / 오른쪽 칸을 칸 안에 나눠 배치하고, 길면 글자를 줄임
//  2. 파티 이름 입력 창(LobbyScene/Popup_InputName): 너무 작고 제목·한국어 안내가 없던 문제
public static class UILayoutFixes
{
    const string SlotQuestPath = "Assets/Prefabs/UI/Slot_Quest.prefab";
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    const string FontPath = "Assets/Fonts/DungGeunMo SDF.asset";

    const string SlotMemberPath = "Assets/Prefabs/UI/Slot_Member.prefab";

    [MenuItem("Tools/UI 배치 정리 (의뢰 칸·이름 입력 창)")]
    static void Run()
    {
        FixQuestSlot();
        FixMemberSlot();
        FixInputNamePopup(); // LobbyScene을 열고 저장함 (아래 의뢰 게시판 버튼도 같은 씬)
        FixQuestCloseButton();
        FixMemberCountText();
        FixQuestWindowSize();
    }

    // ─── 5. 의뢰 게시판 창: 지역 카드 5장이 가로로 들어가도록 넓힘 ───────────
    static void FixQuestWindowSize()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != LobbyScenePath) return;
        Transform window = null;
        foreach (GameObject g in scene.GetRootGameObjects())
        {
            Transform t = g.transform.Find("Popup_Quest/Window");
            if (t != null) window = t;
        }
        if (window == null) { Debug.LogWarning("[UILayoutFixes] Popup_Quest/Window를 찾지 못했습니다."); return; }
        RectTransform rt = (RectTransform)window;
        Undo.RecordObject(rt, "의뢰 게시판 창 크기");
        rt.sizeDelta = new Vector2(1720f, 780f);
        rt.anchoredPosition = new Vector2(0f, -20f);

        // 지역 카드 순서 (왼쪽부터)
        QuestBoardPopup board = window.GetComponentInParent<QuestBoardPopup>(true);
        if (board != null)
        {
            Undo.RecordObject(board, "지역 카드 순서");
            board.regionCardOrder = new[] { "초원", "숲", "지하 묘지", "오크 부락", "드래곤 둥지" };
            board.regionCardArt = new[] { "prairie", "forest", "crypt", "orc", "dragon" };
            board.regionArtSize = new Vector2(288f, 360f);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UILayoutFixes] 의뢰 게시판 창을 1720x780으로 넓히고 LobbyScene을 저장했습니다.");
    }

    // ─── 4. 파티 멤버 추가 창: "현재 인원" 글자가 제목("모험가 명단")과 같은 자리에 겹쳐 깨져 보이던 문제 ───
    // → 제목 줄 오른쪽 끝에 작게 (제목은 가운데 정렬이라 겹치지 않음)
    static void FixMemberCountText()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != LobbyScenePath) return;
        Transform count = null;
        foreach (GameObject g in scene.GetRootGameObjects())
        {
            Transform t = g.transform.Find("Popup_MemberSelect/Window/Txt_Count");
            if (t != null) count = t;
        }
        if (count == null) { Debug.LogWarning("[UILayoutFixes] Popup_MemberSelect/Window/Txt_Count를 찾지 못했습니다."); return; }

        RectTransform rt = (RectTransform)count;
        Undo.RecordObject(rt, "인원 표시");
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 44f);
        rt.anchoredPosition = new Vector2(-44f, -60f);
        count.SetAsLastSibling(); // 목록 위에 그려지도록
        TextMeshProUGUI text = count.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            Undo.RecordObject(text, "인원 표시");
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.fontSize = 26f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UILayoutFixes] 멤버 추가 창의 인원 표시를 제목 줄 오른쪽으로 옮기고 LobbyScene을 저장했습니다.");
    }

    // ─── 1-2. 모험가 칸 (파티 멤버 추가·파티 상세) ─────────────────
    // 이름 칸이 좁고 줄바꿈이 켜져 있어 "(부상:2주)"·사기 표시가 붙으면 여러 줄로 꺾여 옆 글자와 겹치던 문제
    static void FixMemberSlot()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SlotMemberPath);
        try
        {
            // 코드(LobbyManager.SetupSlotBasic)는 0 이름, 1 직업, 2 등급 순서로 채움
            TextMeshProUGUI name = Find(root, "Txt_Name");
            TextMeshProUGUI job  = Find(root, "Txt_Job");
            TextMeshProUGUI rank = Find(root, "Txt_Rank");
            Place(name, new Vector2(0f, 0f),    new Vector2(0.58f, 1f), new Vector2(24f, 0f), new Vector2(0f, 0f));
            Place(job,  new Vector2(0.58f, 0f), new Vector2(0.84f, 1f), new Vector2(8f, 0f),  new Vector2(-8f, 0f));
            Place(rank, new Vector2(0.84f, 0f), new Vector2(1f, 1f),    new Vector2(8f, 0f),  new Vector2(-24f, 0f));
            Style(name, TextAlignmentOptions.Left,   32f, 16f);
            Style(job,  TextAlignmentOptions.Center, 30f, 16f);
            Style(rank, TextAlignmentOptions.Right,  34f, 18f);
            PrefabUtility.SaveAsPrefabAsset(root, SlotMemberPath);
            Debug.Log("[UILayoutFixes] 모험가 칸(Slot_Member) 배치를 정리했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ─── 3. 의뢰 게시판 닫기 버튼 ────────────────────────────────
    // "수락"이라고 적힌 버튼이 다른 창(모험가 관리)을 닫는 함수에 연결돼 있어 게시판을 나갈 수 없던 문제
    static void FixQuestCloseButton()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != LobbyScenePath) return;
        LobbyManager lobby = null;
        Transform btnT = null;
        foreach (GameObject g in scene.GetRootGameObjects())
        {
            if (lobby == null) lobby = g.GetComponentInChildren<LobbyManager>(true);
            Transform t = g.transform.Find("Popup_Quest/Window/Btn_Close");
            if (t != null) btnT = t;
        }
        if (lobby == null || btnT == null) { Debug.LogWarning("[UILayoutFixes] 의뢰 게시판 닫기 버튼을 찾지 못했습니다."); return; }

        Button btn = btnT.GetComponent<Button>();
        Undo.RecordObject(btn, "의뢰 게시판 닫기");
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(btn.onClick, i);
        UnityEventTools.AddPersistentListener(btn.onClick, lobby.CloseQuestPopup);

        TextMeshProUGUI label = btnT.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) { Undo.RecordObject(label, "의뢰 게시판 닫기"); label.text = "닫기"; }
        Image img = btnT.GetComponent<Image>();
        Sprite stone = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Theme/ui_button_stone.png");
        if (img != null && stone != null) { Undo.RecordObject(img, "의뢰 게시판 닫기"); img.sprite = stone; img.type = Image.Type.Sliced; }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UILayoutFixes] 의뢰 게시판 버튼을 [닫기]로 고치고 LobbyScene을 저장했습니다.");
    }

    // ─── 1. 의뢰 칸 ──────────────────────────────────────────────
    static void FixQuestSlot()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SlotQuestPath);
        try
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // 코드(QuestBoardPopup.AddSlot)는 글자 칸을 순서대로 채움: 0 이름, 1 보상, 2 오른쪽 칸, 3 설명
            TextMeshProUGUI name   = Find(root, "Txt_Name");
            TextMeshProUGUI reward = Find(root, "Txt_Reward");
            TextMeshProUGUI rank   = Find(root, "Txt_Rank");
            TextMeshProUGUI detail = Find(root, "Txt_Detail");
            if (detail == null)
            {
                GameObject go = new GameObject("Txt_Detail", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(root.transform, false);
                detail = go.GetComponent<TextMeshProUGUI>();
                if (font != null) detail.font = font;
                detail.color = new Color32(0xB8, 0xA5, 0x8A, 0xFF); // 흐린 양피지색 (테마가 바꾸지 않는 색)
            }
            detail.transform.SetAsLastSibling(); // 4번째 글자 칸이 되도록

            // 왼쪽 42%: 이름(위) + 설명(아래) / 가운데 24%: 보상 / 오른쪽 34%: 상태·종류
            Place(name,   new Vector2(0f, 0.42f),    new Vector2(0.42f, 1f),  new Vector2(24f, 0f), new Vector2(0f, -6f));
            Place(detail, new Vector2(0f, 0f),       new Vector2(0.42f, 0.42f), new Vector2(24f, 6f), new Vector2(0f, 0f));
            Place(reward, new Vector2(0.42f, 0f),    new Vector2(0.66f, 1f),  new Vector2(8f, 0f),  new Vector2(-8f, 0f));
            Place(rank,   new Vector2(0.66f, 0f),    new Vector2(1f, 1f),     new Vector2(8f, 0f),  new Vector2(-24f, 0f));

            Style(name,   TextAlignmentOptions.Left,   32f, 20f);
            Style(detail, TextAlignmentOptions.Left,   22f, 16f);
            Style(reward, TextAlignmentOptions.Center, 30f, 18f);
            Style(rank,   TextAlignmentOptions.Right,  28f, 16f);

            PrefabUtility.SaveAsPrefabAsset(root, SlotQuestPath);
            Debug.Log("[UILayoutFixes] 의뢰 칸(Slot_Quest) 배치를 정리했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static TextMeshProUGUI Find(GameObject root, string name)
    {
        foreach (TextMeshProUGUI t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.name == name) return t;
        return null;
    }

    static void Place(Graphic g, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (g == null) return;
        RectTransform rt = g.rectTransform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    // 한 줄로 쓰고, 칸보다 길면 글자 크기를 줄임 (최소 크기에서도 넘치면 … 처리)
    static void Style(TextMeshProUGUI t, TextAlignmentOptions align, float size, float minSize)
    {
        if (t == null) return;
        t.alignment = align == TextAlignmentOptions.Left ? TextAlignmentOptions.MidlineLeft
                    : align == TextAlignmentOptions.Right ? TextAlignmentOptions.MidlineRight
                    : TextAlignmentOptions.Midline;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = minSize;
        t.fontSize = size;
        t.raycastTarget = false;
    }

    // ─── 2. 파티 이름 입력 창 ─────────────────────────────────────
    static void FixInputNamePopup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != LobbyScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LobbyScenePath);
        }

        GameObject popup = null;
        foreach (GameObject g in scene.GetRootGameObjects())
        {
            Transform t = g.transform.Find("Popup_InputName");
            if (t != null) { popup = t.gameObject; break; }
        }
        if (popup == null) { Debug.LogWarning("[UILayoutFixes] Popup_InputName을 찾지 못했습니다."); return; }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Undo.RegisterFullObjectHierarchyUndo(popup, "UI 배치 정리: 이름 입력 창");

        // 화면 전체를 덮는 어두운 배경 (뒤쪽 클릭 막기)
        RectTransform root = (RectTransform)popup.transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image dim = popup.GetComponent<Image>();
        if (dim == null) dim = Undo.AddComponent<Image>(popup);
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        Transform outline = popup.transform.Find("Outline");
        if (outline != null) outline.gameObject.SetActive(false); // 창틀 스프라이트에 테두리가 있음

        RectTransform box = (RectTransform)popup.transform.Find("Image");
        if (box == null) { Debug.LogWarning("[UILayoutFixes] Popup_InputName/Image가 없습니다."); return; }
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(640f, 280f);
        box.anchoredPosition = Vector2.zero;

        // 제목
        Transform titleT = box.Find("Title");
        TextMeshProUGUI title = titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : null;
        if (title == null)
        {
            GameObject go = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "제목 추가");
            go.transform.SetParent(box, false);
            title = go.GetComponent<TextMeshProUGUI>();
            title.color = Color.white; // 처음 만들 때만 (이후엔 테마·손으로 정한 색을 유지)
        }
        if (font != null) title.font = font;
        title.text = "새 파티 이름";
        title.fontSize = 36f;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(560f, 50f));

        // 입력 칸
        TMP_InputField input = box.GetComponentInChildren<TMP_InputField>(true);
        if (input != null)
        {
            SetRect((RectTransform)input.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(480f, 64f));
            input.characterLimit = 12;
            if (input.textComponent != null)
            {
                if (font != null) input.textComponent.font = font;
                input.textComponent.fontSize = 30f;
                input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (input.placeholder is TextMeshProUGUI ph)
            {
                if (font != null) ph.font = font;
                ph.text = "이름을 입력하세요";
                ph.fontSize = 28f;
                ph.alignment = TextAlignmentOptions.MidlineLeft;
                ph.color = new Color(0.6f, 0.55f, 0.48f, 0.8f);
            }
        }

        // 버튼: 확인(왼쪽) / 취소(오른쪽)
        FixButton(box.Find("Btn_Confirm"), "확인", new Vector2(-110f, 52f), Color.white, font);
        FixButton(box.Find("Btn_Close"),   "취소", new Vector2(110f, 52f),  new Color(0.42f, 0.42f, 0.42f), font); // 회색 → 돌 버튼

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UILayoutFixes] 파티 이름 입력 창을 정리하고 LobbyScene을 저장했습니다.");
    }

    static void FixButton(Transform t, string label, Vector2 pos, Color normal, TMP_FontAsset font)
    {
        if (t == null) return;
        SetRect((RectTransform)t, new Vector2(0.5f, 0f), pos, new Vector2(180f, 52f));
        Button b = t.GetComponent<Button>();
        if (b != null)
        {
            ColorBlock cb = b.colors;
            cb.normalColor = normal;
            b.colors = cb;
        }
        TextMeshProUGUI text = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            if (font != null) text.font = font;
            text.text = label;
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
