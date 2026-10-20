using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [메뉴] Tools > 타이틀 화면 세팅
/// TitleScene에 황혼의 길드 마을 타이틀(Tools/title_art_gen.py 그림)을 만들고 TitleScreen 연출을 연결합니다.
/// 기존 버튼(NewGame·LoadGame·Quit)은 onClick 연결을 그대로 둔 채 새 메뉴로 옮기고, 예전 Canvas는 끕니다.
/// 다시 실행하면 타이틀 화면을 지우고 새로 만듭니다.
/// </summary>
public static class TitleScreenSetup
{
    const string ScenePath = "Assets/Scenes/TitleScene.unity";
    const string ArtDir = "Assets/Art/Title/";
    const string RootName = "TitleScreenCanvas";
    const float K = 4f;                 // 그림 1픽셀 = UI 4
    const float LW = 512f, LH = 288f;   // 레이어 크기 (그림 픽셀)

    // title_art_gen.py와 같은 값 (그림 좌표, 레이어 왼쪽 위 기준)
    const float FlagX = 77f, FlagY = 71f;
    const float SmokeX = 165f, SmokeY = 84f;
    static readonly Vector2[] GuildLanterns = { new Vector2(105f, 191f), new Vector2(138f, 191f) };
    static readonly Vector2 StreetLamp = new Vector2(409.5f, 193f);
    const float RoadFeetY = 252f;
    const float DragonY = 112f;
    const float LogoTopY = 69f, EmblemCenterY = 47f;
    static readonly float[] ButtonCenterY = { 160f, 182f, 204f };

    static readonly (string name, string label)[] Buttons = { ("NewGame", "새 게임"), ("LoadGame", "불러오기"), ("Quit", "종료") };

    [MenuItem("Tools/타이틀 화면 세팅")]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogError("[TitleScreenSetup] 플레이를 끄고 실행하세요."); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DungGeunMo SDF.asset");

        // 1. 기존 버튼 찾기 (새로 만들 때 지워지지 않게 잠시 루트로)
        var buttons = new List<Button>();
        foreach (var (name, _) in Buttons)
        {
            Button b = FindButton(name);
            if (b == null) { Debug.LogError($"[TitleScreenSetup] '{name}' 버튼이 없습니다."); return; }
            buttons.Add(b);
        }
        GameObject oldCanvas = FindRootCanvasOf("TitleLogo");
        foreach (Button b in buttons) b.transform.SetParent(null, false);

