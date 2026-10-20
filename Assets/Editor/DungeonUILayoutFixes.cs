using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 던전 UI 배치 정리 (한 번 실행하면 프리팹·씬에 저장됨. 여러 번 실행해도 결과는 같음)
//  1. 체력바(UnitHud 프리팹): 도트 스타일 — 어두운 테두리 안에 체력 줄(2칸)과 마나 줄(1칸). 색은 HPBar가 아군·적에 맞춰 칠함
//  2. 승리·패배·도주 창: 화면에 글자와 작은 버튼만 떠 있던 것을 창 하나에 담음 (창틀은 UI 테마 굽기가 입힘)
//  3. 도주 버튼: 화면 구석에 납작하게 붙어 있던 것을 키우고 여백을 줌, 빨간 버튼
//  4. 피해 숫자 폰트: 둥근모
public static class DungeonUILayoutFixes
{
    const string UnitHudPath = "Assets/Prefabs/UI/UnitHud.prefab";
    const string DungeonScenePath = "Assets/Scenes/DungeonScene.unity";
    const string FontPath = "Assets/Fonts/DungGeunMo SDF.asset";

    // 체력바 크기 (캔버스 단위, 월드 캔버스 배율 0.01 → 100 = 1 월드 유닛). 도트 한 칸 = 5
    const float Px = 5f;
    const float BarWidth = 16f * Px;                 // 0.8 월드 유닛
    const float HpRow = 2f * Px, MpRow = 1f * Px;
    const float FullHeight = Px + HpRow + Px + MpRow + Px; // 테두리, 체력, 구분선, 마나, 테두리
    const float NoMpHeight = Px + HpRow + Px;

    static readonly Color Outline = new Color32(0x14, 0x0C, 0x08, 0xFF);
    static readonly Color EmptyRow = new Color32(0x3A, 0x24, 0x18, 0xFF);
    static readonly Color MpColor = new Color32(0x4A, 0x8A, 0xE8, 0xFF);

    [MenuItem("Tools/던전 UI 배치 정리 (체력바·결과 창·도주 버튼)")]
    static void Run()
    {
        FixUnitHud();
        FixDungeonScene();
    }

