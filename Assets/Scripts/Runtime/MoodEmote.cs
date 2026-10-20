using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무드가 오르거나 내릴 때 모험가 옆에 잠깐 뜨는 사각형 말풍선 (:) / :().
/// 로비 치비(UI)와 던전 유닛(월드) 둘 다 쓰며, 대상을 따라다니다 사라집니다. 대상이 좌우로 뒤집혀도 글자는 뒤집히지 않습니다.
/// </summary>
public class MoodEmote : MonoBehaviour
{
    public const string HappyFace = ":)";
    public const string SadFace   = ":(";

    static readonly Color BubbleColor = new Color(1f, 1f, 1f, 0.95f);
    static readonly Color HappyColor  = new Color(0.15f, 0.55f, 0.2f);
    static readonly Color SadColor    = new Color(0.7f, 0.15f, 0.15f);

    private Transform target;
    private Vector3 offset;          // 대상 기준 위치 (UI는 anchoredPosition 기준, 월드는 월드 좌표 기준)
    private bool isUI;
    private float lifetime, age;
    private CanvasGroup canvasGroup; // UI 페이드
    private SpriteRenderer[] sprites; // 월드 페이드
    private TextMeshPro worldText;

    static Sprite square;

    // 로비 치비 (UI). target과 같은 부모 아래에 만들어 따라다님
    public static void ShowUI(RectTransform target, bool happy, float duration = 2.5f)
    {
        if (target == null || target.parent == null) return;
        Clear(target);

        float size = Mathf.Clamp(target.rect.height * 0.32f, 30f, 60f);
        GameObject root = new GameObject("MoodEmote", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = target.gameObject.layer;
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(target.parent, false);
        rect.anchorMin = target.anchorMin;
        rect.anchorMax = target.anchorMax;
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(size * 1.3f, size);

        // 꼬리 (말풍선 아래 왼쪽의 기울인 사각형)
        Image tail = CreateUIImage("Tail", rect, BubbleColor);
        tail.rectTransform.sizeDelta = new Vector2(size * 0.3f, size * 0.3f);
        tail.rectTransform.anchoredPosition = new Vector2(-size * 0.4f, -size * 0.45f);
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image bubble = CreateUIImage("Bubble", rect, BubbleColor);
        bubble.rectTransform.anchorMin = Vector2.zero;
        bubble.rectTransform.anchorMax = Vector2.one;
        bubble.rectTransform.offsetMin = Vector2.zero;
        bubble.rectTransform.offsetMax = Vector2.zero;
        Outline outline = bubble.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(rect, false);
        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI fontSource = target.GetComponentInParent<Canvas>() != null ? target.GetComponentInParent<Canvas>().GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (fontSource != null) text.font = fontSource.font;
        text.text = happy ? HappyFace : SadFace;
        text.color = happy ? HappyColor : SadColor;
        text.fontSize = size * 0.6f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        MoodEmote emote = root.AddComponent<MoodEmote>();
        emote.target = target;
        emote.isUI = true;
        emote.lifetime = duration;
        emote.canvasGroup = root.GetComponent<CanvasGroup>();
        emote.canvasGroup.blocksRaycasts = false;
        emote.canvasGroup.interactable = false;
        // 치비 머리 오른쪽 위 (치비 pivot 기준)
        emote.offset = new Vector3(target.rect.xMax * 0.6f, target.rect.yMax * 0.85f, 0f);
        emote.Follow();
        rect.SetAsLastSibling();
    }

    // 던전 유닛 (월드). 유닛 그림 위에 떠서 따라다님
    public static void ShowWorld(Transform target, bool happy, float duration = 2f)
    {
        if (target == null) return;

        SpriteRenderer body = target.GetComponentInChildren<SpriteRenderer>();
        float top = body != null ? body.bounds.max.y - target.position.y : 0.8f;
        float size = 0.45f;

        GameObject root = new GameObject("MoodEmote");
        Transform tr = root.transform;

        SpriteRenderer tail = CreateWorldSquare("Tail", tr, new Vector2(size * 0.3f, size * 0.3f), 998);
        tail.transform.localPosition = new Vector3(-size * 0.35f, -size * 0.45f, 0f);
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        SpriteRenderer bubble = CreateWorldSquare("Bubble", tr, new Vector2(size * 1.3f, size), 999);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshPro));
        textGo.transform.SetParent(tr, false);
        TextMeshPro text = textGo.GetComponent<TextMeshPro>();
        text.text = happy ? HappyFace : SadFace;
        text.color = happy ? HappyColor : SadColor;
        text.fontSize = 3f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(size * 1.3f, size);
        text.sortingOrder = 1000;

        MoodEmote emote = root.AddComponent<MoodEmote>();
        emote.target = target;
        emote.isUI = false;
        emote.lifetime = duration;
        emote.offset = new Vector3(0.35f, top + size * 0.6f, 0f);
        emote.sprites = new[] { tail, bubble };
        emote.worldText = text;
        emote.Follow();
    }

    // 같은 대상에 떠 있던 말풍선은 지움 (겹치지 않게)
    static void Clear(Transform target)
    {
        foreach (MoodEmote e in FindObjectsByType<MoodEmote>(FindObjectsSortMode.None))
            if (e.target == target) Destroy(e.gameObject);
    }

    void LateUpdate()
    {
        age += Time.unscaledDeltaTime;
        if (target == null || age >= lifetime) { Destroy(gameObject); return; }
        Follow();

        // 마지막 0.4초 동안 흐려짐
        float alpha = Mathf.Clamp01((lifetime - age) / 0.4f);
        if (canvasGroup != null) canvasGroup.alpha = alpha;
        if (sprites != null)
            foreach (SpriteRenderer sr in sprites) { Color c = sr.color; c.a = BubbleColor.a * alpha; sr.color = c; }
        if (worldText != null) worldText.alpha = alpha;
    }

    void Follow()
    {
        if (isUI)
        {
            RectTransform t = (RectTransform)target, me = (RectTransform)transform;
            me.anchoredPosition = t.anchoredPosition + (Vector2)offset;
        }
        else transform.position = target.position + offset;
    }

    static Image CreateUIImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static SpriteRenderer CreateWorldSquare(string name, Transform parent, Vector2 size, int order)
    {
        if (square == null)
        {
            Texture2D tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 1x1 월드 유닛
        }
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = BubbleColor;
        sr.sortingOrder = order;
        return sr;
    }
}
