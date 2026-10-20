using UnityEngine;
using System.Collections.Generic;

// 도적단 전사: 무리의 앞을 지키는 근접 전투원. 스탯은 프리팹(Enemy_BanditWarrior)의 직렬화 값을 그대로 씀
public class EnemyBanditWarrior : BattleUnit
{
    // 전투 성향 — 수호: 무리의 궁수를 노리는 적을 먼저 막음
    public override CombatRole Role => CombatRole.Guardian;

    [Header("스킬")]
    public float smashMultiplier = 1.8f; // 강타: 공격력 배율

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if (target != null)
        {
            target.TakeDamage(attackPower);
            Debug.Log($"🪓 [도적단 전사] {target.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 스킬: 강타 — 대상에게 큰 한 방
    protected override void UseSkill()
    {
        if (target != null && !target.isDead)
        {
            float damage = attackPower * smashMultiplier;
            target.TakeDamage(damage);
            Debug.Log($"💢 [도적단 전사] 강타! {target.name}에게 {damage:F0} 데미지!");
        }
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "도적단 전사", this);
        base.Die();
    }
}
