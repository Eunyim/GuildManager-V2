using UnityEngine;
using System.Collections.Generic;

public class EnemySlime : BattleUnit
{
    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable; // 전리품 목록

    [Header("분열 설정")]
    public GameObject slimePrefab;       // 자기 자신 프리팹 (인스펙터에서 연결)
    public int splitCount = 0;           // 이미 분열한 횟수 (0 = 원본)
    public int maxSplitCount = 1;        // 최대 분열 가능 횟수
    [Tooltip("분열로 나온 슬라임(킹 슬라임 분열·미니 슬라임)이 나오자마자 기절해 있는 시간(초)")]
    public float splitStunDuration = 0.5f;
    [HideInInspector] public bool spawnedBySplit; // 분열로 나온 슬라임이면 Start에서 splitStunDuration만큼 기절

    [Header("킹 슬라임 (보스)")]
    public bool isKing = false;          // 보스방에서 BattleManager가 켬. 죽으면 일반 슬라임으로 분열 (그 슬라임들은 더 분열하지 않음)
    public float kingMaxHp = 500f;       // 랭크 배율은 이 값에 곱해짐
    public float kingAttackPower = 40f;  // 착지 범위 공격의 기준 피해 (점프 중엔 공격 못 하는 만큼 30에서 올림)
    public float kingAttackRange = 2.0f; // 몸이 커서 사거리도 넓게
    public float kingAttackCooldown = 1.5f;
    public int kingSplitMin = 3;         // 죽을 때 나오는 일반 슬라임 수 (최소~최대)
    public int kingSplitMax = 4;
    public float kingSplitRadius = 1.5f; // 나오는 슬라임이 흩어지는 범위

    [Header("킹 슬라임 이동·공격: 걷지 않고 계속 깡충 뜀 (스킬·마나 없음). 공격은 착지할 때의 범위 피해뿐 (단일 공격 없음)")]
    public float hopDistance = 1.2f;   // 한 번에 뛰는 최대 거리
    public float hopDuration = 0.6f;   // 뛰어올라 떨어지기까지 걸리는 시간(초)
    public float hopHeight = 1.0f;     // 포물선 최고 높이
    public float hopInterval = 2.0f;   // 착지 후 다음 점프까지 쉬는 시간(초)
    public float kingHopInterval = 1.5f; // 킹 슬라임일 때 hopInterval 대신 씀 (10/18 측정, 10/20 확정)
    [UnityEngine.Tooltip("착지 지점에서의 거리별 피해 비율: ~1 100% / ~3 80% / ~5 50%")]
    public SplashFalloffSettings landingSplash = new SplashFalloffSettings();

    private bool jumping;
    private float nextHopTime;

    public override bool IsDamageImmune => isKing && jumping; // 킹 슬라임은 점프하는 동안 피해 면역

    protected override void Start()
    {
        // 슬라임 스탯 (분열체는 Initialize에서 이미 설정됨)
        if (isKing)
        {
            maxHp = kingMaxHp;
            attackRange = kingAttackRange;
            moveSpeed = 0f;  // 걷지 않음 (점프로만 이동)
            maxMp = 0f;      // 마나 없음
            currentMp = 0f;
            attackPower = kingAttackPower;
            attackCooldown = kingAttackCooldown;
            hopInterval = kingHopInterval;
        }
        else if (splitCount == 0)
        {
            maxHp = 100f;
            attackRange = 1.2f;
            moveSpeed = 1.5f;
            attackPower = 20f;
            attackCooldown = 1.5f;
        }

        base.Start();
        if (spawnedBySplit) Stun(splitStunDuration); // Start 전에는 체력이 0이라 여기서 검
    }

    protected override void Attack()
    {
        if (isKing) return; // 킹 슬라임은 단일 공격 없음 (착지 범위 피해로만 공격)
        if(target != null)
        {
            target.TakeDamage(attackPower);
            Debug.Log($"💥 [슬라임] 퍽! {target.name}에게 {attackPower} 데미지!");
        }
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 점프하는 동안에는 다른 행동을 하지 않음
    // 킹 슬라임은 쉬는 시간이 끝날 때마다 계속 뜀 (다가갈 거리가 없으면 제자리에서)
    protected override void Update()
    {
        if (jumping) return;
        if (isKing && !isDead && target != null && !target.isDead && Time.time >= nextHopTime)
        {
            StartHop();
            return;
        }
        base.Update();
    }

    // 킹 슬라임은 걷지 않음 (이동은 위의 점프로만). 일반 슬라임은 평소처럼 걸음
    protected override void MoveToTarget()
    {
        if (!isKing) base.MoveToTarget();
    }

    // 대상 쪽으로 사거리 안쪽까지 최대 hopDistance만큼, 이미 가까우면(또는 착지점이 바닥 밖이면) 제자리 점프
    void StartHop()
    {
        Vector3 start = transform.position;
        Vector3 toTarget = target.transform.position - start;
        toTarget.z = 0f;
        float gap = toTarget.magnitude - attackRange * 0.8f;

        Vector3 landing = start;
        if (gap > 0.05f)
        {
            landing = start + toTarget.normalized * Mathf.Min(hopDistance, gap);
            if (MapGenerator.Instance != null && !MapGenerator.Instance.IsOnFloor(landing)) landing = start;
        }
        StartCoroutine(Hop(landing, toTarget.x < 0f));
    }

    System.Collections.IEnumerator Hop(Vector3 landing, bool faceLeft)
    {
        jumping = true;
        Vector3 start = transform.position;
        SetFacing(faceLeft);

        float t = 0f;
        while (t < hopDuration)
        {
            if (isDead) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / hopDuration);
            Vector3 pos = Vector3.Lerp(start, landing, k);
            pos.y += 4f * hopHeight * k * (1f - k); // 포물선
            transform.position = pos;
            yield return null;
        }
        transform.position = landing;
        float moved = Vector3.Distance(start, landing);
        LandingSplash(landing, moved);
        jumping = false;
        nextHopTime = Time.time + hopInterval;
    }

