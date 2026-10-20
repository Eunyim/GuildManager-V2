using UnityEngine;
using System.Collections.Generic;

public class BattleUnit : MonoBehaviour
{
    [Header("기본 스탯")]
    public float maxHp = 100;
    public float currentHp;
    public float maxMp = 100;
    public float currentMp = 0;
    
    [Header("전투 설정")]
    public float attackRange = 1.5f; 
    public float moveSpeed = 2.0f;
    public float attackPower = 10f;
    public float defense = 0f; // 방어력 (모험가만, 적은 0): 받는 피해 × 100 ÷ (100 + 방어력)
    public float attackCooldown = 1.0f;
    public float mpRegenOnHit = 20;

    [Header("UI & 이펙트")]
    public GameObject hpBarPrefab;
    private HPBar myHPBar; 

    [Header("특성 시스템")]
    public List<TraitData> activeTraits = new List<TraitData>();
    protected float baseAttackPower;
    protected float baseMoveSpeed;
    protected float baseDefense;
    protected bool isCoward = false;

    // 치명타·회피: 모험가만 (직업별 기본값, GameManager.combatStats). 적은 0
    [HideInInspector] public float critChance = 0f;            // 기본 공격 치명타 확률
    protected float baseEvasion = 0f;

    // 특성이 매 프레임 정하는 값 (특성이 없으면 기본값 그대로)
    [HideInInspector] public float evasion = 0f;               // 회피율 (0~1). 회피하면 그 공격의 피해가 없음
    [HideInInspector] public float evasionMultiplier = 1f;     // 회피율 배율 (외눈박이 0.7). 더한 회피율에 마지막으로 곱함
    public float EffectiveEvasion => Mathf.Clamp01(evasion * evasionMultiplier);
    [HideInInspector] public float damageTakenMultiplier = 1f; // 받는 피해 배율 (약골)
    [HideInInspector] public float bleedTakenMultiplier = 1f;  // 받는 출혈 피해 배율 (단단한 피부)
    [HideInInspector] public float stunTakenMultiplier = 1f;   // 받는 스턴 시간 배율 (강인함)

    // 나오자마자 기절해 있을 시간 (예: 신중한 성격이 알아챈 미믹). 소환 직후 Start 전에 넣음 (Start 전에는 체력이 0이라 Stun이 무시되므로)
    [HideInInspector] public float stunOnStart = 0f;

    // 마지막으로 나를 때린 유닛 (드래곤슬레이어 판정용)
    private BattleUnit lastAttacker;

    // 이 유닛의 적 계열 (괴수/인간/언데드/그린스킨/드래곤, 아군은 빈 문자열). 사냥꾼·공포증 특성이 씀
    private string family;
    public string Family => family ??= SourceData != null ? "" : EnemyFamilyRules.FromPrefabName(gameObject.name);

    // Initialize()가 호출되었는지 여부 (Start()에서 하드코딩 값 덮어쓰기 방지용)
    protected bool isInitialized = false;

    protected BattleUnit target; 
    protected Animator anim;     

    // 기본 공격 / 스킬을 시작할 때 알림. 화면 연출(MonsterAnimator 등)용
    public event System.Action AttackStarted;
    public event System.Action SkillStarted;
    public BattleUnit CurrentTarget => target;
    protected float lastAttackTime; 

    [Header("AI 설정")]
    public float aggro = 10f; // 어그로 수치 (전사는 프리팹에서 높게 설정)

    [Header("파밍(Looting) 시스템")]
    private Transform chestTarget; // 열어야 할 보물상자 위치
    private bool isLooting = false; // 지금 파밍하러 가는 중인가?

    // 이 유닛의 원본 모험가 데이터 (아군만 있음. 적은 null)
    public Adventurer SourceData { get; private set; }

    // 마우스를 올렸을 때 보여줄 기본 정보: 아군은 이름/직업/레벨, 적은 종류
    public string GetHoverText()
    {
        string hpLine = $"HP {Mathf.CeilToInt(Mathf.Max(currentHp, 0f))} / {Mathf.CeilToInt(maxHp)}";

        if (SourceData != null)
            return $"{SourceData.name}\n{DisplayNames.JobToKorean(SourceData.job)}  Lv.{SourceData.level}\n{hpLine}  DEF {defense:F0}";

        return $"{DisplayNames.EnemyToKorean(gameObject.name)}\n{hpLine}";
    }

    public void Initialize(Adventurer data)
    {
        isInitialized = true;
        SourceData = data;
        float multiplier = MoraleRules.Multiplier(data.morale);
        GameManager gmEq = GameManager.Instance;
        int eqHp = gmEq != null ? gmEq.EquipHp(data) : 0, eqAtk = gmEq != null ? gmEq.EquipAtk(data) : 0, eqDef = gmEq != null ? gmEq.EquipDef(data) : 0; // 장비
        maxHp = (data.hp + eqHp) * multiplier;
        // 요양 중인 부상자는 다친 비율 그대로 출발 (건강한 모험가는 가득 찬 체력).
        // 체력은 "최대 체력 대비 비율"로 옮김 → 사기로 최대 체력이 줄어도 부상 정도는 그대로
        float hpRatio = data.hp > 0 ? (float)data.currentHp / data.hp : 1f;
        currentHp = data.recoveryWeeks > 0 ? Mathf.Clamp(maxHp * hpRatio, 1f, maxHp) : maxHp;
        attackPower = (data.atk + eqAtk) * multiplier;
        defense = data.def + eqDef;
        moveSpeed = data.speed;
        currentMp = 0;
        name = $"Unit_{data.name}";

        if (data.traits != null)
        {
            activeTraits = data.traits;
        }
        baseAttackPower = attackPower;
        baseMoveSpeed = moveSpeed;
        baseDefense = defense;

        CombatStatSettings combat = GameManager.Instance != null ? GameManager.Instance.combatStats : null;
        critChance = CombatStatRules.CritChance(combat, data.job);
        baseEvasion = evasion = CombatStatRules.Evasion(combat, data.job);

        if (data.morale < 0f)
            Debug.Log($"😞 [{data.name}] 사기 하락 중 (능력치 {MoraleRules.Percent(data.morale)}% 감소)");
        else if (data.morale > 0f)
            Debug.Log($"🔥 [{data.name}] 사기 고조 (능력치 {MoraleRules.Percent(data.morale)}% 증가)");
        if (currentHp < maxHp)
            Debug.Log($"🩹 [{data.name}] 부상이 낫지 않은 채 출발 (HP {currentHp:F0}/{maxHp:F0}, 요양 {data.recoveryWeeks}주 남음)");
    }

    // 최대 체력과 공격력을 같은 비율로 올림 (파티 케미 보너스). Initialize 직후에 부름
    public void ApplyStatBonus(float multiplier)
    {
        maxHp *= multiplier;
        currentHp *= multiplier; // 남은 체력 비율은 그대로 (다친 채 출발하는 부상자도 같은 비율)
        attackPower *= multiplier;
        baseAttackPower = attackPower;
    }

    // 던전에서 돌아올 때 모험가 데이터에 적을 체력: 출발 시 최대 체력(사기·케미 적용) 대비 남은 비율을 원래 최대 체력에 옮김
    public int GetReturnHp(Adventurer data)
    {
        if (data == null || isDead || maxHp <= 0f) return 0;
        float ratio = Mathf.Clamp01(currentHp / maxHp);
        return Mathf.Clamp(Mathf.RoundToInt(data.hp * ratio), currentHp > 0f ? 1 : 0, data.hp);
    }

    // 의뢰 랭크에 따른 능력치 배율 (적 전용). 소환 직후, Start()가 돌기 전에 불러야 함
    private float rankHpMultiplier = 1f;
    private float rankAttackMultiplier = 1f;

    public void SetRankScaling(float hpMultiplier, float attackMultiplier)
    {
        rankHpMultiplier = hpMultiplier;
        rankAttackMultiplier = attackMultiplier;
    }

    // 랭크 배율 위에 체력 배율을 더 곱함 (보스용). SetRankScaling 뒤, Start()가 돌기 전에 불러야 함
    public void MultiplyHpScaling(float multiplier)
    {
        rankHpMultiplier *= multiplier;
    }

