using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스방에서 화면 위쪽 가운데에 크게 뜨는 보스 이름과 체력 바 (표현만 담당)
// BattleManager가 보스를 소환할 때 화면 캔버스에 붙이고, 보스가 쓰러지면 스스로 사라짐
public class BossHealthBar : MonoBehaviour
{
    [Header("배치")]
    public float topMargin = 28f;
    public float barWidth = 720f;
    public float barHeight = 30f;
    public float nameFontSize = 52f;
    public float hpFontSize = 22f;

    [Header("색")]
    public Color nameColor = new Color(1f, 0.92f, 0.75f, 1f);
    public Color frameColor = new Color(0f, 0f, 0f, 0.75f);
    public Color emptyColor = new Color(0.25f, 0.08f, 0.08f, 1f);
    public Color fillColor = new Color(0.85f, 0.18f, 0.15f, 1f);

    BattleUnit boss;
    RectTransform fill;
    TextMeshProUGUI hpText;

    public void Setup(BattleUnit target, string bossName, TMP_FontAsset font)
    {
        boss = target;

        RectTransform root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -topMargin);
        root.sizeDelta = new Vector2(barWidth, nameFontSize + barHeight + 16f);

        // 보스 이름
        TextMeshProUGUI nameText = CreateText("Txt_Name", root, bossName, nameFontSize, font);
        nameText.color = nameColor;
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(0f, nameFontSize + 8f);

        // 체력 바: 테두리 → 빈 칸 → 채움 (채움은 오른쪽 끝을 체력 비율만큼 당김)
        RectTransform frame = CreateRect("Bar_Frame", root);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0f);
        frame.anchoredPosition = Vector2.zero;
        frame.sizeDelta = new Vector2(barWidth, barHeight);
        AddImage(frame, frameColor);

        RectTransform empty = CreateRect("Bar_Empty", frame);
        Stretch(empty, 3f);
        AddImage(empty, emptyColor);

        fill = CreateRect("Bar_Fill", empty);
        Stretch(fill, 0f);
        AddImage(fill, fillColor);

        hpText = CreateText("Txt_Hp", frame, "", hpFontSize, font);
        Stretch(hpText.rectTransform, 0f);

        Refresh();
    }

    void Update()
    {
        if (boss == null || boss.isDead) { Destroy(gameObject); return; }
        Refresh();
    }

    void Refresh()
    {
        if (boss == null || fill == null) return;
        float ratio = boss.maxHp > 0f ? Mathf.Clamp01(boss.currentHp / boss.maxHp) : 0f;
        fill.anchorMax = new Vector2(ratio, 1f);
        hpText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, boss.currentHp))} / {Mathf.CeilToInt(boss.maxHp)}";
    }

    // ─── 도우미 ─────────────────────────────────────────────

    static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static TextMeshProUGUI CreateText(string objectName, Transform parent, string message, float size, TMP_FontAsset font)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = message;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
