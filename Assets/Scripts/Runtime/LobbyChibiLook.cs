using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 로비를 걸어 다니는 치비(UI Image)의 겉모습을 전투용 모험가 프리팹(몸통 + 손에 든 무기)과 같게 맞춤
// 프리팹의 SpriteRenderer들을 UI Image로 옮겨 그림: 몸통은 치비 자신, 나머지(무기 등)는 자식 Image
public static class LobbyChibiLook
{
    // height: 치비 몸통의 화면 높이(UI 단위). 무기 등은 같은 비율로 맞춤
    public static void Apply(GameObject chibi, GameObject jobPrefab, float height)
    {
        if (chibi == null || jobPrefab == null) return;
        SpriteRenderer bodyRenderer = jobPrefab.GetComponent<SpriteRenderer>();
        Image bodyImage = chibi.GetComponent<Image>();
        RectTransform bodyRect = chibi.GetComponent<RectTransform>();
        if (bodyRenderer == null || bodyRenderer.sprite == null || bodyImage == null || bodyRect == null) return;

        Sprite body = bodyRenderer.sprite;
        float unitsToUi = height / Mathf.Max(0.0001f, body.bounds.size.y); // 프리팹 로컬 단위 → UI 단위

        bodyImage.sprite = body;
        bodyImage.preserveAspect = true;
        bodyImage.color = bodyRenderer.color;
        bodyImage.raycastTarget = false;
        bodyRect.pivot = NormalizedPivot(body);
        bodyRect.sizeDelta = (Vector2)body.bounds.size * unitsToUi;

        // 무기 등 자식 그림: 그리는 순서(sortingOrder)대로 뒤에 붙여 위에 그려지게 함
        List<SpriteRenderer> parts = new List<SpriteRenderer>(jobPrefab.GetComponentsInChildren<SpriteRenderer>(true));
        parts.Remove(bodyRenderer);
        parts.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        foreach (SpriteRenderer part in parts)
        {
            if (part.sprite == null) continue;
            GameObject go = new GameObject(part.name, typeof(RectTransform), typeof(Image));
            go.layer = chibi.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(bodyRect, false);
            rect.anchorMin = rect.anchorMax = bodyRect.pivot;   // 기준점 = 몸통 원점 (프리팹 루트 위치)
            rect.pivot = NormalizedPivot(part.sprite);
            rect.sizeDelta = (Vector2)part.sprite.bounds.size * unitsToUi;
            Vector3 local = jobPrefab.transform.InverseTransformPoint(part.transform.position); // 루트 기준 위치 (루트 크기 무시)
            rect.anchoredPosition = new Vector2(local.x, local.y) * unitsToUi;
            rect.localRotation = Quaternion.Inverse(jobPrefab.transform.rotation) * part.transform.rotation;
            Vector3 s = part.transform.lossyScale, root = jobPrefab.transform.lossyScale;
            rect.localScale = new Vector3(s.x / root.x, s.y / root.y, 1f);

            Image image = go.GetComponent<Image>();
            image.sprite = part.sprite;
            image.color = part.color;
            image.raycastTarget = false;
        }
    }

    /// <summary>
    /// 모험가 한 명의 조립 그림(AdventurerSprites)으로 치비를 꾸미고 걷기·대기 애니메이션을 붙임.
    /// height: 대기 정면 모습의 몸 키(UI 단위). 그림을 만들 수 없으면 false
    /// </summary>
    const float BodyShare = 0.8f;

    public static bool Apply(GameObject chibi, Adventurer adv, float height)
    {
        if (chibi == null || adv == null) return false;
        MonsterSpriteSet set = AdventurerSprites.ForUI(adv);
        Image image = chibi.GetComponent<Image>();
        if (set == null || image == null) return false;

        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = false;
        UISpriteAnimator anim = chibi.GetComponent<UISpriteAnimator>();
        if (anim == null) anim = chibi.AddComponent<UISpriteAnimator>();
        anim.spriteSet = set;
        // 예전 height는 여백 포함 그림 전체 높이였음 → 실제 몸 키는 그 0.8배 정도로 맞춰 화면 크기를 비슷하게 유지
        anim.uiPerPixel = height * BodyShare / Mathf.Max(1f, set.bodyHeight * AdventurerSprites.PxPerUnit);
        anim.Show(MonsterSpriteSet.Anim.Idle, MonsterSpriteSet.Dir.Front, 0f);
        return true;
    }

    static Vector2 NormalizedPivot(Sprite sprite) =>
        new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
}
