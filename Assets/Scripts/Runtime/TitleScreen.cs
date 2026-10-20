using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 연출. 화면 구성은 'Tools > 타이틀 화면 세팅'이 만들고 이 컴포넌트에 연결합니다.
///  - 인트로: 검은 화면이 걷히며 레이어가 아래에서 올라오고, 문장·로고가 떨어지며 화면이 흔들리고, 메뉴가 차례로 들어옴 (클릭·키로 건너뛰기)
///  - 대기: 마우스 시차, 로고 흔들림·반짝임, 별·구름·반딧불이·굴뚝 연기·등불·깃발, 길을 걷는 모험가 파티, 가끔 하늘을 나는 드래곤과 별똥별
///  - 나가기: 메뉴를 누르면 화면이 어두워진 뒤 다음 씬으로 (TitleManager가 PlayExit 호출)
/// 모든 시간은 Time.unscaledDeltaTime 기준 (게임오버 뒤 timeScale 0으로 돌아와도 움직이도록)
/// </summary>
public class TitleScreen : MonoBehaviour
{
    public static TitleScreen Instance { get; private set; }

    const float ArtScale = 4f;   // 그림 1픽셀 = UI 4

    [Serializable]
    public class Layer
    {
        public RectTransform rect;
        [Tooltip("마우스 시차로 움직이는 최대 거리 (UI 단위). 멀수록 작게")]
        public float parallax;
        [Tooltip("인트로에서 이만큼 아래에서 올라옴 (UI 단위)")]
        public float introRise;
        [NonSerialized] public Vector2 basePos;
    }

    [Header("레이어 (뒤 → 앞)")]
    public List<Layer> layers = new List<Layer>();
    public RectTransform sceneRoot;          // 화면 흔들림 대상

    [Header("로고")]
    public RectTransform logoGroup;
    public RectTransform emblem;
    public RectTransform logo;
    public RectTransform logoShine;
    public CanvasGroup subtitle;
    public float logoFloatAmplitude = 6f;
    public float shineInterval = 5f;

    [Header("메뉴")]
    public List<RectTransform> menuButtons = new List<RectTransform>();
    public RectTransform cursor;
    public CanvasGroup versionText;

    [Header("화면 전환")]
    public Image fade;
    public float fadeInTime = 1.4f;
    public float fadeOutTime = 0.6f;

    [Header("인트로 타이밍 (초)")]
    public float layerRiseTime = 2.4f;
    public float emblemAt = 0.9f;
    public float logoAt = 1.25f;
    public float subtitleAt = 1.75f;
    public float menuAt = 2.0f;
    public float menuStagger = 0.12f;
    public float shakeStrength = 14f;

    [Header("시차")]
    public float parallaxSmooth = 4f;

    [Header("애니메이션 그림")]
    public Sprite[] flagFrames;
    public Sprite[] flameFrames;
    public Sprite[] cloudSprites;
    public Sprite starSprite, fireflySprite, smokeSprite, meteorSprite;

    [Header("장면 속 위치 (세팅 도구가 연결)")]
    public Image flag;
    public List<Image> flames = new List<Image>();
    public List<Image> glows = new List<Image>();      // 창·등불 빛 (깜빡임)
    public RectTransform smokeOrigin;
    public RectTransform skyFx;                         // 구름·별·별똥별이 붙는 곳 (하늘 레이어)
    public RectTransform dragonLane;                    // 드래곤이 나는 곳 (먼 산 앞)
    public RectTransform partyLane;                     // 모험가가 걷는 곳 (길 레이어, y = 발 위치)
    public RectTransform fireflyArea;

    [Header("구름·별·반딧불이")]
    public int cloudCount = 5;
    public float cloudSpeed = 6f;      // UI 단위/초
    public int twinkleStarCount = 16;
    public int fireflyCount = 18;
    public Vector2 meteorInterval = new Vector2(6f, 14f);

    [Header("모험가 파티")]
    [Tooltip("모험가 그림 1픽셀의 UI 크기")]
    public float partyPixelScale = 3f;
    public float partySpeed = 70f;
    public float partySpacing = 92f;
    public Vector2 partyRestTime = new Vector2(4f, 9f);