    protected virtual void Start()
    {
        // 하위 클래스 Start()가 기본 스탯을 넣은 뒤이므로 여기서 랭크 배율을 곱함
        // (아군과 분열체는 배율이 1이라 영향 없음. 분열체는 원본의 배율 적용된 스탯을 물려받음)
        maxHp *= rankHpMultiplier;
        attackPower *= rankAttackMultiplier;

        if (currentHp <= 0) currentHp = maxHp;
        if (stunOnStart > 0f) Stun(stunOnStart);

        // Initialize()를 거치지 않는 적 유닛은 여기서 baseAttackPower 초기화
        // (아군은 Initialize()에서 이미 설정되므로 덮어쓰지 않음)
        if (!isInitialized)
        {
            baseAttackPower = attackPower;
            baseMoveSpeed = moveSpeed;
            baseDefense = defense;
        }

        anim = GetComponent<Animator>();

        // ★ 구형 AI 대신 새로운 통합 타겟 찾기 실행
        SearchForTarget();

        CreateHPBar();
    }

    protected virtual void Update()
    {
        if (hitFeedback != null) hitFeedback.RemoveShakeOffset(); // 흔들림은 화면용이라 판정 전에 실제 위치로 되돌림
        if (isDead) return;

        KeepOnFloor();

        TickBleed();
        if (isDead) return;

        // 모닥불 휴식: 정해진 자리로 걸어가 쉼 (전투 로직은 돌지 않음)
        if (isResting)
        {
            if ((transform.position - restPosition).sqrMagnitude > 0.0001f)
                WalkAroundPits(restPosition, 1f);
            else if (hasRestLook)
            {
                // 자리에 앉으면 모닥불 쪽을 바라봄
                SetFacing(restLookAt.x < transform.position.x);
                MonsterAnimator anim = GetComponent<MonsterAnimator>();
                if (anim != null) anim.Face(restLookAt);
            }

            // 휴식으로 회복한 체력이 바에 보이도록
            if (myHPBar != null) myHPBar.UpdateBar(currentHp, maxHp, currentMp, maxMp);
            return;
        }

        // 넉백(경직): 밀려나는 동안에는 움직이거나 공격하지 않고, 공격 쿨타임도 멈춤
        if (knockbackTimeLeft > 0f)
        {
            float dt = Mathf.Min(Time.deltaTime, knockbackTimeLeft);
            Vector3 pushedTo = transform.position + knockbackVelocity * dt;
            if (MapGenerator.Instance == null || MapGenerator.Instance.IsOnFloor(pushedTo)) transform.position = pushedTo; // 바닥 끝에서는 더 안 밀림
            knockbackTimeLeft -= dt;
            lastAttackTime += dt; // 경직된 시간만큼 다음 공격이 늦춰짐
            if (myHPBar != null) myHPBar.UpdateBar(currentHp, maxHp, currentMp, maxMp);
            return;
        }

        // 스턴: 그 자리에 멈춤, 공격 쿨타임도 멈춤
        if (stunTimeLeft > 0f)
        {
            float dt = Mathf.Min(Time.deltaTime, stunTimeLeft);
            stunTimeLeft -= dt;
            lastAttackTime += dt;
            if (myHPBar != null) myHPBar.UpdateBar(currentHp, maxHp, currentMp, maxMp);
            return;
        }

        // 게으른 성격: 잠깐 행동을 멈춤 (스턴처럼 멈추지만 강인함의 영향은 받지 않음)
        if (idleTimeLeft > 0f)
        {
            idleTimeLeft -= Time.deltaTime;
            if (myHPBar != null) myHPBar.UpdateBar(currentHp, maxHp, currentMp, maxMp);
            return;
        }

        // ★ [추가된 파밍 AI] 상자를 향해 걸어가는 상태라면?
        if (isLooting && chestTarget != null)
        {
            // 상자 쪽으로 뚜벅뚜벅 걷기 (구멍은 돌아서)
            WalkAroundPits(chestTarget.position, 1f);
            
            return; // 파밍 중일 때는 아래의 전투 로직(CheckTraits, FindTarget 등)을 아예 무시합니다!
        }

        // 어떤 이유로든 바닥 밖에 있으면 먼저 가장 가까운 바닥으로 돌아옴
        if (ReturnToFloor()) return;

        CheckTraits(); // 실시간 특성 체크

        // 1. UI 갱신
        if (myHPBar != null)
        {
            myHPBar.UpdateBar(currentHp, maxHp, currentMp, maxMp);
        }

        // 2. 타겟이 없거나 죽었으면 다시 찾기 (새로운 타겟팅 함수 호출). 노리던 상대가 숨었어도 놓침
        if (target == null || target.currentHp <= 0 || (target.IsHidden && !target.CompareTag(gameObject.tag)))
        {
            SearchForTarget();
            return; 
        }

        // 3. 겁쟁이 상태면 숨기만 하고 전투하지 않음
        if (isCoward && target != this)
        {
            MoveToTarget();
            return;
        }

        // 3-1. 같은 편을 대상으로 잡고 있으면 공격하지 않음 (겁쟁이가 혼자 남아 자기 자신을 대상으로 잡은 경우 등)
        //      사제처럼 아군에게 다가가 행동하는 직업만 예외
        if (!CanActOnAllies && target.CompareTag(gameObject.tag))
        {
            if (target != this) MoveToTarget();
            return;
        }

        // 4. 거리 계산 및 이동/공격 판별
        float distance = Vector3.Distance(transform.position, target.transform.position);

        // 전투 성향에 따른 이동(물러나기·도망·우회)이 이번 프레임을 차지하면 공격하지 않음
        if (UpdateCombatRole(distance)) return;

        // 사거리 안이거나, 서로 몸이 닿았으면 공격 (충돌 박스 때문에 사거리까지 못 좁히고 밀기만 하는 것 방지)
        // 근접 공격은 사이에 구멍이 있으면 못 침 → 돌아서 다가감
        if (CanStrike(target, distance))
        {
            StopAndAttack();
        }
        else
        {
            MoveToTarget();
        }
    }

    // 물리 충돌(유닛끼리 밀기)은 바닥 판정을 거치지 않아서 가장자리 밖으로 밀려날 수 있음
    // → 매 프레임 처음에 확인해서, 밀려났으면 마지막으로 바닥 위에 있던 자리로 되돌림
    private Vector3 lastFloorPosition;
    private bool hasFloorPosition;

