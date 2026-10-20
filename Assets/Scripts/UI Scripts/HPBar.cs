using UnityEngine;
using UnityEngine.UI;

// 유닛 머리 위 체력·마나 바 (UnitHud 프리팹). 모양은 프리팹에서, 아래 수치는 인스펙터에서 조정
//  - 유닛 그림의 머리 바로 위에 붙고, 유닛 크기·좌우 뒤집힘과 상관없이 항상 같은 크기로 보임
//  - 아군은 초록, 적은 빨강. 마나가 없는 유닛은 마나 줄을 숨기고 바 높이를 줄임
public class HPBar : MonoBehaviour
{
    public Slider hpSlider;
    public Slider mpSlider;

    [Header("모양")]
    public Color allyHpColor  = new Color32(0x6C, 0xC0, 0x4A, 0xFF);
    public Color enemyHpColor = new Color32(0xD8, 0x48, 0x3A, 0xFF);
    [Tooltip("유닛 그림 윗변에서 바 아랫변까지 (월드 유닛, 음수면 머리와 겹침)")]
    public float headGap = 0.05f;
    [Tooltip("유닛 크기·좌우 뒤집힘과 상관없이 같은 크기로 보이게 함")]
    public bool keepWorldSize = true;
    [Tooltip("마나가 없는 유닛의 바 높이 (0이면 그대로)")]
    public float heightWithoutMp = 20f;

    // 체력바 잔상: 채움 막대를 복제해 뒤에 깔고, 깎인 만큼 잠깐 남았다가 줄어들게 함 (수치는 BattleManager.hitFeedback)
    RectTransform trailRect;
    float trailRatio = -1f; // 잔상이 보여 주는 비율 (0~1)
    float trailHoldLeft;    // 줄어들기 시작할 때까지 남은 시간

    RectTransform rect;
    Vector3 baseScale;
    float fullHeight;
    SpriteRenderer body;

    void Awake()
    {
        rect = (RectTransform)transform;
        baseScale = transform.localScale;
        fullHeight = rect.sizeDelta.y;
    }

    // 초기 설정 (생성될 때 한 번 호출)
    public void Setup(BattleUnit unit)
    {
        body = unit.GetComponent<SpriteRenderer>();

        if (hpSlider != null)
        {
            hpSlider.maxValue = unit.maxHp;
            hpSlider.value = unit.currentHp;
            Image fill = hpSlider.fillRect != null ? hpSlider.fillRect.GetComponent<Image>() : null;
            if (fill != null) fill.color = unit.CompareTag("Player") ? allyHpColor : enemyHpColor;
            CreateTrail();
        }

        if (mpSlider != null)
        {
            mpSlider.maxValue = unit.maxMp;
            mpSlider.value = unit.currentMp;
            SetMpVisible(unit.maxMp > 0f);
        }
        FollowHead();
    }

    // ★ [핵심] 매 프레임 체력/마나 갱신하는 함수 (이게 없어서 에러 남!)
    public void UpdateBar(float currentHp, float maxHp, float currentMp, float maxMp)
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHp;
            hpSlider.value = currentHp;
            OnHpChanged();
        }

        if (mpSlider != null)
        {
            SetMpVisible(maxMp > 0f); // 마나 없는 유닛은 MP 바 숨김
            mpSlider.maxValue = maxMp;
            mpSlider.value = currentMp;
        }
    }

    // 마나 줄을 숨기면 그만큼 바 높이를 줄임 (체력 줄은 위쪽에 붙어 있음)
    void SetMpVisible(bool visible)
    {
        if (mpSlider.gameObject.activeSelf == visible) return;
        mpSlider.gameObject.SetActive(visible);
        if (rect == null) return;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, visible || heightWithoutMp <= 0f ? fullHeight : heightWithoutMp);
    }

    // 머리 바로 위에, 유닛 크기와 상관없이 같은 크기로
    void FollowHead()
    {
        if (body == null || rect == null) return;
        Transform parent = transform.parent;
        if (keepWorldSize && parent != null)
        {
            Vector3 ps = parent.lossyScale;
            if (ps.x != 0f && ps.y != 0f) transform.localScale = new Vector3(baseScale.x / ps.x, baseScale.y / ps.y, baseScale.z);
        }

        Bounds b = body.bounds;
        Vector3 s = transform.lossyScale;
        Vector2 size = rect.rect.size;
        float x = b.center.x + (rect.pivot.x - 0.5f) * size.x * Mathf.Abs(s.x);
        float y = b.max.y + headGap + rect.pivot.y * size.y * Mathf.Abs(s.y);
        transform.position = new Vector3(x, y, transform.position.z);
    }

    static HitFeedbackSettings Settings => BattleManager.Instance != null ? BattleManager.Instance.hitFeedback : null;

    float HpRatio => hpSlider.maxValue > hpSlider.minValue
        ? Mathf.Clamp01((hpSlider.value - hpSlider.minValue) / (hpSlider.maxValue - hpSlider.minValue)) : 0f;

    void CreateTrail()
    {
        HitFeedbackSettings s = Settings;
        if (trailRect != null || s == null || !s.hpTrailEnabled || hpSlider.fillRect == null) return;

        RectTransform fill = hpSlider.fillRect;
        GameObject trail = Instantiate(fill.gameObject, fill.parent);
        trail.name = "Fill_Trail";
        trail.transform.SetSiblingIndex(fill.GetSiblingIndex()); // 원래 채움 막대 뒤에 그려지도록
        trailRect = trail.GetComponent<RectTransform>();
        Image img = trail.GetComponent<Image>();
        if (img != null) img.color = s.hpTrailColor;
        trailRatio = HpRatio;
        ApplyTrail();
    }

    void OnHpChanged()
    {
        if (trailRect == null) return;
        float ratio = HpRatio;
        if (ratio < trailRatio) trailHoldLeft = Settings != null ? Settings.hpTrailDelay : 0f; // 깎일 때마다 잠깐 멈춤
        else trailRatio = ratio; // 회복은 바로 따라감
    }

    void LateUpdate()
    {
        FollowHead();

        if (trailRect == null) return;
        float ratio = HpRatio;
        if (trailRatio > ratio)
        {
            if (trailHoldLeft > 0f) trailHoldLeft -= Time.deltaTime;
            else trailRatio = Mathf.Max(ratio, trailRatio - (Settings != null ? Settings.hpTrailSpeed : 1f) * Time.deltaTime);
        }
        else trailRatio = ratio;
        ApplyTrail();
    }

    // 슬라이더의 왼쪽→오른쪽 채움과 같은 방식으로 잔상 길이를 맞춤
    void ApplyTrail()
    {
        Vector2 max = trailRect.anchorMax;
        max.x = trailRatio;
        trailRect.anchorMax = max;
        Vector2 min = trailRect.anchorMin;
        min.x = 0f;
        trailRect.anchorMin = min;
    }
}
