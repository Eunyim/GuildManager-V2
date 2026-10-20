using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Image용 도트 애니메이션 (로비를 걸어 다니는 치비). 위치가 바뀌면 walk, 멈추면 idle
/// 프레임마다 잘린 크기가 달라서 바꿀 때마다 Image 크기·기준점(발)을 맞춤. 좌우는 LobbyWalking이 크기 x로 뒤집음
/// </summary>
[RequireComponent(typeof(Image))]
public class UISpriteAnimator : MonoBehaviour
{
    public MonsterSpriteSet spriteSet;
    [Tooltip("그림 1픽셀이 UI에서 차지하는 크기")]
    public float uiPerPixel = 2f;
    [Tooltip("이 속도(UI 단위/초) 이상으로 움직이면 걷는 것으로 봄")]
    public float moveThreshold = 4f;
    [Tooltip("위아래 움직임이 좌우의 몇 배를 넘으면 앞/뒤 모습")]
    public float verticalBias = 1.2f;

    Image image;
    RectTransform rect;
    Vector2 lastPos;
    MonsterSpriteSet.Anim state = MonsterSpriteSet.Anim.Idle;
    MonsterSpriteSet.Dir dir = MonsterSpriteSet.Dir.Front;
    float t;
    float moveMemory;
    Sprite shown;

    void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        lastPos = rect.anchoredPosition;
        t = Random.value;
    }

    void LateUpdate()
    {
        if (spriteSet == null) return;
        float dt = Time.deltaTime;
        Vector2 delta = rect.anchoredPosition - lastPos;
        lastPos = rect.anchoredPosition;
        bool moving = dt > 0f && delta.magnitude / dt > moveThreshold;
        if (moving)
        {
            moveMemory = 0.15f;
            dir = Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * verticalBias
                ? (delta.y < 0f ? MonsterSpriteSet.Dir.Front : MonsterSpriteSet.Dir.Back)
                : MonsterSpriteSet.Dir.Side;
        }
        else moveMemory -= dt;

        MonsterSpriteSet.Anim want = moveMemory > 0f ? MonsterSpriteSet.Anim.Walk : MonsterSpriteSet.Anim.Idle;
        if (want != state)
        {
            state = want;
            t = 0f;
            if (state == MonsterSpriteSet.Anim.Idle && dir == MonsterSpriteSet.Dir.Back) dir = MonsterSpriteSet.Dir.Front; // 멈추면 이쪽을 봄
        }
        t += dt;
        Show(state, dir, t);
    }

    public void Show(MonsterSpriteSet.Anim anim, MonsterSpriteSet.Dir d, float time)
    {
        // 꺼진 창에 막 붙인 경우 Awake가 아직 안 불렸을 수 있음
        if (image == null) image = GetComponent<Image>();
        if (rect == null) rect = (RectTransform)transform;
        MonsterSpriteSet.Clip c = spriteSet != null ? spriteSet.Get(anim, d) : null;
        if (c == null) return;
        int i = (int)(time * c.fps);
        Sprite s = c.frames[c.loop ? i % c.frames.Length : Mathf.Min(i, c.frames.Length - 1)];
        if (s == null || s == shown) return;
        shown = s;
        image.sprite = s;
        Vector2 size = s.rect.size;
        rect.pivot = new Vector2(s.pivot.x / size.x, s.pivot.y / size.y);
        rect.sizeDelta = size * uiPerPixel;
    }
}