    // 착지: 착지 지점에서의 거리에 따라 범위 안 모험가 전원에게 피해 (가까울수록 셈)
    void LandingSplash(Vector3 landing, float moved)
    {
        List<BattleUnit> victims = EnemyCombat.OpponentsInRadius(this, landing, landingSplash.outerRadius);
        List<string> hits = new List<string>();
        foreach (BattleUnit u in victims)
        {
            float distance = Vector2.Distance(landing, u.transform.position);
            float damage = attackPower * SplashFalloff.GetRatio(landingSplash, distance);
            if (damage <= 0f) continue;
            bool immune = u.IsDamageImmune;
            u.TakeDamage(damage);
            hits.Add(immune ? $"{u.name} 면역({distance:F1}m)" : $"{u.name} {damage:F0}({distance:F1}m)");
        }
        string where = moved > 0.01f ? $"{moved:F2}칸 이동" : "제자리";
        Debug.Log($"👑 [킹 슬라임] 깡충! ({where}) 착지 범위 피해 {hits.Count}명: {string.Join(", ", hits)}");
    }

    protected override void Die()
    {
        CheckDrop();
        if (isKing) SpawnNormalSlimes();
        else SpawnMiniSlimes();
        base.Die();
    }

    // 소환한 쪽에서 원본 프리팹을 알려 줌 (인스펙터에 slimePrefab이 비어 있어도 분열할 수 있도록)
    public static void LinkPrefab(GameObject instance, GameObject prefab)
    {
        EnemySlime slime = instance != null ? instance.GetComponent<EnemySlime>() : null;
        if (slime != null && slime.slimePrefab == null) slime.slimePrefab = prefab;
    }

    // 킹 슬라임이 죽을 때: 일반 슬라임(원본 크기, 기본 스탯 + 랭크 배율)이 나옴. 이 슬라임들은 미니 슬라임으로 분열하지 않음
    void SpawnNormalSlimes()
    {
        if (slimePrefab == null) return;

        int count = Random.Range(kingSplitMin, kingSplitMax + 1);
        Vector3 prefabScale = slimePrefab.transform.localScale;
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-kingSplitRadius, kingSplitRadius), Random.Range(-kingSplitRadius, kingSplitRadius) * 0.5f, 0f);
            GameObject slime = Instantiate(slimePrefab, transform.position + offset, Quaternion.identity);
            slime.tag = "Enemy";
            slime.transform.localScale = new Vector3(-Mathf.Abs(prefabScale.x), Mathf.Abs(prefabScale.y), prefabScale.z);

            EnemySlime child = slime.GetComponent<EnemySlime>();
            if (child != null)
            {
                child.isKing = false;
                child.splitCount = 0;     // 0이어야 Start에서 일반 슬라임 스탯이 들어감
                child.maxSplitCount = 0;  // 미니 슬라임으로는 분열하지 않음
                child.slimePrefab = slimePrefab;
                child.spawnedBySplit = true;
            }

            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.ApplyQuestRankScaling(slime);
                BattleManager.Instance.OnEnemyAdded();
            }
        }

        Debug.Log($"👑 [{name}] 쓰러지며 분열! 슬라임 {count}마리 등장!");
    }

    void SpawnMiniSlimes()
    {
        // 분열 한도 초과 또는 프리팹 없으면 분열하지 않음
        if (splitCount >= maxSplitCount || slimePrefab == null) return;

        int count = Random.Range(3, 5); // 3~4마리

        for (int i = 0; i < count; i++)
        {
            // 원본 위치 주변에 랜덤 배치
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), 0f);
            GameObject mini = Instantiate(slimePrefab, transform.position + offset, Quaternion.identity);
            mini.tag = "Enemy";
            // 작은 크기: 슬라임 프리팹 크기의 60% (왼쪽을 보도록 x는 음수)
            Vector3 prefabScale = slimePrefab.transform.localScale;
            mini.transform.localScale = new Vector3(-Mathf.Abs(prefabScale.x) * 0.6f, Mathf.Abs(prefabScale.y) * 0.6f, 1f);

            EnemySlime miniSlime = mini.GetComponent<EnemySlime>();
            if (miniSlime != null)
            {
                // 분열체 스탯: 원본의 절반
                miniSlime.splitCount = splitCount + 1;
                miniSlime.maxSplitCount = maxSplitCount;
                miniSlime.slimePrefab = slimePrefab;
                miniSlime.maxHp = maxHp * 0.4f;
                miniSlime.currentHp = miniSlime.maxHp;
                miniSlime.attackPower = attackPower * 0.5f;
                miniSlime.moveSpeed = moveSpeed * 1.3f; // 작으니까 더 빠름
                miniSlime.attackRange = attackRange;
                miniSlime.attackCooldown = attackCooldown;
                miniSlime.spawnedBySplit = true;
            }

            // BattleManager 적 카운트 증가
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.OnEnemyAdded();
            }
        }

        Debug.Log($"🫧 [{name}] 분열! 미니 슬라임 {count}마리 등장!");
    }

    void CheckDrop()
    {
        DropItem.Roll(dropTable, name, this);
    }
}