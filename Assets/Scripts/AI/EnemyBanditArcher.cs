using UnityEngine;
using System.Collections.Generic;

// 도적단 궁수: 무리 뒤에서 화살을 쏘는 원거리 전투원. 스탯은 프리팹(Enemy_BanditArcher)의 직렬화 값을 그대로 씀
public class EnemyBanditArcher : BattleUnit
{
    // 전투 성향 — 원거리: 적과 거리를 유지하며 쏨
    public override CombatRole Role => CombatRole.Ranged;

    [Header("도적단 궁수 전용")]
    public GameObject arrowPrefab;       // 아군 궁수와 같은 화살 프리팹
    public float aimedShotMultiplier = 1.8f; // 스킬(조준 사격) 배율

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        Shoot(attackPower);
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 스킬: 조준 사격 — 강한 화살 한 발
    protected override void UseSkill()
    {
        Shoot(attackPower * aimedShotMultiplier);
        Debug.Log($"🎯 [도적단 궁수] 조준 사격!");
        currentMp = 0;
    }

    void Shoot(float damage)
    {
        if (target == null || target.isDead) return;
        if (arrowPrefab == null)
        {
            // 화살 프리팹이 없으면 바로 맞힘 (연결 누락 대비)
            target.TakeDamage(damage);
            return;
        }
        GameObject arrow = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
        Arrow proj = arrow.GetComponent<Arrow>();
        if (proj != null) proj.Setup(target.transform.position, damage, target.tag);
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "도적단 궁수", this);
        base.Die();
    }
}
