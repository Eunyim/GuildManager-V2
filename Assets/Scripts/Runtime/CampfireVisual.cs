using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 모닥불 그림 (돌 화덕 + 흔들리는 불꽃 + 바닥 불빛 + 불티·연기 + 둘레의 앉을 자리).
/// BattleManager가 휴식할 때 Light(), 떠날 때 Extinguish()를 부름. 어디에 놓고 누가 어디 앉을지는 BattleManager가 정함.
/// 휴식하는 동안에는 화덕·자리·모험가를 화면 아래쪽에 있는 것이 앞에 오도록 정렬 순서를 매기고, 끝나면 되돌림.
/// 그림은 Tools/campfire_art_gen.py, 연결은 'Tools > 던전 모닥불 그림 연결'.
/// </summary>
public class CampfireVisual : MonoBehaviour
{
    [Header("그림")]
    public Sprite baseSprite;
    public Sprite[] flameFrames;
    public Sprite glowSprite;
    public Sprite seatLogSprite;     // 위·아래 자리 (눕힌 통나무)
    public Sprite seatStumpSprite;   // 옆 자리 (그루터기)
    public Sprite emberSprite;
    public Sprite smokeSprite;

    [Header("연출")]
    [Tooltip("그림 크기 배율 (화덕·불꽃·불빛·앉을 자리)")]
    public float artScale = 1.5f;
    public float flameFps = 10f;
    public float lightUpTime = 0.45f;       // 불이 붙으며 커지는 시간
    public float glowFlicker = 0.18f;       // 불빛 밝기 흔들림 (0~1)
    public float emberInterval = 0.12f;
    public float smokeInterval = 0.5f;
    [Tooltip("정렬 순서: 이 높이 차(월드 단위)마다 1씩 (아래쪽이 앞)")]
    public float sortStep = 0.05f;

    SpriteRenderer glowR, baseR, flameR;
    readonly List<GameObject> seats = new List<GameObject>();
    readonly Dictionary<SpriteRenderer, int> savedOrders = new Dictionary<SpriteRenderer, int>();
    readonly List<(Transform t, SpriteRenderer r, Vector3 v, float life, float age)> particles = new List<(Transform, SpriteRenderer, Vector3, float, float)>();
    float litTime = -1f, emberTimer, smokeTimer, sortTop;
    bool built;

    public bool IsLit => litTime >= 0f;

    void Build()
    {
        if (built) return;
        built = true;
        SpriteRenderer own = GetComponent<SpriteRenderer>();
        if (own != null) own.enabled = false; // 예전 임시 그림
        glowR = Child("Glow", glowSprite);
        baseR = Child("Hearth", baseSprite);
        flameR = Child("Flame", flameFrames != null && flameFrames.Length > 0 ? flameFrames[0] : null);
    }

    SpriteRenderer Child(string name, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        return r;
    }

    /// <summary>
    /// center에 불을 피우고 seats 자리마다 통나무/그루터기를 놓음. floorOrder = 바닥 타일 정렬 순서 (불빛은 그 바로 위)
    /// </summary>
    public void Light(Vector3 center, IList<Vector3> seatPoints, int floorOrder)
    {
        Build();
        Extinguish();
        gameObject.SetActive(true);
        transform.position = center;
        sortTop = center.y + 4f;

        glowR.sortingOrder = floorOrder + 1;
        baseR.sortingOrder = OrderFor(center.y);
        flameR.sortingOrder = baseR.sortingOrder + 1;

        for (int i = 0; i < seatPoints.Count; i++)
        {
            Vector3 p = seatPoints[i];
            Vector3 d = p - center;
            bool side = Mathf.Abs(d.x) > Mathf.Abs(d.y) * 2.2f; // 옆쪽 자리는 그루터기
            GameObject seat = new GameObject($"Seat_{i}");
            seat.transform.position = p;
            SpriteRenderer r = seat.AddComponent<SpriteRenderer>();
            r.sprite = side ? seatStumpSprite : seatLogSprite;
            r.sortingOrder = OrderFor(p.y + 0.05f); // 옆자리는 앉은 모험가 뒤, 아래 자리(등을 보이고 앉음)는 앞
            seat.transform.localScale = Vector3.one * artScale;
            seats.Add(seat);
        }
        litTime = 0f;
        ApplyLightUp(0f);
    }

