using UnityEngine;
using System.Collections.Generic;

// 스켈레톤 궁수: 일반 궁수. 거리를 유지하며 화살을 쏨
public class EnemySkeletonArcher : BattleUnit
{
    public override CombatRole Role => CombatRole.Ranged;

    [Header("스켈레톤 궁수 전용")]
    public GameObject arrowPrefab;           // 아군 궁수와 같은 화살 프리팹
    public float aimedShotMultiplier = 1.8f; // 스킬(조준 사격) 배율

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        EnemyCombat.Shoot(this, target, arrowPrefab, attackPower);
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 조준 사격
    protected override void UseSkill()
    {
        EnemyCombat.Shoot(this, target, arrowPrefab, attackPower * aimedShotMultiplier);
        Debug.Log($"🎯 [스켈레톤 궁수] 조준 사격!");
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "스켈레톤 궁수", this);
        base.Die();
    }
}
