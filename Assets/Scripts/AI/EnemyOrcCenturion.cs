using UnityEngine;
using System.Collections.Generic;

// 오크 백부장 (오크 보스): 거대한 도끼를 쓰는 오크 지휘관
// 스킬 '지진': 도끼로 땅을 찍어 주변 모험가 전원에게 강력한 범위 피해. 스탯은 프리팹(Enemy_OrcCenturion) 값
public class EnemyOrcCenturion : BattleUnit
{
    public override CombatRole Role => CombatRole.Charger;

    [Header("오크 백부장 전용 — 스킬: 지진")]
    public float quakeRadius = 3f;        // 백부장 주변 이 거리 안의 모험가 전원
    public float quakeMultiplier = 2.2f;  // 공격력 배율

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🪓 [오크 백부장] 대도끼 내려베기! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 지진 — 주변 범위 피해
    protected override void UseSkill()
    {
        float damage = attackPower * quakeMultiplier;
        List<BattleUnit> victims = EnemyCombat.OpponentsInRadius(this, transform.position, quakeRadius);
        foreach (BattleUnit u in victims) u.TakeDamage(damage);
        Debug.Log($"💥 [오크 백부장] 지진! 반경 {quakeRadius:F1} 안 모험가 {victims.Count}명에게 {damage:F0} 데미지!");
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "오크 백부장", this);
        base.Die();
    }
}