    [Header("드래곤")]
    public MonsterSpriteSet dragonSet;
    public float dragonPixelScale = 1.3f;
    public Color dragonTint = new Color(0.22f, 0.16f, 0.3f, 1f);
    public float dragonSpeed = 150f;
    public float dragonFirstDelay = 9f;
    public Vector2 dragonInterval = new Vector2(30f, 45f);

    // 상태
    bool introDone;
    bool introSkipped;
    bool exiting;
    float idleTime;
    Vector2 parallax;          // 부드럽게 따라가는 마우스 위치 (-1~1)
    Vector2 logoBase, emblemBase, subtitleBase;
    readonly List<Vector2> menuBase = new List<Vector2>();
    readonly List<CanvasGroup> menuGroups = new List<CanvasGroup>();
    Vector2 shake;
    RectTransform cursorTarget;
    float cursorBob;

    void Awake()
    {
        Instance = this;
        foreach (Layer l in layers) if (l.rect != null) l.basePos = l.rect.anchoredPosition;
        if (logoGroup != null) logoBase = logoGroup.anchoredPosition;
        if (emblem != null) emblemBase = emblem.anchoredPosition;
        if (subtitle != null) subtitleBase = ((RectTransform)subtitle.transform).anchoredPosition;
        foreach (RectTransform b in menuButtons)
        {
            menuBase.Add(b.anchoredPosition);
            CanvasGroup g = b.GetComponent<CanvasGroup>();
            if (g == null) g = b.gameObject.AddComponent<CanvasGroup>();
            menuGroups.Add(g);
        }
        if (cursor != null) cursor.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        Time.timeScale = 1f; // 게임오버(파산)로 돌아온 경우 대비
        SpawnClouds();
        SpawnStars();
        SpawnFireflies();
        StartCoroutine(Intro());
        StartCoroutine(SmokeLoop());
        StartCoroutine(MeteorLoop());
        StartCoroutine(PartyLoop());
        StartCoroutine(DragonLoop());
    }

    // ─── 인트로 ──────────────────────────────────────────

