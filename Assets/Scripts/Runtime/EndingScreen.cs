using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 데모 엔딩 화면 (드래곤 처치). 로비에 돌아왔을 때 GameManager.pendingEnding이면 스스로 만들어 띄움
/// 순서: 어둠 → 쓰러지는 드래곤 → "드래곤 토벌" 제목 → 파티 입장 → 이번 판 기록 → 감사 인사·[계속 운영]/[타이틀로]
/// 화면 아무 곳이나 누르면 연출을 건너뜀. 뒤의 로비를 가리기만 하고 시간은 멈추지 않음(치비 걷기 애니메이션이 게임 시간을 씀)
/// </summary>
public class EndingScreen : MonoBehaviour
{
    const float RefW = 1920f, RefH = 1080f;

    static readonly Color Gold      = new Color(1f, 0.82f, 0.36f);
    static readonly Color Parchment = new Color(0.96f, 0.9f, 0.76f);
    static readonly Color Dim       = new Color(0.78f, 0.7f, 0.58f);
    static readonly Color WindowCol = new Color(0.12f, 0.08f, 0.07f, 0.92f);
    static readonly Color BorderCol = new Color(0.75f, 0.55f, 0.25f, 1f);
    static readonly Color Ember     = new Color(1f, 0.55f, 0.2f, 1f);

    static EndingScreen current;

    TMP_FontAsset font;
    RectTransform root;
    CanvasGroup group;
    RectTransform partyLane, dragonRect;
    TextMeshProUGUI title, subtitle, thanks;
    CanvasGroup recordGroup, thanksGroup, buttonGroup;
    readonly List<RectTransform> embers = new List<RectTransform>();
    readonly List<float> emberSpeed = new List<float>();
    bool skip;
    bool prevSuppressed;

