using UnityEngine;
using System.Collections.Generic;

// 알파 (초원 늑대 보스): 무리의 우두머리. 스킬 '하울링'으로 아군(모험가) 전원을 잠시 스턴
// 스탯은 프리팹(Enemy_AlphaWolf)의 직렬화 값을 그대로 씀
public class EnemyAlphaWolf : BattleUnit
{
    public override CombatRole Role => CombatRole.Charger;

    [Header("알파 전용 — 스킬: 하울링")]
    public float howlStunDuration = 0.8f; // 모험가 전원 스턴 시간(초)

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🐺 [알파] 물어뜯기! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 하울링 — 상대편 전원 스턴
    protected override void UseSkill()
    {
        List<BattleUnit> victims = EnemyCombat.AllOpponents(this);
        foreach (BattleUnit u in victims) u.Stun(howlStunDuration);
        Debug.Log($"🐺 [알파] 하울링! 모험가 {victims.Count}명 {howlStunDuration:F1}초 스턴");
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "알파", this);
        base.Die();
    }
}
