using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 길드 신청서 화면 연출. 화면 구성은 'Tools > 길드 신청서 화면 세팅'이 만들고 이 컴포넌트에 연결합니다.
///  - 들어올 때: 검은 화면이 걷히며 신청서가 책상 위로 미끄러져 놓이고, 버튼이 차례로 들어옴
///  - 대기: 촛불·빛 깜빡임, 빛 속을 떠다니는 먼지, 글자를 칠 때마다 이름이 살짝 튐
///  - 이름 없이 서명: 신청서가 흔들리고 이름 줄이 빨갛게 번쩍이며 안내
///  - 서명: 깃펜이 잉크병에서 날아와 서명 → 돌아감 → '승인' 도장이 쾅 → 안내 → 어두워진 뒤 onDone
/// 길드 이름 판정·데이터 저장·씬 이동은 GuildCreationManager가 합니다.
/// </summary>
public class GuildCreationScreen : MonoBehaviour
{
    public static GuildCreationScreen Instance { get; private set; }

    [Header("화면 구성 (세팅 도구가 연결)")]
    public RectTransform shakeRoot;
    public RectTransform paper;
    public TMP_InputField nameInput;
    public Image nameLineFlash;          // 이름 줄 위의 빨간 막대 (잘못 냈을 때 번쩍)
    public TMP_Text errorText;
    public TMP_Text dateText;
    public TMP_Text supportText;
    public Image signature;              // 가로로 채워지며 그려지는 서명
    public Image stamp;                  // '승인' 도장
    public RectTransform quill;          // 깃펜 (기준점 = 펜촉)
    public List<RectTransform> buttons = new List<RectTransform>();
    public CanvasGroup toast;            // '승인' 안내 문구 (글자 + 그림자)
    public Image fade;

    [Header("촛불")]
    public Image candleFlame;
    public Sprite[] flameFrames;
    public Image candleGlow;
    public RectTransform dustArea;
    public Sprite dustSprite;
    public int dustCount = 14;

    [Header("도장 튀는 잉크")]
    public Sprite inkSprite;

    [Header("시간 (초)")]
    public float fadeInTime = 0.8f;
    public float paperSlideTime = 0.9f;
    public float signTime = 1.1f;
    public float exitFadeTime = 0.7f;

    Vector2 paperBase, quillRest;
    readonly List<Vector2> buttonBase = new List<Vector2>();
    readonly List<CanvasGroup> buttonGroups = new List<CanvasGroup>();
    Vector2 shake;
    float time;
    bool busy;
    Coroutine errorRoutine;
    float namePop;

    void Awake()
    {
        Instance = this;
        if (paper != null) paperBase = paper.anchoredPosition;
        if (quill != null) quillRest = quill.anchoredPosition;
        foreach (RectTransform b in buttons)
        {
            buttonBase.Add(b.anchoredPosition);
            CanvasGroup g = b.GetComponent<CanvasGroup>();
            if (g == null) g = b.gameObject.AddComponent<CanvasGroup>();
            buttonGroups.Add(g);
        }
        if (signature != null) signature.fillAmount = 0f;
        if (stamp != null) stamp.gameObject.SetActive(false);
        if (errorText != null) errorText.alpha = 0f;
        if (nameLineFlash != null) nameLineFlash.color = new Color(1f, 0.2f, 0.15f, 0f);
        if (toast != null) toast.alpha = 0f;
        if (nameInput != null) nameInput.onValueChanged.AddListener(_ => OnNameTyped());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        Time.timeScale = 1f;
        SpawnDust();
        StartCoroutine(Intro());
    }

    /// <summary>창립일·초기 지원 칸의 글 (GuildCreationManager가 넣음)</summary>
    public void SetInfo(string date, string support)
    {
        if (dateText != null) dateText.text = date;
        if (supportText != null) supportText.text = support;
    }

    // ─── 들어올 때 ──────────────────────────────────────

    IEnumerator Intro()
    {
        busy = true;
        SetButtons(false);
        float end = Mathf.Max(fadeInTime, 0.15f + paperSlideTime, 0.8f + 0.12f * buttons.Count + 0.4f);
        for (float t = 0f; t < end; t += Time.unscaledDeltaTime)
        {
            ApplyIntro(t);
            yield return null;
        }
        ApplyIntro(end + 1f);
        busy = false;
        SetButtons(true);
        if (nameInput != null) nameInput.ActivateInputField();
    }