        GameObject existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);
        if (oldCanvas != null) oldCanvas.SetActive(false);

        // 2. 캔버스
        GameObject rootGO = new GameObject(RootName, typeof(RectTransform));
        rootGO.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = rootGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        CanvasScaler scaler = rootGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // 화면비가 달라도 그림이 잘리지 않게 (레이어에 여유분이 있음)
        rootGO.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)rootGO.transform;
        TitleScreen ts = rootGO.AddComponent<TitleScreen>();

        Stretch(NewImage("Backdrop", root, null, Color.black).rectTransform);

        // 3. 레이어
        RectTransform scene = NewRect("Scene", root);
        Stretch(scene);
        ts.sceneRoot = scene;
        ts.layers = new List<TitleScreen.Layer>();
        RectTransform sky = Layer(ts, scene, "title_sky", 0f, 0f);
        RectTransform mountains = Layer(ts, scene, "title_mountains", 6f, 60f);
        Layer(ts, scene, "title_castle", 12f, 120f);
        RectTransform guild = Layer(ts, scene, "title_guild", 22f, 200f);
        RectTransform road = Layer(ts, scene, "title_road", 32f, 280f);
        RectTransform front = Layer(ts, scene, "title_front", 44f, 360f);

        // 하늘: 구름·별·별똥별
        RectTransform skyFx = NewRect("SkyFx", sky);
        Stretch(skyFx);
        ts.skyFx = skyFx;
        // 먼 산 앞: 드래곤
        ts.dragonLane = Lane("DragonLane", mountains, DragonY);
        // 회관: 창 불빛, 깃발, 등불, 굴뚝
        ts.glows = new List<Image>();
        ts.flames = new List<Image>();
        ts.glows.Add(Glow(guild, "title_guild_glow"));
        ts.flag = InLayer("Flag", guild, Art("title_flag_0"), FlagX, FlagY, new Vector2(0f, 1f), K);
        foreach (Vector2 p in GuildLanterns)
            ts.flames.Add(InLayer("Flame", guild, Art("title_flame_0"), p.x, p.y, new Vector2(0.5f, 0f), K * 0.5f));
        RectTransform smoke = NewRect("SmokeOrigin", guild);
        PlaceInLayer(smoke, SmokeX, SmokeY, new Vector2(0.5f, 0.5f), Vector2.zero);
        smoke.SetAsFirstSibling(); // 연기는 창 불빛·깃발·등불보다 뒤에 그림
        ts.smokeOrigin = smoke;
        // 길: 반딧불이, 모험가
        RectTransform fireflies = NewRect("FireflyArea", road);
        PlaceInLayer(fireflies, 140f, 178f, new Vector2(0f, 1f), new Vector2(360f, 72f) * K);
        ts.fireflyArea = fireflies;
        ts.partyLane = Lane("PartyLane", road, RoadFeetY);
        // 맨 앞: 가로등
        ts.glows.Add(Glow(front, "title_front_glow"));
        ts.flames.Add(InLayer("LampFlame", front, Art("title_flame_0"), StreetLamp.x, StreetLamp.y, new Vector2(0.5f, 0f), K * 0.8f));

        // 4. 로고
        RectTransform logoGroup = NewRect("LogoGroup", root);
        Center(logoGroup, Vector2.zero, Vector2.zero);
        ts.logoGroup = logoGroup;

        Sprite logoSprite = Art("title_logo");
        Sprite subSprite = Art("title_subtitle");
        Image emblem = NewImage("Emblem", logoGroup, Art("title_emblem"), Color.white);
        Center(emblem.rectTransform, ToCanvas(LW / 2f, EmblemCenterY), emblem.sprite.rect.size * K);
        ts.emblem = emblem.rectTransform;

        float logoH = logoSprite.rect.height;
        Image logo = NewImage("Logo", logoGroup, logoSprite, Color.white);
        Center(logo.rectTransform, ToCanvas(LW / 2f, LogoTopY + logoH / 2f), logoSprite.rect.size * K);
        logo.gameObject.AddComponent<Mask>().showMaskGraphic = true; // 반짝임이 글자 모양 안에서만 보이게
        ts.logo = logo.rectTransform;
        Image shine = NewImage("Shine", logo.rectTransform, Art("title_shine"), Color.white);
        Center(shine.rectTransform, Vector2.zero, shine.sprite.rect.size * K);
        shine.gameObject.SetActive(false);
        ts.logoShine = shine.rectTransform;

        Image sub = NewImage("Subtitle", logoGroup, subSprite, Color.white);
        Center(sub.rectTransform, ToCanvas(LW / 2f, LogoTopY + logoH + 2f + subSprite.rect.height / 2f), subSprite.rect.size * K);
        ts.subtitle = sub.gameObject.AddComponent<CanvasGroup>();

        // 5. 메뉴 (기존 버튼을 옮겨 꾸밈)
        RectTransform menu = NewRect("Menu", root);
        Center(menu, Vector2.zero, Vector2.zero);
        ts.menuButtons = new List<RectTransform>();
        Sprite normal = Art("title_button"), hover = Art("title_button_hover"), pressed = Art("title_button_pressed"), disabled = Art("title_button_disabled");
        for (int i = 0; i < buttons.Count; i++)
        {
            Button b = buttons[i];
            RectTransform rt = (RectTransform)b.transform;
            rt.SetParent(menu, false);
            Center(rt, ToCanvas(LW / 2f, ButtonCenterY[i]), normal.rect.size * K);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            Image img = b.GetComponent<Image>();
            img.sprite = normal;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.preserveAspect = false;
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = normal, pressedSprite = pressed, disabledSprite = disabled };
            Navigation nav = b.navigation; nav.mode = Navigation.Mode.Vertical; b.navigation = nav;

            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = Buttons[i].label;
                if (font != null) label.font = font;
                label.fontSize = 34;
                label.fontStyle = FontStyles.Normal;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                RectTransform lr = label.rectTransform;
                Stretch(lr);
                lr.anchoredPosition = new Vector2(0f, 2f);
            }
            TitleMenuButton tmb = b.GetComponent<TitleMenuButton>();
            if (tmb == null) tmb = b.gameObject.AddComponent<TitleMenuButton>();
            tmb.label = label;
            ts.menuButtons.Add(rt);
        }
        Image cursor = NewImage("Cursor", menu, Art("title_cursor"), Color.white);
        Center(cursor.rectTransform, Vector2.zero, cursor.sprite.rect.size * K);
        ts.cursor = cursor.rectTransform;

        // 6. 버전 표시
        GameObject verGO = NewRect("Version", root).gameObject;
        RectTransform vr = (RectTransform)verGO.transform;
        vr.anchorMin = vr.anchorMax = vr.pivot = new Vector2(1f, 0f);
        vr.anchoredPosition = new Vector2(-28f, 20f);
        vr.sizeDelta = new Vector2(500f, 40f);
        TextMeshProUGUI ver = verGO.AddComponent<TextMeshProUGUI>();
        if (font != null) ver.font = font;
        ver.text = $"v{Application.version}";
        ver.fontSize = 24;
        ver.alignment = TextAlignmentOptions.BottomRight;
        ver.color = new Color(1f, 0.9f, 0.75f, 0.55f);
        ver.raycastTarget = false;
        ts.versionText = verGO.AddComponent<CanvasGroup>();

        // 7. 화면 전환용 검은 막
        Image fade = NewImage("Fade", root, null, Color.black);
        Stretch(fade.rectTransform);
        fade.raycastTarget = false;
        ts.fade = fade;

        // 8. 애니메이션 그림
        ts.flagFrames = Frames("title_flag_", 4);
        ts.flameFrames = Frames("title_flame_", 4);
        ts.cloudSprites = Frames("title_cloud_", 3);
        ts.starSprite = Art("title_star");
        ts.fireflySprite = Art("title_firefly");
        ts.smokeSprite = Art("title_smoke");
        ts.meteorSprite = Art("title_meteor");
        ts.dragonSet = AssetDatabase.LoadAssetAtPath<MonsterSpriteSet>("Assets/Art/Monsters/Sets/dragon.asset");

        // 9. 카메라 배경
        Camera cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; EditorUtility.SetDirty(cam); }

        EditorUtility.SetDirty(ts);
        EditorSceneManager.MarkSceneDirty(rootGO.scene);
        EditorSceneManager.SaveScene(rootGO.scene);
        Debug.Log("=== 타이틀 화면 세팅 완료 (TitleScene 저장됨) ===");
    }

    // ─── 도우미 ──────────────────────────────────────────

    static Sprite Art(string name)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
        if (s == null) Debug.LogError($"[TitleScreenSetup] {ArtDir}{name}.png 스프라이트가 없습니다. python Tools/title_art_gen.py 실행 후 'Tools > 도트 그림 다시 가져오기'를 하세요.");
        return s;
    }

    static Sprite[] Frames(string prefix, int count)
    {
        var list = new Sprite[count];
        for (int i = 0; i < count; i++) list[i] = Art(prefix + i);
        return list;
    }

    // 레이어 좌표(그림 픽셀, 왼쪽 위 원점) → 캔버스 가운데 기준 UI 좌표
    static Vector2 ToCanvas(float lx, float ly) => new Vector2((lx - LW / 2f) * K, (LH / 2f - ly) * K);

    static RectTransform Layer(TitleScreen ts, RectTransform parent, string sprite, float parallax, float rise)
    {
        Image img = NewImage(sprite.Replace("title_", "L_"), parent, Art(sprite), Color.white);
        Center(img.rectTransform, Vector2.zero, new Vector2(LW, LH) * K);
        ts.layers.Add(new TitleScreen.Layer { rect = img.rectTransform, parallax = parallax, introRise = rise });
        return img.rectTransform;
    }

    static Image Glow(RectTransform layer, string sprite)
    {
        Image img = NewImage("Glow", layer, Art(sprite), Color.white);
        Stretch(img.rectTransform);
        return img;
    }

    static RectTransform Lane(string name, RectTransform layer, float y)
    {
        RectTransform rt = NewRect(name, layer);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -y * K);
        rt.sizeDelta = new Vector2(0f, 0f);
        return rt;
    }

    static Image InLayer(string name, RectTransform layer, Sprite sprite, float lx, float ly, Vector2 pivot, float scale)
    {
        Image img = NewImage(name, layer, sprite, Color.white);
        PlaceInLayer(img.rectTransform, lx, ly, pivot, sprite != null ? sprite.rect.size * scale : Vector2.zero);
        return img;
    }

    static void PlaceInLayer(RectTransform rt, float lx, float ly, Vector2 pivot, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivot;
        rt.anchoredPosition = new Vector2(lx * K, -ly * K);
        rt.sizeDelta = size;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static Button FindButton(string name)
    {
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.name == name) return b;
        return null;
    }

    static GameObject FindRootCanvasOf(string childName)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != childName) continue;
            Canvas c = t.GetComponentInParent<Canvas>(true);
            if (c != null) return c.rootCanvas.gameObject;
        }
        return null;
    }
}