    void KeepOnFloor()
    {
        MapGenerator map = MapGenerator.Instance;
        if (map == null) return;
        if (map.IsOnFloor(transform.position))
        {
            lastFloorPosition = transform.position;
            hasFloorPosition = true;
        }
        else if (hasFloorPosition && map.IsOnFloor(lastFloorPosition) && (lastFloorPosition - transform.position).sqrMagnitude < 1f)
        {
            transform.position = lastFloorPosition; // 조금 밀려난 것만 되돌림 (방 이동처럼 크게 옮겨진 경우는 아래 ReturnToFloor가 처리)
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = lastFloorPosition; rb.linearVelocity = Vector2.zero; }
        }
    }

    // 바닥 밖이면 가장 가까운 바닥 쪽으로 한 걸음 (돌아오는 중이면 true → 이번 프레임은 전투 행동 안 함)
    bool ReturnToFloor()
    {
        MapGenerator map = MapGenerator.Instance;
        if (map == null || map.IsOnFloor(transform.position)) return false;
        Vector3 floor = map.NearestFloor(transform.position);
        SetFacing(floor.x < transform.position.x);
        transform.position = Vector3.MoveTowards(transform.position, floor, Mathf.Max(moveSpeed, 2f) * Time.deltaTime);
        return true;
    }

    private HitFeedback hitFeedback; // 맞았을 때 번쩍임/흔들림, 공격할 때 내딛기 (처음 필요할 때 붙임)
    [HideInInspector] public bool isBoss; // 보스방의 보스 (쓰러질 때 연출이 더 큼)

    HitFeedback Feedback
    {
        get
        {
            if (hitFeedback == null) hitFeedback = GetComponent<HitFeedback>();
            if (hitFeedback == null) hitFeedback = gameObject.AddComponent<HitFeedback>();
            return hitFeedback;
        }
    }

    static HitFeedbackSettings FeedbackSettings => BattleManager.Instance != null ? BattleManager.Instance.hitFeedback : null;

    private Vector3 knockbackVelocity;
    private float knockbackTimeLeft;

    // direction 쪽으로 distance만큼 duration초 동안 밀려남 (이미 밀려나는 중이면 새로 덮어씀)
    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (isDead || isResting || distance <= 0f || duration <= 0f) return;
        direction.z = 0f;
        if (direction.sqrMagnitude < 0.000001f) return;
        knockbackVelocity = direction.normalized * (distance / duration);
        knockbackTimeLeft = duration;
    }

    // ─── 전투 성향 이동 ───────────────────────────────────────────────
    // 지금 전투에 있는 유닛 목록 (매 프레임 태그로 찾지 않도록)
    public static readonly List<BattleUnit> Active = new List<BattleUnit>();
    protected virtual void OnEnable()  { if (!Active.Contains(this)) Active.Add(this); }
    protected virtual void OnDisable() { Active.Remove(this); }

    // 은신 등으로 지금 대상으로 잡을 수 없는 상태인가 (숨은 유닛은 누구도 새로 노리지 않음)
    public virtual bool IsHidden => false;

    // 지금 모든 피해(일반 공격·범위 공격·출혈·화상)를 받지 않는 상태인가
    public virtual bool IsDamageImmune => false;

    // 방 전투가 시작될 때 BattleManager가 부름 (웨이브 방은 첫 웨이브에서만). 직업별 시작 효과용
    public virtual void OnRoomBattleStart() { }

    // 나를 노리던 상대들이 대상을 잃게 함 (다음 프레임에 다른 대상을 찾음)
    protected void DropAggro()
    {
        foreach (BattleUnit u in Active)
            if (u != null && u != this && u.target == this) u.target = null;
    }

    // 이 유닛의 전투 성향 (직업·몬스터 스크립트에서 정함)
    public virtual CombatRole Role => CombatRole.Charger;

    static readonly CombatMovementSettings defaultMovement = new CombatMovementSettings();
    static CombatMovementSettings Movement => BattleManager.Instance != null && BattleManager.Instance.combatMovement != null
                                              ? BattleManager.Instance.combatMovement : defaultMovement;

    private bool retreating, fleeing;   // 원거리 물러나기 / 지원 도망 중 (떨림 방지용 상태)
    private float flankTimer;           // 우회 시작 뒤 흐른 시간
    private BattleUnit flankTarget;
    private float nextDecisionTime;

    bool IsOpponent(BattleUnit u) => u != null && u != this && !u.isDead && !u.IsHidden && !u.CompareTag(gameObject.tag) && (u.CompareTag("Player") || u.CompareTag("Enemy"));
    bool IsFriend(BattleUnit u)   => u != null && u != this && !u.isDead && u.CompareTag(gameObject.tag);

    BattleUnit FindNearest(List<BattleUnit> list, System.Predicate<BattleUnit> match)
    {
        BattleUnit best = null; float bestDist = Mathf.Infinity;
        foreach (BattleUnit u in list)
        {
            if (u == null || u.isDead || u.IsHidden || !match(u)) continue;
            float d = (u.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = u; }
        }
        return best;
    }

    BattleUnit NearestOpponent(out float distance)
    {
        BattleUnit best = null; distance = Mathf.Infinity;
        foreach (BattleUnit u in Active)
        {
            if (!IsOpponent(u)) continue;
            float d = Vector2.Distance(transform.position, u.transform.position);
            if (d < distance) { distance = d; best = u; }
        }
        return best;
    }

    // 성향에 따라 이번 프레임에 할 이동. true면 이동했으므로 공격하지 않음
    bool UpdateCombatRole(float distanceToTarget)
    {
        CombatMovementSettings s = Movement;
        if (!s.useRoles || isCoward || target == null) return false;

        if (Time.time >= nextDecisionTime)
        {
            nextDecisionTime = Time.time + s.decisionInterval * Random.Range(0.8f, 1.2f);
            Rethink(s);
            if (target == null) return false;
        }

        switch (Role)
        {
            case CombatRole.Ranged:
            {
                NearestOpponent(out float near);
                retreating = CombatPositioning.ShouldRetreat(s, near, attackRange, retreating);
                if (retreating && StepAwayFromNearest(s.rangedRetreatSpeed)) return true;
                retreating = false; // 막혀서 못 물러나면 그 자리에서 싸움
                return false;
            }
            case CombatRole.Support:
            {
                NearestOpponent(out float near);
                fleeing = CombatPositioning.ShouldFlee(s, near, fleeing);
                if (!fleeing) return false;
                switch (StepTowardShelter(s))
                {
                    case ShelterResult.Moving:  return true;
                    case ShelterResult.Arrived: fleeing = false; return false; // 전열 뒤에 숨었으면 다시 회복
                }
                if (StepAwayFromNearest(1f)) return true;
                fleeing = false;
                return false;
            }
            case CombatRole.Flanker:
            {
                // 후열을 노릴 때는 곧장 감. 후열이 아닌 대상이면 등 뒤로 돌아간 뒤 공격
                if (CombatPositioning.IsBackline(target.Role) || target.CompareTag(gameObject.tag)) return false;
                if (flankTarget != target) { flankTarget = target; flankTimer = 0f; }
                flankTimer += Time.deltaTime;
                if (flankTimer > s.flankTimeout) return false; // 오래 걸리면 정면에서 공격

                Vector2 me = transform.position, t = target.transform.position;
                Vector2 back = CombatPositioning.BackDirection(t, TargetFacingPoint(target));
                bool behind = CombatPositioning.IsBehind(me, t, back);
                if (behind && CanStrike(target, distanceToTarget)) return false; // 등 뒤에서 공격

                if (!MoveStep(CombatPositioning.FlankPoint(me, t, back, s.flankOffset), 1f)) flankTimer = s.flankTimeout + 1f; // 막히면 정면으로
                return true;
            }
            default:
                return false;
        }
    }

    // 주기적인 판단: 대상 다시 고르기
    void Rethink(CombatMovementSettings s)
    {
        if (target != null && target.CompareTag(gameObject.tag)) return; // 아군을 대상으로 잡은 사제 등은 그대로
        if (chargeLocked) return; // 다혈질 돌진·분노 중에는 대상을 바꾸지 않음

        // 연인을 노리는 적을 먼저
        BattleUnit lover = LoverInBattle();
        if (lover != null)
        {
            if (target != null && target.target == lover && IsOpponent(target)) return;
            BattleUnit threat = FindNearest(Active, u => IsOpponent(u) && u.target == lover);
            if (threat != null) { target = threat; return; }
        }

        // 이타적인 성격: 아군에게 붙은 적이 있으면 그쪽부터
        if (HasPersonality(TraitType.Altruistic))
        {
            if (target != null && IsAttackingFriend(target)) return;
            BattleUnit bully = FindNearest(Active, IsAttackingFriend);
            if (bully != null) { target = bully; return; }
        }

        // 나르시시스트: 막타를 칠 수 있는 적이 보이면 그쪽으로
        if (HasPersonality(TraitType.Narcissist))
        {
            if (target != null && CanFinish(target)) return;
            BattleUnit prey = FindNearest(Active, CanFinish);
            if (prey != null) { target = prey; return; }
        }

        if (Role == CombatRole.Flanker)
        {
            // 상대 후열이 새로 보이면 그쪽으로 바꿈
            if (target != null && CombatPositioning.IsBackline(target.Role)) return;
            BattleUnit backline = FindNearest(Active, u => IsOpponent(u) && CombatPositioning.IsBackline(u.Role));
            if (backline != null) target = backline;
        }
        else if (s.backlineDefense && (Role == CombatRole.Guardian || Role == CombatRole.Ranged))
        {
            // 후열 방어: 수호는 막으러 가고, 원거리는 먼저 쏨
            if (target != null && IsBacklineThreat(target, s)) return; // 이미 위협을 상대하는 중
            BattleUnit threat = FindNearest(Active, u => IsOpponent(u) && IsBacklineThreat(u, s));
            if (threat != null) target = threat;
        }
        else if (Role == CombatRole.Guardian)
        {
            // 우리 후열에 가장 가까이 붙은 적을 먼저 막음
            BattleUnit threat = null; float best = s.guardRadius;
            foreach (BattleUnit friend in Active)
            {
                if (!IsFriend(friend) || !CombatPositioning.IsBackline(friend.Role)) continue;
                foreach (BattleUnit enemy in Active)
                {
                    if (!IsOpponent(enemy)) continue;
                    float d = Vector2.Distance(friend.transform.position, enemy.transform.position);
                    if (d < best) { best = d; threat = enemy; }
                }
            }
            if (threat != null) target = threat;
        }
    }

    // 후열 위협: 우리 후열(원거리·지원, 나 자신 포함)을 대상으로 잡았거나, 우리 후열에 수호 반경 안으로 붙은 상대
    bool IsBacklineThreat(BattleUnit enemy, CombatMovementSettings s)
    {
        if (!IsOpponent(enemy)) return false;
        BattleUnit victim = enemy.target;
        if (victim != null && !victim.isDead && (victim == this || IsFriend(victim)) && CombatPositioning.IsBackline(victim.Role)) return true;

        foreach (BattleUnit friend in Active)
        {
            if (!(friend == this || IsFriend(friend)) || !CombatPositioning.IsBackline(friend.Role)) continue;
            if (Vector2.Distance(friend.transform.position, enemy.transform.position) < s.guardRadius) return true;
        }
        return false;
    }

    // 대상이 바라보는 지점: 대상의 대상, 없으면 우리 편 중심
    Vector2 TargetFacingPoint(BattleUnit t)
    {
        if (t.target != null && !t.target.isDead) return t.target.transform.position;
        Vector2 sum = transform.position; int n = 1;
        foreach (BattleUnit u in Active) if (IsFriend(u)) { sum += (Vector2)u.transform.position; n++; }
        return sum / n;
    }

    enum ShelterResult { None, Moving, Arrived }

    // 지원: 전열 아군(수호 우선, 없으면 가장 가까운 전열) 뒤로 피함. 피할 곳이 없거나 적 쪽이면 None
    ShelterResult StepTowardShelter(CombatMovementSettings s)
    {
        BattleUnit threat = NearestOpponent(out float _);
        if (threat == null) return ShelterResult.None;

        BattleUnit protector = FindNearest(Active, u => IsFriend(u) && u.Role == CombatRole.Guardian);
        if (protector == null) protector = FindNearest(Active, u => IsFriend(u) && !CombatPositioning.IsBackline(u.Role));
        if (protector == null) return ShelterResult.None;

        Vector2 me = transform.position, threatPos = threat.transform.position;
        Vector2 shelter = CombatPositioning.ShelterPoint(protector.transform.position, threatPos, s.supportShelterOffset);
        if (MapGenerator.Instance != null && !MapGenerator.Instance.IsOnFloor(shelter)) shelter = protector.transform.position;
        if (CombatPositioning.ShelterTowardThreat(me, shelter, threatPos, s.supportShelterMaxAlign)) return ShelterResult.None;
        if ((shelter - me).sqrMagnitude < 0.04f) return ShelterResult.Arrived;

        if (!MoveStep(shelter, 1f)) return ShelterResult.None;
        SetFacing(threatPos.x < transform.position.x);
        return ShelterResult.Moving;
    }

    // 가장 가까운 적 반대쪽으로 한 걸음. 바닥 밖이면 방향을 틀어 보고, 다 막히면 false
    bool StepAwayFromNearest(float speedMultiplier)
    {
        BattleUnit threat = NearestOpponent(out float _);
        if (threat == null) return false;
        float step = Mathf.Max(moveSpeed * speedMultiplier * Time.deltaTime, 0.3f);
        foreach (Vector2 p in CombatPositioning.AwayCandidates(transform.position, threat.transform.position, step))
        {
            if (MapGenerator.Instance != null && !MapGenerator.Instance.IsOnFloor(p)) continue;
            if (!MoveStep(p, speedMultiplier)) continue;
            SetFacing(threat.transform.position.x < transform.position.x); // 물러나면서도 적을 바라봄
            return true;
        }
        return false;
    }

    // 목적지 쪽으로 한 프레임 이동 + 같은 편과 겹치지 않게 밀기 + 바닥 밖으로는 안 감. 못 움직였으면 false
    protected bool MoveStep(Vector3 destination, float speedMultiplier)
    {
        CombatMovementSettings s = Movement;
        destination = SteerAroundPits(destination); // 사이에 구멍이 있으면 돌아가는 경로의 다음 경유지로
        Vector3 next = Vector3.MoveTowards(transform.position, destination, moveSpeed * speedMultiplier * Time.deltaTime);

        Vector2 push = Vector2.zero;
        if (s.separationStrength > 0f)
        {
            List<Vector2> friends = new List<Vector2>();
            foreach (BattleUnit u in Active) if (IsFriend(u)) friends.Add(u.transform.position);
            push = CombatPositioning.Separation(transform.position, friends, s.separationRadius) * s.separationStrength * Time.deltaTime;
        }
        Vector3 pushed = next + new Vector3(push.x, push.y, 0f);

        MapGenerator map = MapGenerator.Instance;
        if (map == null || map.IsOnFloor(pushed)) next = pushed;
        else if (!map.IsOnFloor(next))
        {
            // 바닥 밖으로는 안 감. 이미 바닥 밖이면 바닥에 가까워지는 쪽으로만 움직임
            if (map.IsOnFloor(transform.position))
            {
                // 구덩이·오목한 벽에 막히면 목적지 쪽에 가까운 방향부터 비스듬히 돌아가 봄
                if (!TrySlide(map, destination, speedMultiplier, out next)) return false;
            }
            else
            {
                Vector3 floor = map.NearestFloor(transform.position);
                if ((floor - next).sqrMagnitude >= (floor - transform.position).sqrMagnitude) return false;
            }
        }

        SetFacing(destination.x < transform.position.x);
        transform.position = next;
        return true;
    }

    // ─── 구멍 돌아가기 (바닥 길찾기) ─────────────────────────────
    // 목적지까지 곧게 가면 구멍(바닥 밖)을 밟게 되면 MapGenerator.FindPath로 바닥만 지나는 경로를 받아 경유지를 차례로 따라감.
    // 목적지가 크게 바뀌거나 일정 시간이 지나면 경로를 다시 구함 (목표가 움직이므로)

    [Header("구멍 돌아가기")]
    [Tooltip("경로를 다시 구하는 간격(초)")]
    public float pathRefreshInterval = 0.3f;
    [Tooltip("목적지가 이만큼(월드 단위) 넘게 바뀌면 바로 경로를 다시 구함")]
    public float pathRepathDistance = 0.6f;
    [Tooltip("경유지에 이만큼 가까워지면 다음 경유지로")]
    public float waypointReachDistance = 0.15f;

    private readonly List<Vector3> pathWaypoints = new List<Vector3>();
    private int pathIndex;
    private Vector3 pathGoal;
    private float nextPathTime;
    private bool followingPath;

    Vector3 SteerAroundPits(Vector3 destination)
    {
        MapGenerator map = MapGenerator.Instance;
        if (map == null) return destination;
        Vector3 pos = transform.position;

        bool goalMoved = (destination - pathGoal).sqrMagnitude > pathRepathDistance * pathRepathDistance;
        if (Time.time >= nextPathTime || goalMoved)
        {
            nextPathTime = Time.time + pathRefreshInterval;
            pathGoal = destination;
            followingPath = !map.IsLineClear(pos, destination) && map.FindPath(pos, destination, pathWaypoints);
            pathIndex = 0;
        }
        if (!followingPath || pathWaypoints.Count == 0) return destination;

        // 가까워진 경유지는 넘기고, 곧게 닿는 더 먼 경유지가 있으면 그쪽으로 (밀려서 경로에서 벗어났을 때도 자연스럽게)
        while (pathIndex < pathWaypoints.Count - 1 && (pathWaypoints[pathIndex] - pos).sqrMagnitude < waypointReachDistance * waypointReachDistance)
            pathIndex++;
        if (pathIndex >= pathWaypoints.Count - 1)
        {
            // 마지막 구간: 목적지가 바닥이고 곧게 닿으면 실제 목적지로 (움직이는 목표를 놓치지 않게)
            return map.IsOnFloor(destination) && map.IsLineClear(pos, destination) ? destination : pathWaypoints[pathWaypoints.Count - 1];
        }
        return pathWaypoints[pathIndex];
    }

    // 전투 외 이동(상자 줍기·모닥불 자리): 같은 편 밀기 없이 구멍만 돌아서 걸음
    void WalkAroundPits(Vector3 destination, float speedMultiplier)
    {
        Vector3 waypoint = SteerAroundPits(destination);
        Vector3 next = Vector3.MoveTowards(transform.position, waypoint, moveSpeed * speedMultiplier * Time.deltaTime);
        MapGenerator map = MapGenerator.Instance;
        if (map != null && map.IsOnFloor(transform.position) && !map.IsOnFloor(next))
        {
            if (!TrySlide(map, waypoint, speedMultiplier, out next)) return; // 막혔으면 다음 경로 갱신까지 대기
        }
        SetFacing(waypoint.x < transform.position.x);
        transform.position = next;
    }

    static readonly float[] SlideAngles = { 35f, -35f, 70f, -70f, 100f, -100f };

    bool TrySlide(MapGenerator map, Vector3 destination, float speedMultiplier, out Vector3 next)
    {
        next = transform.position;
        Vector2 dir = destination - transform.position;
        if (dir.sqrMagnitude < 1e-6f) return false;
        dir.Normalize();
        float step = moveSpeed * speedMultiplier * Time.deltaTime;
        foreach (float a in SlideAngles)
        {
            Vector2 d = Quaternion.Euler(0f, 0f, a) * dir;
            Vector3 cand = transform.position + new Vector3(d.x, d.y, 0f) * step;
            if (!map.IsOnFloor(cand)) continue;
            next = cand;
            return true;
        }
        return false;
    }

    // 같은 편을 대상으로 잡고 행동할 수 있는 직업인가 (기본: 불가)
    protected virtual bool CanActOnAllies => false;

    [Header("근접 판정")]
    [Tooltip("충돌 박스 사이 간격이 이 값 이하면 몸이 닿은 것으로 보고, 사거리가 모자라도 공격할 수 있음")]
    public float contactTolerance = 0.05f;

    private Collider2D bodyCollider;
    private bool bodyColliderCached;

    Collider2D BodyCollider
    {
        get
        {
            if (!bodyColliderCached)
            {
                bodyCollider = GetComponent<Collider2D>();
                bodyColliderCached = true;
            }
            return bodyCollider;
        }
    }

    // 대상과 충돌 박스가 맞닿아 있는지
    // 근접 공격을 하는 유닛인가 (원거리·지원 성향이 아니면 근접). 필요하면 하위 클래스에서 바꿈
    protected virtual bool IsMeleeAttacker => Role != CombatRole.Ranged && Role != CombatRole.Support;

    [Tooltip("근접 공격의 구멍 판정에서 두 유닛 발밑 근처는 이 거리만큼 빼고 봄 (가장자리에 선 유닛끼리 못 치는 것 방지)")]
    public float meleeGapEndMargin = 0.25f;

    // 지금 대상을 칠 수 있는가: 사거리 안(또는 몸이 닿음) + 근접이면 사이가 바닥으로 이어져 있어야 함
    bool CanStrike(BattleUnit target, float distance)
    {
        if (target == null) return false;
        if (!(distance <= attackRange || IsTouching(target))) return false;
        if (!IsMeleeAttacker || target == this) return true;
        MapGenerator map = MapGenerator.Instance;
        return map == null || map.IsLineClear(transform.position, target.transform.position, meleeGapEndMargin);
    }

    bool IsTouching(BattleUnit other)
    {
        if (other == null || other == this) return false;

        Collider2D mine = BodyCollider, theirs = other.BodyCollider;
        if (mine == null || theirs == null) return false;

        return mine.Distance(theirs).distance <= contactTolerance;
    }

    // ★ [추가] 배틀 매니저에서 적 리스트를 가져와 새로운 FindTarget에 넘겨주는 연결 함수
    protected void SearchForTarget()
    {
        // 내가 플레이어면 "Enemy" 태그를 찾고, 적이면 "Player" 태그를 찾음
        string targetTag = gameObject.CompareTag("Player") ? "Enemy" : "Player";
        GameObject[] targetObjects = GameObject.FindGameObjectsWithTag(targetTag);

        List<BattleUnit> targetList = new List<BattleUnit>();
        foreach (GameObject obj in targetObjects)
        {
            BattleUnit unit = obj.GetComponent<BattleUnit>();
            if (unit != null) targetList.Add(unit);
        }

        FindTarget(targetList);
    }

    // ★ 통합된 새로운 타겟 탐색 함수 (오직 이 녀석 하나만 존재해야 합니다!)
    protected virtual void FindTarget(List<BattleUnit> enemyList)
    {
        // 1. [특성] 내가 '겁쟁이'라면? 적을 찾지 않고 튼튼한 아군 뒤로 숨는다!
        if (isCoward)
        {
            target = FindStrongestAlly();
            return; 
        }

        // 2. [기본 AI] 적 탐색 (어그로 시스템 적용)
        chargeLocked = false;
        if (enemyList == null || enemyList.Count == 0) return;

        // 다혈질 성격: 가끔 어그로와 성향을 무시하고 가장 가까운 적에게 돌진
        if (HasPersonality(TraitType.HotTempered) && Random.value < Personality.hotTemperedChargeChance)
        {
            BattleUnit nearest = FindNearest(enemyList, u => u.currentHp > 0);
            if (nearest != null)
            {
                target = nearest;
                chargeLocked = true;
                if (BattleManager.Instance != null) BattleManager.Instance.ShowStatusText(transform.position, "!");
                return;
            }
        }

        // 연인을 노리는 적을 먼저
        BattleUnit myLover = LoverInBattle();
        if (myLover != null)
        {
            BattleUnit threat = FindNearest(enemyList, u => u.currentHp > 0 && u.target == myLover);
            if (threat != null) { target = threat; return; }
        }

        // 이타적인 성격: 아군에게 붙은 적을 먼저
        if (HasPersonality(TraitType.Altruistic))
        {
            BattleUnit bully = FindNearest(enemyList, IsAttackingFriend);
            if (bully != null) { target = bully; return; }
        }

        // 나르시시스트: 막타를 칠 수 있는 적을 먼저
        if (HasPersonality(TraitType.Narcissist))
        {
            BattleUnit prey = FindNearest(enemyList, CanFinish);
            if (prey != null) { target = prey; return; }
        }

        float bestScore = Mathf.Infinity;
        BattleUnit bestTarget = null;

        // 우회 성향: 상대 후열(원거리·지원)이 있으면 어그로와 상관없이 가장 가까운 후열부터
        if (Role == CombatRole.Flanker && Movement.useRoles)
        {
            BattleUnit backline = FindNearest(enemyList, u => CombatPositioning.IsBackline(u.Role));
            if (backline != null) { target = backline; return; }
        }

        foreach (BattleUnit enemy in enemyList)
        {
            if (enemy == null || enemy.currentHp <= 0 || enemy.IsHidden) continue;

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            float score = distance - enemy.aggro; // 어그로 적용

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }
        target = bestTarget;
    }

    // -------------------------------------------------------------
    // 아래는 이동, 공격, 특성 등 기능 함수들입니다.
    // -------------------------------------------------------------

    private System.Action<TraitEffectType, float> applyEffect;

    private void CheckTraits()
    {
        // 트레이트가 있을 때만 기본값으로 리셋
        // (트레이트 없는 적 유닛은 attackPower를 Start()에서 설정한 값 그대로 유지)
        if ((activeTraits != null && activeTraits.Count > 0) || SourceData != null)
        {
            attackPower = baseAttackPower;
            moveSpeed = baseMoveSpeed;
            defense = baseDefense;
            evasion = baseEvasion;
            evasionMultiplier = 1f;
            damageTakenMultiplier = 1f;
            bleedTakenMultiplier = 1f;
            stunTakenMultiplier = 1f;
        }
        isCoward = false;

        if (activeTraits != null)
        {
            if (applyEffect == null) applyEffect = ApplyTraitEffect;
            foreach (TraitData trait in activeTraits)
            {
                // 대상 계열 조건(사냥꾼·공포증)은 피해를 줄 때 GetDamageDealtMultiplier에서 판정
                if (trait == null || trait.conditionType == TraitConditionType.TargetIsFamily) continue;

                bool conditionMet = IsConditionMet(trait);
                if (conditionMet || trait.hasElseEffect) trait.ForEachEffect(!conditionMet, applyEffect);
            }
        }

        UpdatePersonality();

        // 라이벌이 같은 파티에 있으면 지지 않으려 공격력 상승
        if (HasRivalInParty()) attackPower *= Relationship.rivalAttackMultiplier;
    }

    // 대상과 상관없는 특성 조건 판정
    private bool IsConditionMet(TraitData trait)
    {
        switch (trait.conditionType)
        {
            case TraitConditionType.Always:
                return true;
            case TraitConditionType.NoAllyNearby:
                return CheckNoAllyNearby(trait.conditionValue);
            case TraitConditionType.HpBelowPercentage:
                return maxHp > 0 && (currentHp / maxHp) <= trait.conditionValue;
            case TraitConditionType.SpecificEnemyExists:
                return GameObject.FindGameObjectWithTag(trait.conditionTag) != null;
            case TraitConditionType.AllyCountBelow:
                return GameObject.FindGameObjectsWithTag(gameObject.tag).Length <= (int)trait.conditionValue;
            case TraitConditionType.AllyRatioAtLeast:
                BattleManager bm = BattleManager.Instance;
                return bm == null || bm.startAllyCount <= 0
                       || (float)bm.allyCount / bm.startAllyCount >= trait.conditionValue;
            default:
                return false;
        }
    }

    private bool CheckNoAllyNearby(float radius)
    {
        string myTag = gameObject.tag; // 나와 같은 편 태그
        GameObject[] allyObjects = GameObject.FindGameObjectsWithTag(myTag);

        foreach (GameObject obj in allyObjects)
        {
            BattleUnit ally = obj.GetComponent<BattleUnit>();
            if (ally == null || ally.isDead || ally == this) continue;

            float dist = Vector2.Distance(transform.position, ally.transform.position);
            if (dist <= radius) return false; // 반경 내에 아군 발견!
        }
        return true;
    }

    // 여러 특성의 배율은 곱해짐 (CheckTraits가 매번 기본값으로 되돌린 뒤 차례로 곱함). 예: 1.2 × 0.7 = 0.84
    // 회피율만 더함 (빠른 발 +0.1, 외눈박이 -0.3)
    private void ApplyTraitEffect(TraitEffectType type, float value)
    {
        switch (type)
        {
            case TraitEffectType.ModifyAttackPower:
                attackPower *= value;
                break;
            case TraitEffectType.ModifyMoveSpeed:
                moveSpeed *= value;
                break;
            case TraitEffectType.ChangeAI_HideBehindAlly:
                isCoward = true;
                break;
            case TraitEffectType.ModifyAllStats:
                attackPower *= value;
                defense *= value;
                moveSpeed *= value;
                break;
            case TraitEffectType.ModifyDefense:
                defense *= value;
                break;
            case TraitEffectType.ModifyEvasion:
                evasion += value;
                break;
            case TraitEffectType.MultiplyEvasion:
                evasionMultiplier *= value;
                break;
            case TraitEffectType.ModifyDamageTaken:
                damageTakenMultiplier *= value;
                break;
            case TraitEffectType.ModifyBleedTaken:
                bleedTakenMultiplier *= value;
                break;
            case TraitEffectType.ModifyStunTaken:
                stunTakenMultiplier *= value;
                break;
            case TraitEffectType.AttackByMissingHp:
                if (maxHp > 0f) attackPower *= 1f + value * (1f - Mathf.Clamp01(currentHp / maxHp));
                break;
            // ModifyDamageDealt는 피해를 줄 때(GetDamageDealtMultiplier), RevealNextRoom은 던전 지도에서 씀
        }
    }

    // 이 유닛이 victim에게 주는 피해 배율 (외팔이, 사냥꾼, 공포증 등. 곱해짐)
    public float GetDamageDealtMultiplier(BattleUnit victim)
    {
        float multiplier = 1f;
        if (activeTraits == null) return multiplier;

        foreach (TraitData trait in activeTraits)
        {
            if (trait == null || !trait.HasEffect(TraitEffectType.ModifyDamageDealt)) continue;

            bool met = trait.conditionType == TraitConditionType.TargetIsFamily
                ? victim != null && !string.IsNullOrEmpty(victim.Family) && victim.Family == trait.conditionTag
                : IsConditionMet(trait);
            if (!met && !trait.hasElseEffect) continue;

            trait.ForEachEffect(!met, (type, value) => { if (type == TraitEffectType.ModifyDamageDealt) multiplier *= value; });
        }
        return multiplier;
    }

    // ─── 관계 (아군만) ────────────────────────────────────────────────
    static readonly RelationshipSettings defaultRelationship = new RelationshipSettings();
    static readonly MoodSettings defaultMood = new MoodSettings();
    static RelationshipSettings Relationship => GameManager.Instance != null && GameManager.Instance.relationship != null
                                                ? GameManager.Instance.relationship : defaultRelationship;
    static MoodSettings Mood => GameManager.Instance != null && GameManager.Instance.moodSettings != null
                                ? GameManager.Instance.moodSettings : defaultMood;

    private int rivalChecked = -1; // -1: 아직 안 봄, 0: 없음, 1: 있음 (던전 동안 파티는 그대로이므로 한 번만 확인)

    RelationType RelationWith(BattleUnit other) =>
        SourceData != null && other != null && other.SourceData != null && GameManager.Instance != null
            ? GameManager.Instance.GetRelation(SourceData, other.SourceData) : RelationType.None;

    bool HasRivalInParty()
    {
        if (SourceData == null) return false;
        if (rivalChecked < 0)
        {
            rivalChecked = 0;
            foreach (BattleUnit u in Active)
                if (u != this && u.CompareTag(gameObject.tag) && RelationWith(u) == RelationType.Rival) { rivalChecked = 1; break; }
        }
        return rivalChecked == 1;
    }

    // 같은 던전에 살아 있는 연인
    BattleUnit LoverInBattle()
    {
        if (SourceData == null) return null;
        foreach (BattleUnit u in Active)
            if (IsFriend(u) && RelationWith(u) == RelationType.Lover) return u;
        return null;
    }

    // 말풍선 (:) / :()
    public void ShowEmote(bool happy) => MoodEmote.ShowWorld(transform, happy);

    // 내가 쓰러질 때: 관계가 있던 동료는 슬퍼하고(:(), 친우·연인·의형제는 나를 쓰러뜨린 적에게 분노
    void NotifyAlliesOfMyDeath()
    {
        if (SourceData == null) return;
        SourceData.lastKillerFamily = lastAttacker != null ? lastAttacker.Family : "";

        foreach (BattleUnit ally in Active)
        {
            if (!IsFriend(ally) || ally.SourceData == null) continue;
            RelationType rel = ally.RelationWith(this);
            if (rel == RelationType.None || rel == RelationType.Feud) continue;

            ally.ShowEmote(false);
            if (RelationshipRules.IsPositive(rel) && lastAttacker != null && !lastAttacker.isDead && !lastAttacker.IsHidden && !lastAttacker.CompareTag(ally.tag))
            {
                ally.target = lastAttacker;
                ally.chargeLocked = true; // 그 적을 쓰러뜨릴 때까지 대상 고정
                Debug.Log($"😡 [분노] {ally.name}이(가) {RelationshipRules.GetName(rel)} {SourceData.name}을(를) 쓰러뜨린 {lastAttacker.name}에게 분노합니다!");
            }
        }
    }

    // ─── 성격 (아군만) ────────────────────────────────────────────────
    // 성격은 스탯을 바꾸지 않고 행동에 관여함. 수치는 GameManager.personality

    static readonly PersonalitySettings defaultPersonality = new PersonalitySettings();
    static PersonalitySettings Personality => GameManager.Instance != null && GameManager.Instance.personality != null
                                              ? GameManager.Instance.personality : defaultPersonality;

    bool HasPersonality(TraitType type) => SourceData != null && SourceData.trait == type;

    private bool cowardHiding;          // 겁쟁이: 힐을 받을 때까지 아군 뒤에 숨는 중
    private bool cowardHpArmed = true;  // 체력이 중상 기준 위로 올라오면 다시 발동할 수 있음
    private bool cowardRatioFired;      // '아군 절반 이상 쓰러짐'은 던전에서 한 번만 발동
    private float idleTimeLeft;         // 게으른: 남은 멍때리기 시간
    private bool chargeLocked;          // 다혈질: 돌진 대상을 쓰러뜨릴 때까지 대상 고정

    void UpdatePersonality()
    {
        if (!HasPersonality(TraitType.Coward)) return;

        bool badlyHurt = maxHp > 0f && currentHp / maxHp <= Personality.cowardHpRatio;
        if (badlyHurt && cowardHpArmed) { cowardHpArmed = false; StartHiding("중상"); }
        else if (!badlyHurt) cowardHpArmed = true;

        BattleManager bm = BattleManager.Instance;
        bool halfDown = bm != null && bm.startAllyCount > 0 && (bm.startAllyCount - bm.allyCount) * 2 >= bm.startAllyCount;
        if (halfDown && !cowardRatioFired) { cowardRatioFired = true; StartHiding("아군 절반 이상 쓰러짐"); }

        if (cowardHiding) isCoward = true;
    }

    void StartHiding(string reason)
    {
        if (cowardHiding) return;
        cowardHiding = true;
        target = null; // 다음 프레임에 숨을 아군을 고름
        Debug.Log($"😨 [겁쟁이] {name}이(가) 아군 뒤로 숨습니다. ({reason})");
    }

    // 힐을 받았을 때 (사제 힐 등). 겁쟁이는 숨기를 그만둠
    public void NotifyHealed()
    {
        if (!cowardHiding) return;
        cowardHiding = false;
        target = null; // 다시 적을 고름
        Debug.Log($"💪 [겁쟁이] {name}이(가) 힐을 받고 다시 싸웁니다.");
    }

    // 내 공격 몇 번 안에 쓰러뜨릴 수 있는 적인가 (나르시시스트용)
    bool CanFinish(BattleUnit enemy) =>
        IsOpponent(enemy) && enemy.currentHp <= attackPower * Personality.narcissistFinishHits;

    // 이 적이 지금 내 동료(나 말고)를 노리고 있는가 (이타적인 성격용)
    bool IsAttackingFriend(BattleUnit enemy) =>
        IsOpponent(enemy) && enemy.target != null && IsFriend(enemy.target);

    private BattleUnit FindStrongestAlly()
    {
        BattleUnit strongest = null;
        float maxHealth = -1;

        string myTag = gameObject.tag; // 나와 같은 편 태그
        GameObject[] allyObjects = GameObject.FindGameObjectsWithTag(myTag);

        foreach (GameObject obj in allyObjects)
        {
            BattleUnit ally = obj.GetComponent<BattleUnit>();
            if (ally == null || ally.isDead || ally == this) continue;

            if (ally.maxHp > maxHealth)
            {
                maxHealth = ally.maxHp;
                strongest = ally;
            }
        }
        return strongest != null ? strongest : this;
    }

    protected virtual void MoveToTarget()
    {
        Vector3 destination = target.transform.position;

        // ★ [특성] 겁쟁이 AI: 가장 가까운 적의 반대 방향으로 아군 뒤에 숨기
        if (isCoward && target != this)
        {
            // 가장 가까운 적 찾기
            string enemyTag = gameObject.CompareTag("Player") ? "Enemy" : "Player";
            GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
            Vector3 nearestEnemyPos = target.transform.position + Vector3.right; // 기본값: 아군 오른쪽에 적이 있다고 가정

            float closestDist = Mathf.Infinity;
            foreach (GameObject obj in enemies)
            {
                BattleUnit enemy = obj.GetComponent<BattleUnit>();
                if (enemy == null || enemy.isDead) continue;
                float dist = Vector3.Distance(target.transform.position, enemy.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nearestEnemyPos = enemy.transform.position;
                }
            }

            // 적 → 아군 방향의 반대쪽(뒤쪽)으로 1.5만큼 떨어진 위치
            Vector3 awayDir = (target.transform.position - nearestEnemyPos).normalized;
            destination = target.transform.position + awayDir * 1.5f;
        }

        MoveStep(destination, 1f);
    }

    // 좌우 방향 전환. 프리팹에 설정된 크기는 그대로 두고 x 부호만 바꿈 (왼쪽을 볼 때 음수)
    private Vector3 baseScale;
    private bool baseScaleCaptured;

    protected void SetFacing(bool faceLeft)
    {
        if (!baseScaleCaptured)
        {
            Vector3 s = transform.localScale;
            baseScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), s.z);
            baseScaleCaptured = true;
        }
        transform.localScale = new Vector3(faceLeft ? -baseScale.x : baseScale.x, baseScale.y, baseScale.z);
    }

    protected void StopAndAttack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            // 게으른 성격 / 기분이 아주 나쁜 모험가: 가끔 공격하지 않고 멍하니 서 있음
            bool lazy = HasPersonality(TraitType.Lazy) && Random.value < Personality.lazyChance;
            bool gloomy = !lazy && SourceData != null && SourceData.mood <= Mood.lowMood && Random.value < Mood.lowMoodIdleChance;
            if (lazy || gloomy)
            {
                idleTimeLeft = lazy ? Personality.lazyDuration : Mood.lowMoodIdleDuration;
                lastAttackTime = Time.time;
                if (BattleManager.Instance != null) BattleManager.Instance.ShowStatusText(transform.position, "Zzz");
                return;
            }

            if (target != null && target != this && FeedbackSettings != null)
                Feedback.PlayLunge(FeedbackSettings, target.transform.position - transform.position);

            if (maxMp > 0f && currentMp >= maxMp) // 마나가 없는 유닛(maxMp 0)은 스킬 없음
            {
                SkillStarted?.Invoke();
                UseSkill();
            }
            else
            {
                AttackStarted?.Invoke();
                Attack();
            }
            lastAttackTime = Time.time;
        }
    }

    protected virtual void Attack()
    {
        if (target != null)
        {
            // 이 공격으로 대상이 죽으면 target이 비워질 수 있으므로 따로 잡아 둠
            BattleUnit victim = target;
            float damage = RollBasicAttack(attackPower, out bool crit);
            victim.TakeDamageFrom(this, damage, crit);
            Debug.Log($"⚔️ {name}의 공격! -> {victim.name} (HP: {victim.currentHp})");
        }

        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    protected virtual void UseSkill()
    {
        Debug.Log($"{name}의 스킬!");
        currentMp = 0; 
    }

    // 공격자를 알고 때릴 때: 공격자의 대상별 피해 배율(외팔이·사냥꾼·공포증)을 곱하고, 막타 판정용으로 공격자를 기억
    public void TakeDamageFrom(BattleUnit attacker, float damage, bool crit = false)
    {
        if (attacker != null)
        {
            damage *= attacker.GetDamageDealtMultiplier(this);
            lastAttacker = attacker;
        }
        nextHitCrit = crit;
        TakeDamage(damage);
        nextHitCrit = false;
    }

    private bool nextHitCrit; // 지금 받는 피해가 치명타인가 (피해 숫자 색만 바꿈)

    // 기본 공격 피해: critChance 확률로 치명타 (스킬에는 쓰지 않음)
    protected float RollBasicAttack(float damage, out bool crit)
    {
        crit = critChance > 0f && Random.value < critChance;
        return crit ? damage * CritMultiplier : damage;
    }

    // 치명타 배율 (도적은 자기 배율을 씀)
    protected virtual float CritMultiplier =>
        GameManager.Instance != null && GameManager.Instance.combatStats != null ? GameManager.Instance.combatStats.critMultiplier : 1.5f;

    public virtual void TakeDamage(float damage)
    {
        // 이미 죽은 유닛은 파괴되기 전까지 같은 프레임에 또 맞을 수 있음 (화살, 광역 공격 등)
        // → 사망 처리가 두 번 일어나지 않도록 무시
        if (isDead) return;

        // 피해 면역 상태(예: 도적 은신)면 피해 없음
        if (IsDamageImmune && damage > 0f)
        {
            if (BattleManager.Instance != null) BattleManager.Instance.ShowImmuneText(transform.position);
            Debug.Log($"🛡️ {name} 피해 면역 ({damage:F0} 무시)");
            return;
        }

        // 회피 (빠른 발 등)
        if (damage > 0f && EffectiveEvasion > 0f && Random.value < EffectiveEvasion)
        {
            if (BattleManager.Instance != null) BattleManager.Instance.ShowStatusText(transform.position, "MISS");
            return;
        }
        damage *= damageTakenMultiplier; // 약골

        // 방어력: 받는 피해 × 상수 ÷ (상수 + 방어력). 지속 피해(출혈·화상)는 방어력과 무관
        if (defense > 0f)
            damage = DefenseRules.Apply(damage, defense, GameManager.Instance != null ? GameManager.Instance.defense.armorConstant : 100f);

        currentHp -= damage;

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.ShowDamageText(transform.position, damage, nextHitCrit);
            HitFeedbackSettings fx = BattleManager.Instance.hitFeedback;
            if (damage > 0f && currentHp > 0f) Feedback.Play(fx, nextHitCrit);
            if (damage > 0f && fx != null)
            {
                if (currentHp <= 0f) CombatJuice.HitStop(fx.killHitStop);
                else if (nextHitCrit)
                {
                    CombatJuice.HitStop(fx.critHitStop);
                    CombatJuice.ShakeCamera(fx.critCameraShake);
                }
            }
        }

        if (currentHp <= 0) Die();
    }

    // ─── 지속 피해 (출혈·화상) ──────────────────────────────────────
    // 방어·막기와 상관없이 1초마다 피해를 입음. 전투 중에만 흐르고, 모닥불 휴식·다음 방으로 넘어가면 멈춤
    // 같은 종류가 이미 걸려 있으면 지속 시간은 새로 채우고, 초당 피해는 더 큰 쪽을 씀 (겹쳐 쌓이지 않음). 출혈과 화상은 따로 걸림
    class DamageOverTime
    {
        public readonly string label;
        public float damagePerTick, timeLeft, tickTimer;
        public DamageOverTime(string label) { this.label = label; }
        public bool Active => timeLeft > 0f;

        public void Apply(float damagePerSecond, float duration)
        {
            if (!Active) tickTimer = 0f;
            damagePerTick = Active ? Mathf.Max(damagePerTick, damagePerSecond) : damagePerSecond;
            timeLeft = Mathf.Max(timeLeft, duration);
        }

        public void Clear() { timeLeft = 0f; tickTimer = 0f; }

        // 이번 프레임에 줄 피해 (1초가 차지 않았으면 0)
        public float Tick(float dt)
        {
            if (!Active) return 0f;
            timeLeft -= dt;
            tickTimer += dt;
            if (tickTimer < 1f) return 0f;
            tickTimer -= 1f;
            return damagePerTick;
        }
    }

    private readonly DamageOverTime bleed = new DamageOverTime("출혈");
    private readonly DamageOverTime burn  = new DamageOverTime("화상");
    public bool IsBleeding => bleed.Active;
    public bool IsBurning  => burn.Active;

    public void ApplyBleed(float damagePerSecond, float duration)
    {
        if (isDead || damagePerSecond <= 0f || duration <= 0f) return;
        bleed.Apply(damagePerSecond, duration);
    }

    public void ApplyBurn(float damagePerSecond, float duration)
    {
        if (isDead || damagePerSecond <= 0f || duration <= 0f) return;
        burn.Apply(damagePerSecond, duration);
    }

    // 출혈·화상·스턴을 모두 풂 (모닥불 휴식, 다음 방 진입)
    public void ClearStatusEffects() { bleed.Clear(); burn.Clear(); stunTimeLeft = 0f; }

    void TickBleed()
    {
        if (!bleed.Active && !burn.Active) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.isBattleActive) return;

        foreach (DamageOverTime dot in new[] { bleed, burn })
        {
            float damage = dot.Tick(Time.deltaTime);
            if (damage <= 0f || isDead || IsDamageImmune) continue; // 면역 중에도 지속 시간은 흐름
            damage *= damageTakenMultiplier * (dot == bleed ? bleedTakenMultiplier : 1f); // 약골, 단단한 피부

            currentHp -= damage;
            BattleManager.Instance.ShowDamageText(transform.position, damage, false);
            if (currentHp <= 0)
            {
                Debug.Log($"{(dot == burn ? "🔥" : "🩸")} [{dot.label}] {name}이(가) {dot.label}으로 쓰러졌습니다.");
                Die();
            }
        }
    }

    // ─── 스턴 ──────────────────────────────────────────────────────
    // 스턴 동안에는 움직이거나 공격하지 않고, 공격 쿨타임도 멈춤 (넉백 경직과 같은 방식). 더 긴 쪽으로 갱신
    private float stunTimeLeft;
    public bool IsStunned => stunTimeLeft > 0f;

    public void Stun(float duration)
    {
        if (isDead || duration <= 0f) return;
        duration *= stunTakenMultiplier; // 강인함
        stunTimeLeft = Mathf.Max(stunTimeLeft, duration);
    }

    // 지금 노리는 대상을 잊고 다음 프레임에 다시 고름 (진형 붕괴 등)
    public void ClearTarget() { target = null; }

    // 체력이 0이 되었을 때의 처리 (isDead 프로퍼티 역할 수행)
    public bool isDead { get { return currentHp <= 0; } }

    protected virtual void Die()
    {
        Debug.Log($"💀 {name} 사망!");

        if (CompareTag("Player")) NotifyAlliesOfMyDeath();

        // 드래곤에게 마지막 일격을 넣은 모험가는 드래곤슬레이어
        if (!CompareTag("Player") && EnemyFamilyRules.IsTrueDragon(gameObject.name)
            && lastAttacker != null && lastAttacker.SourceData != null && GameManager.Instance != null)
            GameManager.Instance.GrantTrait(lastAttacker.SourceData, GameManager.Instance.dragonSlayerTrait, "드래곤에게 마지막 일격");
        
        if (myHPBar != null) Destroy(myHPBar.gameObject);

        // 쓰러짐 연출 (화면용). 도트 애니메이션이 있는 몬스터는 쓰러지는 모습을 재생하는 시체를 남김
        HitFeedbackSettings fx = FeedbackSettings;
        MonsterAnimator monsterAnim = GetComponent<MonsterAnimator>();
        if (monsterAnim != null)
        {
            if (hitFeedback != null) hitFeedback.RemoveShakeOffset();
            monsterAnim.SpawnCorpse();
        }
        if (fx != null)
        {
            if (hitFeedback != null) hitFeedback.RemoveShakeOffset();
            if (monsterAnim == null) CombatJuice.SpawnDeathGhost(GetComponent<SpriteRenderer>());
            if (isBoss)
            {
                CombatJuice.HitStop(fx.bossKillHitStop);
                CombatJuice.ShakeCamera(fx.bossDeathCameraShake);
            }
            else if (CompareTag("Player")) CombatJuice.ShakeCamera(fx.allyDeathCameraShake);
        }

        if (BattleManager.Instance != null)
        {
            bool isPlayerSide = gameObject.CompareTag("Player");
            BattleManager.Instance.OnUnitDead(isPlayerSide);
        }
        Destroy(gameObject);
    }

    void CreateHPBar()
    {
        if (hpBarPrefab == null) return;
        
        GameObject barObj = Instantiate(hpBarPrefab, transform); 
        myHPBar = barObj.GetComponent<HPBar>();
        if (myHPBar != null) myHPBar.Setup(this);
    }

    // 매니저가 "상자 열러 가!" 하고 명령할 때 호출되는 함수
    public void CommandLoot(Transform chest)
    {
        isLooting = true;
        chestTarget = chest;
        target = null; // 전투 타겟 초기화 (싸움 끝!)
        Debug.Log($"🏃 {name} : 내가 상자 열러 갈게!");
    }

    // 모닥불 휴식 상태
    protected bool isResting = false;
    private Vector3 restPosition;
    private Vector3 restLookAt;
    private bool hasRestLook;

    /// <summary>모닥불 자리에 도착해 앉아 있는가</summary>
    public bool IsSeated => isResting && (transform.position - restPosition).sqrMagnitude <= 0.0001f;

    // 모닥불 둘레에 앉음: position에 가서 lookAt(모닥불)을 바라봄
    public void CommandRest(Vector3 position, Vector3 lookAt)
    {
        CommandRest(position);
        restLookAt = lookAt;
        hasRestLook = true;
    }

    // 매니저가 "모닥불 옆 이 자리에서 쉬어!" 하고 명령할 때 호출 (ResetState로 해제)
    public void CommandRest(Vector3 position)
    {
        isResting = true;
        hasRestLook = false;
        cowardHiding = false; // 모닥불에서 쉬면 회복한 것으로 봄
        restPosition = MapGenerator.Instance != null ? MapGenerator.Instance.NearestFloor(position) : position; // 방 모양에 따라 쉬는 자리가 바닥 밖일 수 있음
        ClearStatusEffects();
        isLooting = false;
        chestTarget = null;
        target = null;
    }

    // ★ 다음 스테이지 진입 시 상태를 초기화하는 함수
    public void ResetState()
    {
        isResting = false; // 모닥불에서 쉬던 상태 해제
        ClearStatusEffects(); // 방을 넘어가면 출혈·화상·스턴 멈춤
        isLooting = false; // 상자 열러 가던 상태 해제
        target = null;     // 타겟 초기화
        
        // 다시 오른쪽(적 진영)을 바라보게 만듭니다
        SetFacing(false);
    }
}