    void ApplyIntro(float t)
    {
        if (fade != null) fade.color = new Color(0f, 0f, 0f, 1f - Ease.OutQuad(Mathf.Clamp01(t / fadeInTime)));
        if (paper != null)
        {
            float k = Ease.OutBack(Mathf.Clamp01((t - 0.15f) / paperSlideTime));
            paper.anchoredPosition = paperBase + new Vector2(0f, -1250f * (1f - k));
            paper.localRotation = Quaternion.Euler(0f, 0f, -9f * (1f - Mathf.Clamp01(k)));
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            float k = Mathf.Clamp01((t - 0.8f - 0.12f * i) / 0.4f);
            buttonGroups[i].alpha = k;
            buttons[i].anchoredPosition = buttonBase[i] + new Vector2(120f * (1f - Ease.OutBack(k)), 0f);
        }
    }

    void SetButtons(bool on)
    {
        foreach (CanvasGroup g in buttonGroups) { g.interactable = on; g.blocksRaycasts = on; }
        if (nameInput != null) nameInput.interactable = on;
    }

    // ─── 매 프레임 ──────────────────────────────────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;
        if (candleFlame != null && flameFrames != null && flameFrames.Length > 0)
            candleFlame.sprite = flameFrames[(int)(time * 9f) % flameFrames.Length];
        if (candleGlow != null)
        {
            Color c = candleGlow.color;
            c.a = Mathf.Lerp(0.72f, 1f, Mathf.PerlinNoise(time * 2.6f, 0.3f));
            candleGlow.color = c;
        }
        if (shakeRoot != null) shakeRoot.anchoredPosition = new Vector2(Mathf.Round(shake.x), Mathf.Round(shake.y));

