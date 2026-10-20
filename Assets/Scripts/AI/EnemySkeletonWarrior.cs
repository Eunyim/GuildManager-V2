using UnityEngine;
using System.Collections.Generic;

// 스켈레톤 전사: 일반 전사. 자기 편 후열(스켈레톤 궁수)을 노리는 적을 먼저 막음
// 스탯은 프리팹(Enemy_SkeletonWarrior)의 직렬화 값을 그대로 씀
public class EnemySkeletonWarrior : BattleUnit
{
    public override CombatRole Role => CombatRole.Guardian;

    [Header("스켈레톤 전사 전용")]
    public float smashMultiplier = 1.6f; // 스킬(뼈 부수기) 배율

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"💀 [스켈레톤 전사] 녹슨 검! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 뼈 부수기 — 강한 일격
    protected override void UseSkill()
    {
        if (target != null && !target.isDead)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower * smashMultiplier);
            Debug.Log($"💥 [스켈레톤 전사] 뼈 부수기! {victim.name}에게 {attackPower * smashMultiplier:F0} 데미지!");
        }
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "스켈레톤 전사", this);
        base.Die();
    }
}