    IEnumerator Intro()
    {
        SetMenuInteractable(false);
        float end = Mathf.Max(menuAt + menuStagger * menuButtons.Count + 0.5f, layerRiseTime, fadeInTime);
        bool shook = false;
        float t = 0f;
        while (t < end)
        {
            if (!introSkipped && t > 0.15f && AnyPress()) { introSkipped = true; t = end; }
            ApplyIntro(t);
            if (!shook && t >= logoAt + 0.4f && !introSkipped) { shook = true; StartCoroutine(Shake(0.3f, shakeStrength)); }
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        ApplyIntro(end + 1f);
        introDone = true;
        SetMenuInteractable(true);
        StartCoroutine(ShineLoop());
    }

    void ApplyIntro(float t)
    {
        if (fade != null)
        {
            float a = 1f - Ease.OutQuad(Mathf.Clamp01(t / fadeInTime));
            fade.color = new Color(0f, 0f, 0f, a);
            fade.raycastTarget = false;
        }

        float rise = Ease.OutCubic(Mathf.Clamp01(t / layerRiseTime));
        foreach (Layer l in layers)
            if (l.rect != null) l.rect.anchoredPosition = l.basePos + new Vector2(0f, -l.introRise * (1f - rise));

        if (emblem != null)
        {
            float k = Mathf.Clamp01((t - emblemAt) / 0.6f);
            emblem.localScale = Vector3.one * Ease.OutBack(k);
            emblem.localRotation = Quaternion.Euler(0f, 0f, -25f * (1f - Ease.OutCubic(k)));
            emblem.gameObject.SetActive(k > 0f);
        }
        if (logo != null)
        {
            float k = Mathf.Clamp01((t - logoAt) / 0.4f);
            float s = Mathf.Lerp(2.2f, 1f, Ease.InQuad(k));
            logo.localScale = new Vector3(s, s, 1f);
            SetAlpha(logo, Ease.OutQuad(k));
            logo.gameObject.SetActive(k > 0f);
        }
        if (subtitle != null)
        {
            float k = Mathf.Clamp01((t - subtitleAt) / 0.5f);
            subtitle.alpha = k;
            ((RectTransform)subtitle.transform).anchoredPosition = subtitleBase + new Vector2(0f, -16f * (1f - Ease.OutCubic(k)));
        }
        for (int i = 0; i < menuButtons.Count; i++)
        {
            float k = Mathf.Clamp01((t - menuAt - menuStagger * i) / 0.45f);
            menuGroups[i].alpha = k;
            menuButtons[i].anchoredPosition = menuBase[i] + new Vector2(-80f * (1f - Ease.OutBack(k)), 0f);
        }
        if (versionText != null) versionText.alpha = Mathf.Clamp01((t - menuAt - 0.4f) / 0.6f);
    }

    void SetMenuInteractable(bool on)
    {
        foreach (CanvasGroup g in menuGroups) { g.interactable = on; g.blocksRaycasts = on; }
    }

    static void SetAlpha(RectTransform rt, float a)
    {
        Graphic g = rt.GetComponent<Graphic>();
        if (g != null) { Color c = g.color; c.a = a; g.color = c; }
    }

    static bool AnyPress()
    {
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }

    IEnumerator Shake(float time, float strength)
    {
        float t = 0f;
        while (t < time)
        {
            float k = 1f - t / time;
            shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * strength * k * k;
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        shake = Vector2.zero;
    }

    // ─── 매 프레임 (대기 연출) ──────────────────────────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        idleTime += dt;

        // 마우스 시차 (인트로가 끝난 뒤에만 레이어에 반영)
        Vector2 target = Vector2.zero;
        if (Pointer.current != null && Screen.width > 0)
        {
            Vector2 p = Pointer.current.position.ReadValue();
            target = new Vector2(p.x / Screen.width * 2f - 1f, p.y / Screen.height * 2f - 1f);
            target = Vector2.ClampMagnitude(target, 1f);
        }
        parallax = Vector2.Lerp(parallax, target, 1f - Mathf.Exp(-parallaxSmooth * dt));
        if (introDone)
        {
            foreach (Layer l in layers)
                if (l.rect != null) l.rect.anchoredPosition = Snap(l.basePos - parallax * l.parallax);
        }
        if (sceneRoot != null) sceneRoot.anchoredPosition = Snap(shake);

        // 로고 둥실둥실
        if (logoGroup != null)
            logoGroup.anchoredPosition = Snap(logoBase + new Vector2(0f, Mathf.Sin(idleTime * 1.6f) * logoFloatAmplitude) - parallax * 8f + shake * 0.5f);
        if (emblem != null && introDone)
            emblem.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(idleTime * 1.1f) * 2f);

        AnimateFrames();
        AnimateGlows();
        UpdateCursor(dt);
    }

