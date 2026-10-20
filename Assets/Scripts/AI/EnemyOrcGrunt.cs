using UnityEngine;
using System.Collections.Generic;

// 오크 하급 전사: 근력이 높아 한 방이 무겁지만 느림. 영리하게 움직이지 않고 대상에게 곧장 감
// 스탯은 프리팹(Enemy_OrcGrunt)의 직렬화 값을 그대로 씀
public class EnemyOrcGrunt : BattleUnit
{
    public override CombatRole Role => CombatRole.Charger;

    [Header("오크 하급 전사 전용")]
    public float slamMultiplier = 2.0f; // 스킬(내려찍기) 배율

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🪓 [오크 하급 전사] 육중한 도끼질! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 내려찍기
    protected override void UseSkill()
    {
        if (target != null && !target.isDead)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower * slamMultiplier);
            Debug.Log($"💥 [오크 하급 전사] 내려찍기! {victim.name}에게 {attackPower * slamMultiplier:F0} 데미지!");
        }
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "오크 하급 전사", this);
        base.Die();
    }
}