        // 글자를 칠 때 이름이 살짝 커졌다 돌아옴
        if (nameInput != null && nameInput.textComponent != null)
        {
            namePop = Mathf.MoveTowards(namePop, 0f, dt * 6f);
            nameInput.textComponent.rectTransform.localScale = Vector3.one * (1f + 0.08f * namePop);
        }
    }

    void OnNameTyped()
    {
        namePop = 1f;
        if (errorText != null && errorText.alpha > 0f && errorRoutine == null) errorText.alpha = 0f;
    }

    // ─── 이름이 비었을 때 ────────────────────────────────

    public void ShowError(string message)
    {
        if (busy) return;
        if (errorRoutine != null) StopCoroutine(errorRoutine);
        errorRoutine = StartCoroutine(Error(message));
    }

    IEnumerator Error(string message)
    {
        if (errorText != null) { errorText.text = message; errorText.alpha = 1f; }
        const float time = 0.45f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / time;
            if (paper != null) paper.anchoredPosition = paperBase + new Vector2(Mathf.Sin(t * 60f) * 14f * k, 0f);
            if (nameLineFlash != null) nameLineFlash.color = new Color(1f, 0.2f, 0.15f, (Mathf.Sin(t * 30f) * 0.5f + 0.5f) * k);
            yield return null;
        }
        if (paper != null) paper.anchoredPosition = paperBase;
        if (nameLineFlash != null) nameLineFlash.color = new Color(1f, 0.2f, 0.15f, 0f);
        if (nameInput != null) nameInput.ActivateInputField();
        yield return new WaitForSecondsRealtime(2f);
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            if (errorText != null) errorText.alpha = 1f - t / 0.5f;
            yield return null;
        }
        if (errorText != null) errorText.alpha = 0f;
        errorRoutine = null;
    }

    // ─── 서명 → 도장 → 나가기 ────────────────────────────

    /// <summary>서명 연출을 하고 끝나면 onDone. 이미 진행 중이면 false</summary>
    public bool PlaySign(Action onDone)
    {
        if (busy) return false;
        busy = true;
        StartCoroutine(Sign(onDone));
        return true;
    }

    IEnumerator Sign(Action onDone)
    {
        SetButtons(false);
        if (errorRoutine != null) { StopCoroutine(errorRoutine); errorRoutine = null; }
        if (errorText != null) errorText.alpha = 0f;
        StartCoroutine(FadeButtons());

        // 1. 깃펜이 서명 줄 왼쪽으로 날아감
        Vector2 sigLeft = PaperPoint(signature, 0.03f, 0.35f);
        Vector2 sigRight = PaperPoint(signature, 0.95f, 0.35f);
        if (quill != null)
        {
            Vector2 lift = quillRest + new Vector2(-40f, 120f);
            yield return MoveQuill(quillRest, lift, sigLeft, 0.55f);
        }

        // 2. 서명 (깃펜이 물결치며 오른쪽으로, 서명 그림이 따라 채워짐)
        Vector2 sigHeight = PaperPoint(signature, 0f, 1f) - PaperPoint(signature, 0f, 0f);
        for (float t = 0f; t < signTime; t += Time.unscaledDeltaTime)
        {
            float k = t / signTime;
            if (signature != null) signature.fillAmount = k;
            if (quill != null)
            {
                float wave = Mathf.Sin(k * 19f) * 0.22f * (1f - k * 0.5f) + Mathf.Sin(k * 7f + 1f) * 0.12f;
                quill.anchoredPosition = Vector2.Lerp(sigLeft, sigRight, k) + sigHeight * wave;
                quill.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 40f) * 4f);
            }
            yield return null;
        }
        if (signature != null) signature.fillAmount = 1f;

        // 3. 깃펜이 잉크병으로 돌아감
        if (quill != null)
        {
            quill.localRotation = Quaternion.identity;
            yield return MoveQuill(quill.anchoredPosition, quill.anchoredPosition + new Vector2(60f, 140f), quillRest, 0.45f);
        }
        yield return new WaitForSecondsRealtime(0.15f);

        // 4. 승인 도장 쾅
        if (stamp != null)
        {
            stamp.gameObject.SetActive(true);
            RectTransform st = stamp.rectTransform;
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                float k = Ease.InQuad(t / 0.22f);
                st.localScale = Vector3.one * Mathf.Lerp(2.8f, 1f, k);
                st.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-20f, -8f, k));
                stamp.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.95f, k));
                yield return null;
            }
            st.localScale = Vector3.one;
            st.localRotation = Quaternion.Euler(0f, 0f, -8f);
            stamp.color = new Color(1f, 1f, 1f, 0.95f);
            StartCoroutine(Shake(0.3f, 18f));
            StartCoroutine(PaperPunch());
            SpawnInk(st);
        }
        yield return new WaitForSecondsRealtime(0.5f);

        // 5. 안내 문구
        if (toast != null)
        {
            RectTransform tr = (RectTransform)toast.transform;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.35f;
                toast.alpha = k;
                tr.localScale = Vector3.one * Ease.OutBack(k);
                yield return null;
            }
            toast.alpha = 1f;
            tr.localScale = Vector3.one;
        }
        yield return new WaitForSecondsRealtime(1.1f);

        yield return FadeOut();
        onDone?.Invoke();
    }

    IEnumerator MoveQuill(Vector2 from, Vector2 control, Vector2 to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = Ease.InOutQuad(t / duration);
            Vector2 a = Vector2.Lerp(from, control, k), b = Vector2.Lerp(control, to, k);
            quill.anchoredPosition = Vector2.Lerp(a, b, k);
            yield return null;
        }
        quill.anchoredPosition = to;
    }

    // 신청서 위 그림(서명 등)의 한 점을 깃펜 좌표(같은 부모 기준)로
    Vector2 PaperPoint(Graphic g, float u, float v)
    {
        if (g == null || quill == null) return quillRest;
        RectTransform rt = g.rectTransform;
        Rect r = rt.rect;
        Vector3 world = rt.TransformPoint(new Vector3(r.xMin + r.width * u, r.yMin + r.height * v, 0f));
        RectTransform parent = (RectTransform)quill.parent;
        Vector2 local = parent.InverseTransformPoint(world);
        // anchoredPosition은 앵커 기준이므로 앵커 위치만큼 보정
        Vector2 anchorRef = Vector2.Scale(parent.rect.size, quill.anchorMin) + parent.rect.min;
        return local - anchorRef;
    }

    IEnumerator FadeButtons()
    {
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            foreach (CanvasGroup g in buttonGroups) g.alpha = Mathf.Min(g.alpha, 1f - t / 0.3f);
            yield return null;
        }
        foreach (CanvasGroup g in buttonGroups) g.alpha = 0f;
    }

    IEnumerator Shake(float duration, float strength)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / duration;
            shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * strength * k * k;
            yield return null;
        }
        shake = Vector2.zero;
    }

    IEnumerator PaperPunch()
    {
        if (paper == null) yield break;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.25f;
            paper.localScale = Vector3.one * (1f - 0.025f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        paper.localScale = Vector3.one;
    }

    void SpawnInk(RectTransform at)
    {
        if (inkSprite == null) return;
        for (int i = 0; i < 12; i++) StartCoroutine(InkDrop(at, i / 12f * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f)));
    }

    IEnumerator InkDrop(RectTransform at, float angle)
    {
        Image img = NewImage("Ink", (RectTransform)at.parent, inkSprite, 4f);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = at.anchorMin;
        Vector2 start = at.anchoredPosition;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float dist = UnityEngine.Random.Range(70f, 130f);
        const float life = 0.35f;
        for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
        {
            float k = Ease.OutQuad(t / life);
            img.rectTransform.anchoredPosition = start + dir * dist * k;
            img.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.6f, k);
            img.color = new Color(1f, 1f, 1f, 1f - k * 0.4f);
            yield return null;
        }
        // 종이에 점으로 남음
        img.color = new Color(1f, 1f, 1f, 0.75f);
    }

    // ─── 돌아가기 / 화면 어둡게 ───────────────────────────

    /// <summary>화면을 어둡게 한 뒤 onDone (타이틀로 돌아갈 때)</summary>
    public bool PlayExit(Action onDone)
    {
        if (busy) return false;
        busy = true;
        SetButtons(false);
        StartCoroutine(ExitRoutine(onDone));
        return true;
    }

    IEnumerator ExitRoutine(Action onDone)
    {
        // 신청서가 아래로 치워짐
        Vector2 from = paper != null ? paper.anchoredPosition : Vector2.zero;
        for (float t = 0f; t < exitFadeTime; t += Time.unscaledDeltaTime)
        {
            float k = Ease.InQuad(t / exitFadeTime);
            if (paper != null) { paper.anchoredPosition = from + new Vector2(0f, -900f * k); paper.localRotation = Quaternion.Euler(0f, 0f, 6f * k); }
            if (fade != null) fade.color = new Color(0f, 0f, 0f, k);
            foreach (CanvasGroup g in buttonGroups) g.alpha = 1f - k;
            yield return null;
        }
        if (fade != null) fade.color = Color.black;
        onDone?.Invoke();
    }

    IEnumerator FadeOut()
    {
        if (fade == null) yield break;
        fade.raycastTarget = true;
        for (float t = 0f; t < exitFadeTime; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, Ease.InQuad(t / exitFadeTime));
            yield return null;
        }
        fade.color = Color.black;
    }

    // ─── 먼지 ───────────────────────────────────────────

    void SpawnDust()
    {
        if (dustArea == null || dustSprite == null) return;
        for (int i = 0; i < dustCount; i++)
        {
            Image img = NewImage("Dust", dustArea, dustSprite, 2f);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0f);
            StartCoroutine(Dust(img));
        }
    }

    IEnumerator Dust(Image img)
    {
        Rect area = dustArea.rect;
        float seed = UnityEngine.Random.value * 50f;
        float speed = UnityEngine.Random.Range(8f, 20f);
        float y = UnityEngine.Random.Range(0f, area.height);
        float x0 = UnityEngine.Random.Range(0f, area.width);
        while (true)
        {
            y += speed * Time.unscaledDeltaTime;
            if (y > area.height) { y = 0f; x0 = UnityEngine.Random.Range(0f, area.width); }
            float x = x0 + Mathf.Sin((time + seed) * 0.6f) * 30f;
            img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x / 2f) * 2f, Mathf.Round(y / 2f) * 2f);
            float edge = Mathf.Min(y, area.height - y) / (area.height * 0.25f);
            float twinkle = Mathf.Sin((time + seed) * 2.3f) * 0.3f + 0.7f;
            img.color = new Color(1f, 0.86f, 0.6f, Mathf.Clamp01(edge) * twinkle * 0.8f);
            yield return null;
        }
    }

    Image NewImage(string name, RectTransform parent, Sprite sprite, float scale)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (sprite != null) rt.sizeDelta = sprite.rect.size * scale;
        return img;
    }

    static class Ease
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InQuad(float t) => t * t;
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return t <= 0f ? 0f : 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
