using UnityEngine;

public class Ally_Archer : BattleUnit
{
    // 전투 성향 — 원거리: 적과 거리를 유지하며 쏨
    public override CombatRole Role => CombatRole.Ranged;

   [Header("궁수 전용")]
    public GameObject arrowPrefab; // 화살 프리팹 연결 필요!

    protected override void Start()
    {
        // Initialize()가 호출되지 않은 경우(테스트 모드)에만 기본값 적용
        if (!isInitialized)
        {
            maxHp = 80f;
            currentHp = maxHp;
            moveSpeed = 3.0f;
        }
        attackRange = 6.0f; // 긴 사거리 (항상 적용)

        base.Start();
    }

    protected override void Attack()
    {
        if (target == null) return;
        
        // 화살 생성
        GameObject arrow = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
        Arrow proj = arrow.GetComponent<Arrow>();
        
        if (proj != null)
        {
            // 투사체 세팅 (위치, 데미지, 타겟 태그)
            float damage = RollBasicAttack(attackPower, out bool crit);
            proj.Setup(target.transform.position, damage, target.tag, this, crit);
        }
        
        // MP 회복
        currentMp += mpRegenOnHit;
    }

    protected override void UseSkill()
    {
        Debug.Log($"🏹 {name}의 화살 연사!");
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        // 거리순 정렬 후 최대 3명에게 데미지
        float minDist1 = float.MaxValue, minDist2 = float.MaxValue, minDist3 = float.MaxValue;
        BattleUnit t1 = null, t2 = null, t3 = null;
        foreach (GameObject obj in enemies)
        {
            BattleUnit u = obj.GetComponent<BattleUnit>();
            if (u == null || u.isDead) continue;
            float d = Vector2.Distance(transform.position, obj.transform.position);
            if (d < minDist1) { minDist3 = minDist2; t3 = t2; minDist2 = minDist1; t2 = t1; minDist1 = d; t1 = u; }
            else if (d < minDist2) { minDist3 = minDist2; t3 = t2; minDist2 = d; t2 = u; }
            else if (d < minDist3) { minDist3 = d; t3 = u; }
        }
        if (t1 != null) t1.TakeDamageFrom(this, attackPower * 1.5f);
        if (t2 != null) t2.TakeDamageFrom(this, attackPower * 1.5f);
        if (t3 != null) t3.TakeDamageFrom(this, attackPower * 1.5f);
        currentMp = 0;
    }
}