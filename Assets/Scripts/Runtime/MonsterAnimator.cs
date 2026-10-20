using UnityEngine;

/// <summary>
/// 몬스터·모험가 도트 애니메이션 재생 (화면 표현만). 전투 로직은 건드리지 않고 BattleUnit을 지켜봄
///  - 움직이면 walk, 멈추면 idle, BattleUnit.AttackStarted가 오면 attack, SkillStarted면 skill을 한 번
///  - 방향: 움직이는 쪽(멈춰 있으면 대상 쪽)이 위아래로 더 크면 front/back, 아니면 side
///  - 죽으면 SpawnCorpse()로 death를 재생하는 시체를 남김 (BattleUnit.Die에서 부름)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MonsterAnimator : MonoBehaviour
{
    public MonsterSpriteSet spriteSet;
    [Tooltip("보스가 되면 쓰는 그림 (예: 슬라임 → 킹 슬라임). BattleManager가 UseBossSet()으로 바꿈")]
    public MonsterSpriteSet bossSet;

    [Header("판정")]
    [Tooltip("이 속도(유닛/초) 이상으로 움직이면 걷는 것으로 봄")]
    public float moveThreshold = 0.15f;
    [Tooltip("위아래 움직임이 좌우의 몇 배를 넘으면 앞/뒤 모습")]
    public float verticalBias = 0.9f;

    [Header("사망 연출")]
    public float corpseHold = 0.5f;
    public float corpseFade = 0.6f;

    SpriteRenderer sr;
    Material baseMaterial;   // 피격 번쩍임 재질로 바뀌기 전 원래 재질 (시체에 씀)
    BattleUnit unit;
    Vector3 lastPos;
    MonsterSpriteSet.Dir dir = MonsterSpriteSet.Dir.Side;
    MonsterSpriteSet.Anim state = MonsterSpriteSet.Anim.Idle;
    float stateTime;
    float attackUntil;
    float moveMemory;   // 한두 프레임 멈춰도 걷기가 끊기지 않게

    public MonsterSpriteSet Current => spriteSet;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseMaterial = sr.sharedMaterial;
        unit = GetComponent<BattleUnit>();
        lastPos = transform.position;
        stateTime = Random.value; // 같은 몬스터들이 똑같이 움직이지 않게
    }

    void OnEnable()
    {
        if (unit == null) return;
        unit.AttackStarted += OnAttack;
        unit.SkillStarted += OnSkill;
    }

    void OnDisable()
    {
        if (unit == null) return;
        unit.AttackStarted -= OnAttack;
        unit.SkillStarted -= OnSkill;
    }

    public void UseBossSet()
    {
        if (bossSet != null) spriteSet = bossSet;
        Apply(true);
    }

    /// <summary>보이는 몸의 키 (월드 유닛)</summary>
    public float VisualHeight => spriteSet != null ? spriteSet.bodyHeight * Mathf.Abs(transform.lossyScale.y) : 0f;

    /// <summary>가만히 있을 때 point 쪽을 바라봄 (앞·뒤·옆 모습 고르기. 좌우 뒤집기는 BattleUnit.SetFacing)</summary>
    public void Face(Vector3 point) => dir = DirOf(point - transform.position, dir);

    void OnAttack() => PlayAction(MonsterSpriteSet.Anim.Attack);

    // 스킬 그림이 없는 몬스터는 공격 그림으로
    void OnSkill() => PlayAction(spriteSet != null && spriteSet.Has(MonsterSpriteSet.Anim.Skill) ? MonsterSpriteSet.Anim.Skill : MonsterSpriteSet.Anim.Attack);

    void PlayAction(MonsterSpriteSet.Anim a)
    {
        MonsterSpriteSet.Clip c = spriteSet != null ? spriteSet.Get(a, dir) : null;
        if (c == null) return;
        if (unit != null && unit.CurrentTarget != null && unit.CurrentTarget != unit)
            dir = DirOf(unit.CurrentTarget.transform.position - transform.position, dir);
        attackUntil = Time.time + c.frames.Length / Mathf.Max(1f, c.fps);
        state = MonsterSpriteSet.Anim.Idle;   // 같은 동작을 연달아 해도 처음부터
        SetState(a);
    }

    void LateUpdate()
    {
        if (spriteSet == null) return;
        float dt = Time.deltaTime;
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;

        bool moving = dt > 0f && delta.magnitude / dt > moveThreshold;
        if (moving) moveMemory = 0.12f; else moveMemory -= dt;

        if (unit != null && unit.IsSeated && spriteSet.Has(MonsterSpriteSet.Anim.Sit))
        {
            SetState(MonsterSpriteSet.Anim.Sit);   // 모닥불 자리에 앉음 (바라보는 쪽은 Face로 정해짐)
        }
        else if (Time.time < attackUntil)
        {
            // 공격 중: 방향 그대로
        }
        else if (moving || moveMemory > 0f)
        {
            if (moving) dir = DirOf(delta, dir);
            SetState(MonsterSpriteSet.Anim.Walk);
        }
        else
        {
            if (unit != null && unit.CurrentTarget != null && unit.CurrentTarget != unit)
                dir = DirOf(unit.CurrentTarget.transform.position - transform.position, dir);
            SetState(MonsterSpriteSet.Anim.Idle);
        }
        stateTime += dt;
        Apply(false);
    }

    MonsterSpriteSet.Dir DirOf(Vector3 v, MonsterSpriteSet.Dir current)
    {
        if (v.sqrMagnitude < 1e-6f) return current;
        if (Mathf.Abs(v.y) > Mathf.Abs(v.x) * verticalBias)
            return v.y < 0f ? MonsterSpriteSet.Dir.Front : MonsterSpriteSet.Dir.Back;
        return MonsterSpriteSet.Dir.Side;
    }

    void SetState(MonsterSpriteSet.Anim s)
    {
        if (state == s) return;
        state = s;
        stateTime = 0f;
    }

    void Apply(bool force)
    {
        MonsterSpriteSet.Clip c = spriteSet.Get(state, dir);
        if (c == null) return;
        int n = c.frames.Length;
        int i = (int)(stateTime * c.fps);
        i = c.loop ? i % n : Mathf.Min(i, n - 1);
        Sprite s = c.frames[i];
        if (force || sr.sprite != s) sr.sprite = s;
    }

    /// <summary>쓰러지는 모습을 재생하고 사라지는 시체를 남김</summary>
    public void SpawnCorpse()
    {
        if (spriteSet == null || sr == null) return;
        MonsterSpriteSet.Clip c = spriteSet.Get(MonsterSpriteSet.Anim.Death, dir);
        if (c == null) return;

        GameObject go = new GameObject(gameObject.name + "_Corpse");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = c.frames[0];
        r.color = sr.color;
        r.sortingLayerID = sr.sortingLayerID;
        r.sortingOrder = sr.sortingOrder;
        r.sharedMaterial = baseMaterial;
        go.AddComponent<MonsterCorpse>().Play(r, c.frames, c.fps, corpseHold, corpseFade);
    }
}

/// <summary>사망 프레임을 한 번 재생 → 잠깐 누워 있다가 → 흐려지며 사라짐</summary>
public class MonsterCorpse : MonoBehaviour
{
    SpriteRenderer sr;
    Sprite[] frames;
    float fps, hold, fade, t;
    Color baseColor;

    public void Play(SpriteRenderer renderer, Sprite[] frames, float fps, float hold, float fade)
    {
        sr = renderer;
        this.frames = frames;
        this.fps = Mathf.Max(1f, fps);
        this.hold = hold;
        this.fade = Mathf.Max(0.01f, fade);
        baseColor = sr.color;
    }

    void Update()
    {
        if (sr == null) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        float playTime = frames.Length / fps;
        sr.sprite = frames[Mathf.Min((int)(t * fps), frames.Length - 1)];
        float f = (t - playTime - hold) / fade;
        if (f > 0f)
        {
            Color c = baseColor;
            c.a = baseColor.a * (1f - Mathf.Clamp01(f));
            sr.color = c;
            if (f >= 1f) Destroy(gameObject);
        }
    }
}