    /// <summary>휴식하는 동안 이 그림을 높이(y)에 맞춰 앞뒤로 정렬 (끝나면 Extinguish가 되돌림)</summary>
    public void SortRenderer(SpriteRenderer r, float y)
    {
        if (r == null) return;
        if (!savedOrders.ContainsKey(r)) savedOrders[r] = r.sortingOrder;
        r.sortingOrder = OrderFor(y);
    }

    int OrderFor(float y) => 20 + Mathf.RoundToInt((sortTop - y) / Mathf.Max(0.001f, sortStep));

    public void Extinguish()
    {
        foreach (var pair in savedOrders) if (pair.Key != null) pair.Key.sortingOrder = pair.Value;
        savedOrders.Clear();
        foreach (GameObject s in seats) if (s != null) Destroy(s);
        seats.Clear();
        foreach (var p in particles) if (p.t != null) Destroy(p.t.gameObject);
        particles.Clear();
        litTime = -1f;
        if (built) gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // 다른 곳(방 이동 등)에서 꺼도 정렬 순서와 자리는 되돌림
        if (litTime >= 0f) Extinguish();
    }

    void Update()
    {
        if (litTime < 0f) return;
        float dt = Time.deltaTime;
        litTime += dt;
        ApplyLightUp(Mathf.Clamp01(litTime / lightUpTime));

        if (flameFrames != null && flameFrames.Length > 0)
            flameR.sprite = flameFrames[(int)(litTime * flameFps) % flameFrames.Length];

        float flick = 1f - glowFlicker + glowFlicker * Mathf.PerlinNoise(litTime * 3.1f, 0.7f);
        glowR.color = new Color(1f, 1f, 1f, flick * Mathf.Clamp01(litTime / lightUpTime));

        emberTimer -= dt;
        if (emberTimer <= 0f && emberSprite != null)
        {
            emberTimer = emberInterval * Random.Range(0.6f, 1.4f);
            Spawn(emberSprite, new Vector3(Random.Range(-0.12f, 0.12f), 0.25f, 0f) * artScale,
                  new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.7f, 1.2f), 0f), Random.Range(0.7f, 1.2f));
        }
        smokeTimer -= dt;
        if (smokeTimer <= 0f && smokeSprite != null)
        {
            smokeTimer = smokeInterval * Random.Range(0.7f, 1.3f);
            Spawn(smokeSprite, new Vector3(Random.Range(-0.05f, 0.05f), 0.55f, 0f) * artScale,
                  new Vector3(Random.Range(0.02f, 0.15f), 0.45f, 0f), Random.Range(1.8f, 2.6f));
        }

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.age += dt;
            if (p.t == null || p.age >= p.life)
            {
                if (p.t != null) Destroy(p.t.gameObject);
                particles.RemoveAt(i);
                continue;
            }
            float k = p.age / p.life;
            p.t.position += p.v * dt;
            if (p.r.sprite == smokeSprite)
            {
                p.t.localScale = Vector3.one * Mathf.Lerp(0.6f, 2f, k);
                p.r.color = new Color(1f, 1f, 1f, Mathf.Sin(Mathf.PI * k) * 0.6f);
            }
            else p.r.color = new Color(1f, 1f, 1f, 1f - k * k);
            particles[i] = p;
        }
    }

    void Spawn(Sprite sprite, Vector3 offset, Vector3 velocity, float life)
    {
        GameObject go = new GameObject("Particle");
        go.transform.position = transform.position + offset;
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = flameR.sortingOrder + 1;
        particles.Add((go.transform, r, velocity, life, 0f));
    }

    // 불이 붙는 연출: 불꽃이 바닥에서 솟고 화덕이 살짝 튐
    void ApplyLightUp(float k)
    {
        float pop = k < 1f ? 1f + Mathf.Sin(k * Mathf.PI) * 0.12f : 1f;
        baseR.transform.localScale = new Vector3(pop, 1f / pop, 1f) * artScale;
        float grow = 1f - (1f - k) * (1f - k);
        flameR.transform.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, grow), grow, 1f) * artScale;
        glowR.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, grow) * artScale;
    }
}
