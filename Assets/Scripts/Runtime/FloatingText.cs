using TMPro;
using UnityEngine;

/// <summary>
/// 전투 중 머리 위로 떠올랐다 사라지는 숫자 (데미지 / 회복량).
/// 수치는 BattleManager가 넘겨주고, 여기서는 떠오르고 흐려지는 표현만 담당합니다.
/// </summary>
public class FloatingText : MonoBehaviour
{
    private TMP_Text label;
    private float lifetime;
    private float riseSpeed;
    private float elapsed;
    private Color baseColor;
    private Vector3 baseScale;
    private float popScale = 1f, popTime;
    private TMP_Text shadow;   // 글자 뒤 그림자 (같이 흐려짐)
    private Color shadowColor;

    // scale: 최종 크기 배율 (치명타는 크게), popScale: 처음 나올 때 그보다 몇 배 컸다가 popTime 동안 줄어듦
    public void Play(TMP_Text text, string message, Color color, float lifetime, float riseSpeed,
                     float scale = 1f, float popScale = 1f, float popTime = 0f, TMP_Text shadow = null)
    {
        label = text;
        this.shadow = shadow;
        if (shadow != null) shadowColor = shadow.color;
        baseScale = transform.localScale * scale;
        this.popScale = popScale;
        this.popTime = popTime;
        transform.localScale = popTime > 0f ? baseScale * popScale : baseScale;
        this.lifetime = Mathf.Max(0.05f, lifetime);
        this.riseSpeed = riseSpeed;
        baseColor = color;

        label.text = message;
        label.color = color;
    }

    void Update()
    {
        if (label == null) { Destroy(gameObject); return; }

        elapsed += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // 툭 튀어나오듯 컸다가 원래 크기로
        if (popTime > 0f)
        {
            float p = Mathf.Clamp01(elapsed / popTime);
            transform.localScale = baseScale * Mathf.Lerp(popScale, 1f, 1f - (1f - p) * (1f - p));
        }

        // 수명의 뒤쪽 절반 동안 서서히 사라짐
        float t = elapsed / lifetime;
        Color c = baseColor;
        c.a = baseColor.a * Mathf.Clamp01((1f - t) * 2f);
        label.color = c;
        if (shadow != null)
        {
            Color sc = shadowColor;
            sc.a = shadowColor.a * c.a / Mathf.Max(0.001f, baseColor.a);
            shadow.color = sc;
        }

        if (elapsed >= lifetime) Destroy(gameObject);
    }
}