    // ─── 자동 생성 ──────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryShow();
        TryShow();
    }

    static void TryShow()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.pendingEnding || current != null) return;
        if (SceneManager.GetActiveScene().name != "LobbyScene") return;
        gm.pendingEnding = false;
        gm.SaveGame(); // 엔딩을 본 기록을 바로 저장
        GameObject go = new GameObject("EndingScreen", typeof(RectTransform));
        current = go.AddComponent<EndingScreen>();
        current.Build(gm);
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }

    // ─── 만들기 ──────────────────────────────────────────

    void Build(GameManager gm)
    {
        font = FindFont();
        prevSuppressed = TutorialManager.Suppressed;
        TutorialManager.Suppressed = true; // 엔딩을 보는 동안 튜토리얼 도움말이 끼어들지 않게

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000; // 튜토리얼(5000)보다 위
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // 배경: 밤하늘 → 불그스름한 지평선 그라데이션 (뒤의 로비를 누르지 못하게 막음)
        Image bg = NewImage("Bg", root, GradientSprite(new Color(0.04f, 0.035f, 0.1f), new Color(0.3f, 0.12f, 0.07f)));
        Stretch(bg.rectTransform);
        Button skipArea = bg.gameObject.AddComponent<Button>();
        skipArea.transition = Selectable.Transition.None;
        skipArea.onClick.AddListener(() => skip = true);

        // 불티
        for (int i = 0; i < 46; i++)
        {
            Image e = NewImage($"Ember{i}", root, null);
            e.raycastTarget = false;
            float s = Random.Range(4f, 9f);
            e.rectTransform.sizeDelta = new Vector2(s, s);
            e.rectTransform.anchorMin = e.rectTransform.anchorMax = new Vector2(0f, 0f);
            e.rectTransform.anchoredPosition = new Vector2(Random.Range(0f, RefW), Random.Range(-40f, RefH));
            e.color = new Color(Ember.r, Ember.g * Random.Range(0.8f, 1.3f), Ember.b, Random.Range(0.25f, 0.8f));
            embers.Add(e.rectTransform);
            emberSpeed.Add(Random.Range(25f, 70f));
        }

        // 쓰러지는 드래곤 (그림이 연결돼 있으면)
        if (gm.endingDragonSet == null) Debug.LogWarning("[EndingScreen] 드래곤 그림이 연결되지 않았습니다 (Tools > 데모 엔딩 연결)");
        if (gm.endingDragonSet != null)
        {
            Image d = NewImage("Dragon", root, gm.endingDragonSet.FirstSprite(MonsterSpriteSet.Anim.Idle, MonsterSpriteSet.Dir.Side));
            d.raycastTarget = false;
            dragonRect = d.rectTransform;
            dragonRect.anchorMin = dragonRect.anchorMax = new Vector2(0.5f, 0.5f);
            dragonRect.anchoredPosition = new Vector2(0f, 110f);
            d.color = new Color(1f, 1f, 1f, 0f);
            UISpriteAnimator anim = d.gameObject.AddComponent<UISpriteAnimator>();
            anim.spriteSet = gm.endingDragonSet;
            anim.uiPerPixel = 8f;
            anim.enabled = false; // 직접 프레임을 고름
        }

        title = MakeText("Txt_Title", root, "드래곤 토벌", 118, Gold, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1400f, 150f));
        title.fontStyle = FontStyles.Bold;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color32(60, 25, 8, 255);
        title.alpha = 0f;

        string guild = string.IsNullOrEmpty(gm.guildName) ? "우리" : gm.guildName;
        subtitle = MakeText("Txt_Sub", root, $"<b>{guild}</b> 길드가 드래곤 둥지의 주인을 쓰러뜨렸습니다.\n그 이름이 모든 모험가 협회에 울려 퍼집니다.", 34, Parchment, TextAlignmentOptions.Center);
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -285f), new Vector2(1500f, 110f));
        subtitle.lineSpacing = 18f;
        subtitle.alpha = 0f;

        // 파티 (드래곤을 쓰러뜨리고 살아 돌아온 사람)
        GameObject laneGo = new GameObject("PartyLane", typeof(RectTransform));
        partyLane = (RectTransform)laneGo.transform;
        partyLane.SetParent(root, false);
        Place(partyLane, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(RefW, 10f));

        BuildRecord(gm);
        BuildThanks();
        StartCoroutine(Play(gm));
    }

    void BuildRecord(GameManager gm)
    {
        GuildRecord r = gm.record ?? new GuildRecord();
        RectTransform win = MakeWindow("Record", root, new Vector2(1160f, 262f));
        win.anchorMin = win.anchorMax = new Vector2(0.5f, 0f);
        win.anchoredPosition = new Vector2(0f, 352f);
        recordGroup = win.gameObject.AddComponent<CanvasGroup>();
        recordGroup.alpha = 0f;

        TextMeshProUGUI head = MakeText("Head", win, "이번 길드의 기록", 34, Gold, TextAlignmentOptions.Center);
        Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(1000f, 50f));

        int weeks = GuildRecordRules.TotalWeeks(r.endingMonth > 0 ? r.endingMonth : gm.month, r.endingMonth > 0 ? r.endingWeek : gm.week);
        string fallen = r.fallen != null && r.fallen.Count > 0 ? string.Join(", ", r.fallen) : "없음 — 모두 함께 돌아왔습니다";
        string left =
            $"<color=#{Hex(Dim)}>걸린 기간</color>   {(r.endingMonth > 0 ? r.endingMonth : gm.month)}월 {(r.endingMonth > 0 ? r.endingWeek : gm.week)}주차  (총 {weeks}주)\n" +
            $"<color=#{Hex(Dim)}>고용한 모험가</color>   {r.hired}명 (+ 창립 멤버 {(gm.starterJobs != null ? gm.starterJobs.Length : 0)}명)\n" +
            $"<color=#{Hex(Dim)}>의뢰</color>   성공 {r.questsSucceeded} / 실패 {r.questsFailed}";
        string right =
            $"<color=#{Hex(Dim)}>최종 골드</color>   {gm.gold:N0} G\n" +
            $"<color=#{Hex(Dim)}>최종 명성</color>   {gm.reputation:N0}\n" +
            $"<color=#{Hex(Dim)}>소속 모험가</color>   {gm.adventurers.Count}명";
        TextMeshProUGUI l = MakeText("Left", win, left, 27, Parchment, TextAlignmentOptions.TopLeft);
        Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(360f, -132f), new Vector2(600f, 120f));
        l.lineSpacing = 14f;
        TextMeshProUGUI rt = MakeText("Right", win, right, 27, Parchment, TextAlignmentOptions.TopLeft);
        Place(rt.rectTransform, new Vector2(0f, 1f), new Vector2(920f, -132f), new Vector2(400f, 120f));
        rt.lineSpacing = 14f;

        TextMeshProUGUI f = MakeText("Fallen", win, $"<color=#{Hex(Dim)}>잠든 동료</color>   {fallen}", 25, Parchment, TextAlignmentOptions.TopLeft);
        Place(f.rectTransform, new Vector2(0f, 0f), new Vector2(580f, 38f), new Vector2(1060f, 44f));
        f.textWrappingMode = TextWrappingModes.Normal;
    }

    void BuildThanks()
    {
        thanks = MakeText("Thanks", root,
            "<size=46><b>플레이해 주셔서 감사합니다</b></size>\n<size=26>길드 매니저 데모 · 이야기는 정식 버전에서 경쟁 길드와 함께 이어집니다</size>",
            46, Gold, TextAlignmentOptions.Center);
        Place(thanks.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(1600f, 110f));
        thanks.lineSpacing = 10f;
        thanksGroup = thanks.gameObject.AddComponent<CanvasGroup>();
        thanksGroup.alpha = 0f;

        GameObject btns = new GameObject("Buttons", typeof(RectTransform));
        RectTransform br = (RectTransform)btns.transform;
        br.SetParent(root, false);
        Place(br, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(460f, 70f));
        buttonGroup = btns.AddComponent<CanvasGroup>();
        buttonGroup.alpha = 0f;
        buttonGroup.interactable = buttonGroup.blocksRaycasts = false;

        Button cont = MakeButton("Btn_Continue", br, "계속 운영", new Color(0.45f, 0.28f, 0.12f), Close);
        Place((RectTransform)cont.transform, new Vector2(0f, 0.5f), new Vector2(105f, 0f), new Vector2(210f, 64f));
        Button toTitle = MakeButton("Btn_Title", br, "타이틀로", new Color(0.25f, 0.2f, 0.18f), ToTitle);
        Place((RectTransform)toTitle.transform, new Vector2(0f, 0.5f), new Vector2(350f, 0f), new Vector2(210f, 64f));
    }

    // ─── 연출 ────────────────────────────────────────────

    IEnumerator Play(GameManager gm)
    {
        yield return Fade(f => group.alpha = f, 0.8f);

        // 드래곤이 쓰러짐
        if (dragonRect != null && gm.endingDragonSet != null)
        {
            Image d = dragonRect.GetComponent<Image>();
            UISpriteAnimator anim = dragonRect.GetComponent<UISpriteAnimator>();
            float t = 0f;
            while (t < 2.4f && !skip)
            {
                t += Time.unscaledDeltaTime;
                d.color = new Color(0.85f, 0.62f, 0.55f, Mathf.Clamp01(t * 2.5f) * Mathf.Clamp01((2.4f - t) * 2f));
                anim.Show(t < 0.6f ? MonsterSpriteSet.Anim.Idle : MonsterSpriteSet.Anim.Death, MonsterSpriteSet.Dir.Side, Mathf.Max(0f, t - 0.6f));
                float shake = t > 0.6f && t < 1.2f ? Mathf.Sin(t * 70f) * 10f * (1.2f - t) : 0f;
                dragonRect.anchoredPosition = new Vector2(shake, 110f - Mathf.Clamp01(t - 0.6f) * 40f);
                yield return null;
            }
            d.enabled = false;
        }

        // 제목: 커졌다가 자리 잡음
        float k = 0f;
        while (k < 0.7f && !skip)
        {
            k += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(k / 0.7f);
            title.alpha = p;
            float s = 1f + (1f - EaseOutBack(p)) * 0.6f;
            title.rectTransform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        title.alpha = 1f;
        title.rectTransform.localScale = Vector3.one;
        yield return Wait(0.3f);
        yield return Fade(f => subtitle.alpha = f, 0.8f);

        // 파티 입장: 왼쪽에서 걸어 들어와 가운데에 나란히 섬
        List<RectTransform> walkers = SpawnParty(gm);
        float spacing = 150f;
        float startX = -RefW * 0.5f - 120f;
        float total = (walkers.Count - 1) * spacing;
        float w = 0f;
        bool arrived = walkers.Count == 0;
        while (!arrived && !skip)
        {
            w += Time.unscaledDeltaTime;
            arrived = true;
            for (int i = 0; i < walkers.Count; i++)
            {
                float target = total * 0.5f - i * spacing; // 앞장선 사람이 가장 오른쪽
                float x = Mathf.Min(target, startX + (w - i * 0.35f) * 420f);
                if (x < target) arrived = false;
                walkers[i].anchoredPosition = new Vector2(Mathf.Max(startX, x), 0f);
            }
            yield return null;
        }
        for (int i = 0; i < walkers.Count; i++) walkers[i].anchoredPosition = new Vector2(total * 0.5f - i * spacing, 0f);

        yield return Wait(0.4f);
        yield return Fade(f => recordGroup.alpha = f, 0.7f);
        yield return Wait(0.6f);
        yield return Fade(f => thanksGroup.alpha = f, 0.9f);
        ShowFinal();
    }

    void ShowFinal()
    {
        group.alpha = 1f;
        if (dragonRect != null) dragonRect.GetComponent<Image>().enabled = false;
        title.alpha = 1f;
        title.rectTransform.localScale = Vector3.one;
        subtitle.alpha = 1f;
        recordGroup.alpha = 1f;
        thanksGroup.alpha = 1f;
        buttonGroup.alpha = 1f;
        buttonGroup.interactable = buttonGroup.blocksRaycasts = true;
    }

    List<RectTransform> SpawnParty(GameManager gm)
    {
        var list = new List<RectTransform>();
        GuildRecord r = gm.record;
        if (r == null || r.dragonSlayers == null) return list;
        foreach (string name in r.dragonSlayers)
        {
            Adventurer adv = gm.adventurers.Find(a => a.name == name);
            if (adv == null) continue;
            MonsterSpriteSet set;
            try { set = AdventurerSprites.ForUI(adv); }
            catch (System.Exception e) { Debug.LogWarning($"[EndingScreen] 모험가 그림을 만들 수 없습니다: {e.Message}"); continue; }
            if (set == null) continue;
            Image img = NewImage($"Hero_{name}", partyLane, set.FirstSprite(MonsterSpriteSet.Anim.Walk));
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.anchoredPosition = new Vector2(-RefW, 0f);
            UISpriteAnimator anim = img.gameObject.AddComponent<UISpriteAnimator>();
            anim.spriteSet = set;
            anim.uiPerPixel = 4f;
            anim.moveThreshold = 2f;

            TextMeshProUGUI label = MakeText("Name", img.transform, adv.name, 24, Parchment, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            label.rectTransform.sizeDelta = new Vector2(220f, 34f);
            list.Add(img.rectTransform);
        }
        return list;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < embers.Count; i++)
        {
            RectTransform e = embers[i];
            Vector2 p = e.anchoredPosition;
            p.y += emberSpeed[i] * dt;
            p.x += Mathf.Sin(Time.unscaledTime * 0.8f + i) * 12f * dt;
            if (p.y > RefH + 20f) { p.y = -20f; p.x = Random.Range(0f, RefW); }
            e.anchoredPosition = p;
        }
        if (skip && buttonGroup != null && !buttonGroup.interactable)
        {
            StopAllCoroutines();
            ShowFinalWithParty();
        }
    }

    void ShowFinalWithParty()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && partyLane.childCount == 0)
        {
            List<RectTransform> walkers = SpawnParty(gm);
            float total = (walkers.Count - 1) * 150f;
            for (int i = 0; i < walkers.Count; i++) walkers[i].anchoredPosition = new Vector2(total * 0.5f - i * 150f, 0f);
        }
        else
        {
            // 걷던 중이면 제자리로
            int n = partyLane.childCount;
            float total = (n - 1) * 150f;
            for (int i = 0; i < n; i++) ((RectTransform)partyLane.GetChild(i)).anchoredPosition = new Vector2(total * 0.5f - i * 150f, 0f);
        }
        ShowFinal();
    }

    void Close()
    {
        TutorialManager.Suppressed = prevSuppressed;
        Destroy(gameObject);
    }

    void ToTitle()
    {
        TutorialManager.Suppressed = prevSuppressed;
        GameManager gm = GameManager.Instance;
        if (gm != null) gm.ReturnToTitleAfterEnding();
        else { Time.timeScale = 1f; SceneManager.LoadScene("TitleScene"); }
    }

    // ─── 도우미 ──────────────────────────────────────────

    IEnumerator Fade(System.Action<float> set, float duration)
    {
        float t = 0f;
        while (t < duration && !skip)
        {
            t += Time.unscaledDeltaTime;
            set(Mathf.Clamp01(t / duration));
            yield return null;
        }
        set(1f);
    }

    IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds && !skip) { t += Time.unscaledDeltaTime; yield return null; }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    static Sprite GradientSprite(Color top, Color bottom)
    {
        Texture2D tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
    }

    static TMP_FontAsset FindFont()
    {
        if (LobbyManager.Instance != null && LobbyManager.Instance.notificationText != null)
            return LobbyManager.Instance.notificationText.font;
        TextMeshProUGUI any = Object.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        if (any != null && any.font != null) return any.font;
        return TMP_Settings.defaultFontAsset;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        if (sprite != null) img.SetNativeSize();
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    RectTransform MakeWindow(string name, Transform parent, Vector2 size)
    {
        Image img = NewImage(name, parent, null);
        img.color = WindowCol;
        Outline o = img.gameObject.AddComponent<Outline>();
        o.effectColor = BorderCol;
        o.effectDistance = new Vector2(4f, -4f);
        img.rectTransform.sizeDelta = size;
        return img.rectTransform;
    }

    TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    Button MakeButton(string name, Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        Image img = NewImage(name, parent, null);
        img.color = color;
        Outline o = img.gameObject.AddComponent<Outline>();
        o.effectColor = BorderCol;
        o.effectDistance = new Vector2(3f, -3f);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
        TextMeshProUGUI t = MakeText("Label", img.transform, label, 28, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        return b;
    }
}