    // ─── 1. 체력바 ───────────────────────────────────────────────
    static void FixUnitHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(UnitHudPath);
        try
        {
            RectTransform rt = (RectTransform)root.transform;
            rt.pivot = new Vector2(0.5f, 0f); // 아랫변이 머리 위에 오도록 (위치는 HPBar가 맞춤)
            rt.sizeDelta = new Vector2(BarWidth, FullHeight);

            HPBar bar = root.GetComponent<HPBar>();
            if (bar != null) bar.heightWithoutMp = NoMpHeight;

            // 바깥 테두리 (전체를 덮는 어두운 판)
            Transform frame = root.transform.Find("BackGround");
            if (frame != null)
            {
                Stretch((RectTransform)frame, Vector2.zero, Vector2.zero);
                Flat(frame.GetComponent<Image>(), Outline);
            }

            if (bar != null && bar.hpSlider != null) StyleSlider(bar.hpSlider, top: true, HpRow);
            if (bar != null && bar.mpSlider != null) StyleSlider(bar.mpSlider, top: false, MpRow, MpColor);

            PrefabUtility.SaveAsPrefabAsset(root, UnitHudPath);
            Debug.Log("[DungeonUILayoutFixes] 체력바(UnitHud) 모양을 정리했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 체력 줄은 위쪽, 마나 줄은 아래쪽에 테두리 한 칸 안으로 붙임
    static void StyleSlider(Slider slider, bool top, float height, Color? fillColor = null)
    {
        RectTransform rt = (RectTransform)slider.transform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(-2f * Px, height);
        rt.anchoredPosition = new Vector2(0f, top ? -Px : Px);
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        foreach (Transform child in slider.transform)
        {
            RectTransform c = (RectTransform)child;
            if (child == slider.fillRect || child == slider.handleRect) continue;
            Stretch(c, Vector2.zero, Vector2.zero); // 빈 줄 배경, 채움 영역
            Image img = child.GetComponent<Image>();
            if (img != null) Flat(img, EmptyRow);
        }
        if (slider.fillRect != null)
        {
            slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero;
            slider.fillRect.sizeDelta = Vector2.zero;
            Image fill = slider.fillRect.GetComponent<Image>();
            if (fill != null) Flat(fill, fillColor ?? fill.color);
        }
    }

    static void Flat(Image img, Color color)
    {
        if (img == null) return;
        img.sprite = null; // 단색 사각형 (도트처럼 또렷하게)
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
    }

    static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    // ─── 2~4. 던전 씬 ────────────────────────────────────────────
    static void FixDungeonScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(DungeonScenePath);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Transform canvas = null;
        BattleManager battle = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (canvas == null && go.name == "Canvas_UI") canvas = go.transform;
            if (battle == null) battle = go.GetComponentInChildren<BattleManager>(true);
        }
        if (canvas == null) { Debug.LogWarning("[DungeonUILayoutFixes] Canvas_UI를 찾지 못했습니다."); return; }

        // 결과 창: 승리·패배는 제목+버튼, 도주는 결과 글+버튼
        WrapInWindow(canvas.Find("Panel_Victory"), new Vector2(560f, 300f), font, titleSize: 64f, bodyRectTop: 0f);
        WrapInWindow(canvas.Find("Panel_Defeat"),  new Vector2(560f, 300f), font, titleSize: 64f, bodyRectTop: 0f);
        WrapInWindow(canvas.Find("Panel_Flee"),    new Vector2(760f, 600f), font, titleSize: 0f,  bodyRectTop: 1f);

        // 도주 버튼
        Transform flee = canvas.Find("Btn_Flee");
        if (flee != null)
        {
            Undo.RecordObject(flee, "던전 UI 배치 정리");
            RectTransform rt = (RectTransform)flee;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 60f);
            rt.anchoredPosition = new Vector2(24f + 100f, 24f + 30f);
            Image img = flee.GetComponent<Image>();
            Sprite red = Resources.Load<Sprite>("UI/Theme/ui_button_red");
            if (img != null && red != null)
            {
                Undo.RecordObject(img, "던전 UI 배치 정리");
                img.sprite = red;
                img.type = Image.Type.Sliced;
            }
            TextMeshProUGUI label = flee.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                Undo.RecordObject(label, "던전 UI 배치 정리");
                if (font != null) label.font = font;
                label.fontSize = 30f;
            }
        }

        // 피해 숫자 폰트
        if (battle != null && font != null && battle.damageTextFont != font)
        {
            Undo.RecordObject(battle, "던전 UI 배치 정리");
            battle.damageTextFont = font;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DungeonUILayoutFixes] 던전 씬(결과 창·도주 버튼·피해 숫자 폰트)을 정리하고 저장했습니다. 이어서 Tools > UI 테마 굽기를 실행하세요.");
    }

    // 화면 전체 어두운 판(panel) 안에 가운데 창(Window)을 만들고, 원래 자식(글·버튼)을 그 안으로 옮김
    static void WrapInWindow(Transform panel, Vector2 size, TMP_FontAsset font, float titleSize, float bodyRectTop)
    {
        if (panel == null) return;
        Transform window = panel.Find("Window");
        if (window == null)
        {
            GameObject go = new GameObject("Window", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "던전 UI 배치 정리: 창");
            go.transform.SetParent(panel, false);
            window = go.transform;
            go.GetComponent<Image>().color = new Color(0.17f, 0.12f, 0.08f, 1f); // 테마 굽기가 창틀로 바꿈
        }
        RectTransform w = (RectTransform)window;
        w.anchorMin = w.anchorMax = w.pivot = new Vector2(0.5f, 0.5f);
        w.sizeDelta = size;
        w.anchoredPosition = Vector2.zero;
        window.SetAsFirstSibling();

        // 원래 자식들을 창 안으로
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Transform child = panel.GetChild(i);
            if (child == window) continue;
            Undo.SetTransformParent(child, window, "던전 UI 배치 정리");
        }

        foreach (Transform child in window)
        {
            RectTransform c = (RectTransform)child;
            Undo.RecordObject(c, "던전 UI 배치 정리");
            Button button = child.GetComponent<Button>();
            if (button != null)
            {
                // 버튼: 창 아래쪽 가운데
                c.anchorMin = c.anchorMax = new Vector2(0.5f, 0f);
                c.pivot = new Vector2(0.5f, 0.5f);
                c.sizeDelta = new Vector2(280f, 60f);
                c.anchoredPosition = new Vector2(0f, 70f);
                TextMeshProUGUI bl = child.GetComponentInChildren<TextMeshProUGUI>(true);
                if (bl != null) { Undo.RecordObject(bl, "던전 UI 배치 정리"); if (font != null) bl.font = font; bl.fontSize = 28f; }
                continue;
            }

            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text == null) continue;
            Undo.RecordObject(text, "던전 UI 배치 정리");
            if (font != null) text.font = font;
            if (bodyRectTop > 0f)
            {
                // 본문(도주 결과): 버튼 위 영역을 채움
                c.anchorMin = Vector2.zero; c.anchorMax = Vector2.one;
                c.pivot = new Vector2(0.5f, 0.5f);
                c.offsetMin = new Vector2(48f, 120f);
                c.offsetMax = new Vector2(-48f, -40f);
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = true;
                text.fontSizeMax = 30f;
                text.fontSizeMin = 18f;
            }
            else
            {
                // 제목(승리!/패배!): 버튼 위 가운데
                c.anchorMin = c.anchorMax = new Vector2(0.5f, 1f);
                c.pivot = new Vector2(0.5f, 0.5f);
                c.sizeDelta = new Vector2(size.x - 80f, 100f);
                c.anchoredPosition = new Vector2(0f, -110f);
                text.alignment = TextAlignmentOptions.Center;
                if (titleSize > 0f) text.fontSize = titleSize;
            }
        }
    }
}