    static Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));

    void AnimateFrames()
    {
        if (flag != null && flagFrames != null && flagFrames.Length > 0)
            flag.sprite = flagFrames[(int)(idleTime * 7f) % flagFrames.Length];
        if (flameFrames != null && flameFrames.Length > 0)
            for (int i = 0; i < flames.Count; i++)
                if (flames[i] != null) flames[i].sprite = flameFrames[(int)(idleTime * 9f + i * 1.7f) % flameFrames.Length];
    }

    void AnimateGlows()
    {
        for (int i = 0; i < glows.Count; i++)
        {
            if (glows[i] == null) continue;
            float n = Mathf.PerlinNoise(idleTime * 2.3f, i * 7.1f);
            Color c = glows[i].color;
            c.a = Mathf.Lerp(0.7f, 1f, n);
            glows[i].color = c;
        }
    }

    // ─── 메뉴 커서 (TitleMenuButton이 호출) ─────────────────

    public void SetCursorTarget(RectTransform button)
    {
        cursorTarget = button;
        if (cursor == null) return;
        cursor.gameObject.SetActive(button != null);
        if (button != null) cursorBob = 0f;
    }

    public void ClearCursorTarget(RectTransform button)
    {
        if (cursorTarget == button) SetCursorTarget(null);
    }

    void UpdateCursor(float dt)
    {
        if (cursor == null || cursorTarget == null) return;
        cursorBob += dt;
        Vector2 left = cursorTarget.anchoredPosition + new Vector2(-cursorTarget.rect.width * 0.5f * cursorTarget.localScale.x - 34f, 0f);
        left.x += Mathf.Round(Mathf.Sin(cursorBob * 8f) * 2f) * 3f;
        cursor.anchoredPosition = left;
    }

    // ─── 반짝임 ──────────────────────────────────────────

    IEnumerator ShineLoop()
    {
        if (logoShine == null || logo == null) yield break;
        while (true)
        {
            float w = logo.rect.width;
            float t = 0f;
            const float time = 0.7f;
            logoShine.gameObject.SetActive(true);
            while (t < time)
            {
                float k = Ease.InOutQuad(t / time);
                logoShine.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.6f, w * 0.6f, k), 0f);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            logoShine.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(shineInterval);
        }
    }

    // ─── 구름·별·반딧불이 ────────────────────────────────

    class Drifter { public RectTransform rt; public float speed; }
    readonly List<Drifter> clouds = new List<Drifter>();

    Image NewImage(string name, RectTransform parent, Sprite sprite, float scale, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // 부모 왼쪽 위 기준 (그림 좌표처럼 아래로 -)
        rt.pivot = pivot;
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (sprite != null) rt.sizeDelta = sprite.rect.size * scale;
        return img;
    }

    void SpawnClouds()
    {
        if (skyFx == null || cloudSprites == null || cloudSprites.Length == 0) return;
        float w = skyFx.rect.width;
        for (int i = 0; i < cloudCount; i++)
        {
            Image img = NewImage("Cloud", skyFx, cloudSprites[i % cloudSprites.Length], ArtScale, new Vector2(0.5f, 0.5f));
            img.color = new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.75f, 1f));
            RectTransform rt = img.rectTransform;
            rt.anchoredPosition = new Vector2(w * (i + UnityEngine.Random.value * 0.6f) / cloudCount, -UnityEngine.Random.Range(150f, 520f));
            clouds.Add(new Drifter { rt = rt, speed = cloudSpeed * UnityEngine.Random.Range(0.6f, 1.4f) });
        }
        StartCoroutine(CloudLoop());
    }

    IEnumerator CloudLoop()
    {
        while (true)
        {
            float w = skyFx.rect.width;
            foreach (Drifter c in clouds)
            {
                Vector2 p = c.rt.anchoredPosition;
                p.x -= c.speed * Time.unscaledDeltaTime;
                if (p.x < -c.rt.rect.width) p.x = w + c.rt.rect.width;
                c.rt.anchoredPosition = p;
            }
            yield return null;
        }
    }

    void SpawnStars()
    {
        if (skyFx == null || starSprite == null) return;
        float w = skyFx.rect.width;
        for (int i = 0; i < twinkleStarCount; i++)
        {
            Image img = NewImage("Star", skyFx, starSprite, ArtScale, new Vector2(0.5f, 0.5f));
            Vector2 pos = new Vector2(UnityEngine.Random.Range(40f, w - 40f), -UnityEngine.Random.Range(20f, 400f));
            img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(pos.x / ArtScale) * ArtScale, Mathf.Round(pos.y / ArtScale) * ArtScale);
            StartCoroutine(Twinkle(img, UnityEngine.Random.Range(0f, 6f), UnityEngine.Random.Range(1.5f, 3.5f)));
        }
    }

    IEnumerator Twinkle(Image img, float phase, float period)
    {
        while (true)
        {
            float k = Mathf.Sin((idleTime + phase) * Mathf.PI * 2f / period);
            k = Mathf.Max(0f, k);
            img.color = new Color(1f, 1f, 1f, k * k);
            img.rectTransform.localScale = Vector3.one * (k > 0.85f ? 1f : 0.75f);
            yield return null;
        }
    }

    void SpawnFireflies()
    {
        if (fireflyArea == null || fireflySprite == null) return;
        for (int i = 0; i < fireflyCount; i++)
        {
            Image img = NewImage("Firefly", fireflyArea, fireflySprite, ArtScale * 0.75f, new Vector2(0.5f, 0.5f));
            StartCoroutine(Firefly(img));
        }
    }

    IEnumerator Firefly(Image img)
    {
        RectTransform area = fireflyArea;
        Vector2 home = new Vector2(UnityEngine.Random.Range(0f, area.rect.width), -UnityEngine.Random.Range(0f, area.rect.height));
        float seed = UnityEngine.Random.value * 100f;
        float blinkPeriod = UnityEngine.Random.Range(2f, 4.5f);
        while (true)
        {
            float t = idleTime + seed;
            Vector2 p = home + new Vector2(Mathf.Sin(t * 0.37f) * 60f + Mathf.Sin(t * 1.3f) * 14f, Mathf.Cos(t * 0.29f) * 34f + Mathf.Sin(t * 1.7f) * 10f);
            img.rectTransform.anchoredPosition = Snap(p);
            float b = Mathf.Sin(t * Mathf.PI * 2f / blinkPeriod) * 0.5f + 0.5f;
            img.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0.05f, 1f, b));
            yield return null;
        }
    }

    // ─── 굴뚝 연기 ───────────────────────────────────────

    IEnumerator SmokeLoop()
    {
        if (smokeOrigin == null || smokeSprite == null) yield break;
        while (true)
        {
            StartCoroutine(SmokePuff());
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.45f, 0.8f));
        }
    }

    IEnumerator SmokePuff()
    {
        Image img = NewImage("Smoke", smokeOrigin, smokeSprite, ArtScale, new Vector2(0.5f, 0.5f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        float life = UnityEngine.Random.Range(3.5f, 5f);
        float drift = UnityEngine.Random.Range(30f, 70f);
        float t = 0f;
        while (t < life)
        {
            float k = t / life;
            img.rectTransform.anchoredPosition = Snap(new Vector2(drift * k * k + Mathf.Sin(t * 2f) * 6f, 150f * k));
            img.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.2f, k);
            img.color = new Color(1f, 1f, 1f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.4f)) * 0.8f * (1f - k));
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        Destroy(img.gameObject);
    }

    // ─── 별똥별 ─────────────────────────────────────────

    IEnumerator MeteorLoop()
    {
        if (skyFx == null || meteorSprite == null) yield break;
        yield return new WaitForSecondsRealtime(4f);
        while (true)
        {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(meteorInterval.x, meteorInterval.y));
            Image img = NewImage("Meteor", skyFx, meteorSprite, ArtScale, new Vector2(1f, 0f));
            Vector2 start = new Vector2(UnityEngine.Random.Range(200f, skyFx.rect.width - 300f), -UnityEngine.Random.Range(30f, 160f));
            Vector2 dir = new Vector2(1f, -0.3f).normalized;
            float t = 0f, life = 0.7f;
            while (t < life)
            {
                float k = t / life;
                img.rectTransform.anchoredPosition = Snap(start + dir * 520f * k);
                img.color = new Color(1f, 1f, 1f, Mathf.Sin(Mathf.PI * k));
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            Destroy(img.gameObject);
        }
    }

    // ─── 길을 걷는 모험가 파티 ───────────────────────────

    static readonly JobType[] PartyJobs = { JobType.Warrior, JobType.Rogue, JobType.Archer, JobType.Mage, JobType.Healer };

    IEnumerator PartyLoop()
    {
        if (partyLane == null) yield break;
        yield return new WaitForSecondsRealtime(2.6f);
        while (true)
        {
            // 직업 4개를 섞어 고름 (맨 앞은 전사나 도적)
            List<JobType> jobs = new List<JobType>(PartyJobs);
            for (int i = jobs.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (jobs[i], jobs[j]) = (jobs[j], jobs[i]); }
            jobs.RemoveAt(4);
            jobs.Sort((a, b) => Rank(a).CompareTo(Rank(b)));

            List<RectTransform> members = new List<RectTransform>();
            for (int i = 0; i < jobs.Count; i++)
            {
                RectTransform m = SpawnWalker(jobs[i]);
                if (m == null) continue;
                m.anchoredPosition = new Vector2(-120f - i * partySpacing, 0f);
                members.Add(m);
            }
            if (members.Count == 0) yield break; // 모험가 그림이 없으면 그만

            float width = partyLane.rect.width;
            bool anyOnScreen = true;
            while (anyOnScreen)
            {
                anyOnScreen = false;
                for (int i = 0; i < members.Count; i++)
                {
                    RectTransform m = members[i];
                    Vector2 p = m.anchoredPosition;
                    p.x += partySpeed * Time.unscaledDeltaTime; // 걷는 모습은 UISpriteAnimator가 이동을 보고 정함
                    m.anchoredPosition = p;
                    if (p.x < width + 80f) anyOnScreen = true;
                }
                yield return null;
            }
            foreach (RectTransform m in members) Destroy(m.gameObject);
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(partyRestTime.x, partyRestTime.y));
        }
    }

    static int Rank(JobType job) => job == JobType.Warrior ? 0 : job == JobType.Rogue ? 1 : job == JobType.Archer ? 2 : job == JobType.Mage ? 3 : 4;

    RectTransform SpawnWalker(JobType job)
    {
        MonsterSpriteSet set;
        try { set = AdventurerSprites.ForUI(new Adventurer { job = job }); }
        catch (Exception e) { Debug.LogWarning($"[TitleScreen] 모험가 그림을 만들 수 없습니다: {e.Message}"); return null; }
        if (set == null) return null;

        Image img = NewImage($"Walker_{job}", partyLane, set.FirstSprite(MonsterSpriteSet.Anim.Walk), partyPixelScale, new Vector2(0.5f, 0f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        UISpriteAnimator anim = img.gameObject.AddComponent<UISpriteAnimator>();
        anim.spriteSet = set;
        anim.uiPerPixel = partyPixelScale;
        return img.rectTransform;
    }

    // ─── 하늘을 나는 드래곤 ──────────────────────────────

    IEnumerator DragonLoop()
    {
        if (dragonLane == null || dragonSet == null) yield break;
        MonsterSpriteSet.Clip clip = dragonSet.Get(MonsterSpriteSet.Anim.Walk, MonsterSpriteSet.Dir.Side);
        if (clip == null || clip.frames == null || clip.frames.Length == 0) yield break;

        yield return new WaitForSecondsRealtime(dragonFirstDelay);
        while (true)
        {
            Image img = NewImage("Dragon", dragonLane, clip.frames[0], dragonPixelScale, new Vector2(0.5f, 0.5f));
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            img.color = dragonTint;
            bool leftToRight = UnityEngine.Random.value < 0.5f;
            float width = dragonLane.rect.width;
            float y0 = UnityEngine.Random.Range(-40f, 60f);
            float x = leftToRight ? -200f : width + 200f;
            img.rectTransform.localScale = new Vector3(leftToRight ? 1f : -1f, 1f, 1f); // 옆모습은 오른쪽을 봄
            float t = 0f;
            while (leftToRight ? x < width + 200f : x > -200f)
            {
                t += Time.unscaledDeltaTime;
                x += (leftToRight ? 1f : -1f) * dragonSpeed * Time.unscaledDeltaTime;
                img.rectTransform.anchoredPosition = Snap(new Vector2(x, y0 + Mathf.Sin(t * 1.8f) * 22f));
                Sprite s = clip.frames[(int)(t * clip.fps) % clip.frames.Length];
                if (img.sprite != s) { img.sprite = s; img.rectTransform.sizeDelta = s.rect.size * dragonPixelScale; }
                yield return null;
            }
            Destroy(img.gameObject);
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(dragonInterval.x, dragonInterval.y));
        }
    }

    // ─── 나가기 ─────────────────────────────────────────

    /// <summary>화면을 어둡게 한 뒤 onDone 호출. 이미 나가는 중이면 무시</summary>
    public bool PlayExit(Action onDone)
    {
        if (exiting) return false;
        exiting = true;
        StartCoroutine(Exit(onDone));
        return true;
    }

    IEnumerator Exit(Action onDone)
    {
        SetMenuInteractable(false);
        if (cursor != null) cursor.gameObject.SetActive(false);
        if (fade != null) fade.raycastTarget = true;
        float t = 0f;
        while (t < fadeOutTime)
        {
            float k = Ease.InQuad(t / fadeOutTime);
            if (fade != null) fade.color = new Color(0f, 0f, 0f, k);
            for (int i = 0; i < menuGroups.Count; i++) menuGroups[i].alpha = 1f - k;
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        if (fade != null) fade.color = Color.black;
        onDone?.Invoke();
    }

    // ─── 이징 ───────────────────────────────────────────

    static class Ease
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InQuad(float t) => t * t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return t <= 0f ? 0f : 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
