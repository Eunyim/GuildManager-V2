using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI 테마를 씬·프리팹에 직접 구움 → 에디터에서 보이는 모습 = 실행 화면. 구운 뒤에는 에디터에서 손으로 고치면 됨
//  - 이미 테마 스프라이트가 들어간 것은 건너뛰므로 여러 번 실행해도 손으로 고친 것을 덮어쓰지 않음 (스프라이트를 비우면 다시 입힘)
//  - 구운 창에는 UIThemeBaked를 붙여 실행 중 테마(UITheme)가 다시 건드리지 않게 함
//  - 로비 상단 정보줄 아이콘(HudStatIcon)도 오브젝트로 만들어 둠
public static class UIThemeBaker
{
    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/TitleScene.unity",
        "Assets/Scenes/GuildCreationScene.unity",
        "Assets/Scenes/LobbyScene.unity",
        "Assets/Scenes/DungeonScene.unity",
    };
    const string WidgetFolder = "Assets/Prefabs/UI";
    static readonly HashSet<string> SkipPrefabs = new HashSet<string> { "UnitHud", "HP_Canvas ", "HP_Canvas", "MP_Canvas", "Prefab_LobbyChibi" };

    [MenuItem("Tools/UI 테마 굽기 (씬·프리팹)")]
    static void Bake()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UIThemeBaker] 플레이 중에는 구울 수 없습니다."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string originalScene = SceneManager.GetActiveScene().path;
        UIThemeSettings settings = FindSettings();

        BakeWidgets(settings);
        foreach (string path in ScenePaths) BakeScene(path, settings);

        if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene);
        Debug.Log("[UIThemeBaker] 굽기 완료. 이제 에디터에서 보이는 모습이 실행 화면과 같습니다.");
    }

    // GameManager 인스펙터의 테마 수치 (없으면 기본값)
    static UIThemeSettings FindSettings()
    {
        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameManager gm = root.GetComponentInChildren<GameManager>(true);
                if (gm != null && gm.uiTheme != null) return gm.uiTheme;
            }
        }
        return new UIThemeSettings();
    }

    // ─── 목록 칸 프리팹 (Slot_* 등) ───────────────────────────────
    static void BakeWidgets(UIThemeSettings settings)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { WidgetFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (SkipPrefabs.Contains(System.IO.Path.GetFileNameWithoutExtension(path))) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentInChildren<Canvas>(true) is Canvas c && c.renderMode == RenderMode.WorldSpace) continue;
                UITheme.BeginBake(settings, null);
                UITheme.ThemeWidget(root);
                UITheme.EndBake();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[UIThemeBaker] 프리팹: {path}");
            }
            finally
            {
                UITheme.EndBake();
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    // ─── 씬 ─────────────────────────────────────────────────────
    static void BakeScene(string path, UIThemeSettings settings)
    {
        Scene scene = EditorSceneManager.OpenScene(path);
        UITheme.BeginBake(settings, o => Undo.RecordObject(o, "UI 테마 굽기"));
        try
        {
            int popups = 0, buttons = 0;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in go.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;

                    // 창: Canvas 바로 아래 Popup_* / Panel_*
                    foreach (Transform child in canvas.transform)
                    {
                        if (!child.name.StartsWith("Popup_") && !child.name.StartsWith("Panel_")) continue;
                        UITheme.ThemePopup(child);
                        if (child.GetComponent<UIThemeBaked>() == null) Undo.AddComponent<UIThemeBaked>(child.gameObject);
                        popups++;
                    }

                    foreach (Button b in canvas.GetComponentsInChildren<Button>(true)) { UITheme.ThemeButton(b); buttons++; }
                    foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                    {
                        if (img.name.Contains("Tooltip")) UITheme.ThemeTooltip(img);
                        else UITheme.ThemePaper(img);
                    }
                }
            }

            // 던전: 유닛에 마우스를 올렸을 때의 정보 창
            foreach (GameObject go in scene.GetRootGameObjects())
                foreach (UnitHoverTooltip tip in go.GetComponentsInChildren<UnitHoverTooltip>(true))
                    if (tip.panel != null) UITheme.ThemeTooltip(tip.panel.GetComponent<Image>());

            BakeLobbyHud(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UIThemeBaker] 씬: {path} (창 {popups}개, 버튼 {buttons}개)");
        }
        finally { UITheme.EndBake(); }
    }

    // 로비 상단 정보줄: 아이콘 오브젝트를 만들고, 에디터에서 보기 좋게 예시 숫자를 넣어 둠 (실행하면 LobbyManager가 실제 값으로 바꿈)
    static void BakeLobbyHud(Scene scene)
    {
        LobbyManager lm = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            lm = go.GetComponentInChildren<LobbyManager>(true);
            if (lm != null) break;
        }
        if (lm == null) return;

        AddHudIcon(lm.goldText, "gold", "1,000");
        AddHudIcon(lm.spiritStoneText, "spirit", "0");
        AddHudIcon(lm.reputationText, "reputation", "0 (Lv.1)");
        AddHudIcon(lm.dayText, "day", "1월 1주차");
        AddHudIcon(lm.receptionLevelText, "reception", "Lv.1");
        AddHudIcon(lm.questBoardLevelText, "questboard", "Lv.1");
        AddHudIcon(lm.innLevelText, "inn", "Lv.1");
        AddHudIcon(lm.pubLevelText, "pub", "Lv.1");
        AddHudIcon(lm.templeLevelText, "temple", "Lv.1");
    }

    static void AddHudIcon(TextMeshProUGUI text, string iconName, string sample)
    {
        if (text == null) return;
        HudStatIcon hud = text.GetComponent<HudStatIcon>();
        if (hud == null) hud = Undo.AddComponent<HudStatIcon>(text.gameObject);
        Undo.RecordObject(hud, "UI 테마 굽기");
        hud.iconName = iconName;

        bool hadIcon = text.transform.Find("Icon") != null;
        RectTransform icon = hud.EnsureIcon();
        if (!hadIcon && icon != null) Undo.RegisterCreatedObjectUndo(icon.gameObject, "UI 테마 굽기: 아이콘");

        Undo.RecordObject(text, "UI 테마 굽기");
        text.text = sample;
    }
}
