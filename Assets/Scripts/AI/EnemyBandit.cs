using UnityEngine;
using System.Collections.Generic;

// 도적단 암살자: 무리 중 날쌘 기습·암살 담당. 스탯은 프리팹(Enemy_Bandit)의 직렬화 값을 그대로 씀
// (적 목록에서는 Enemy_Bandit이 '도적단' 무리를 대표함 → QuestGenerator가 전사·암살자·궁수 무리로 바꿈)
public class EnemyBandit : BattleUnit
{
    // 전투 성향 — 우회: 상대 후열을 노리거나 등 뒤로 돌아감
    public override CombatRole Role => CombatRole.Flanker;

    [Header("도적단 암살자 전용")]
    [Range(0f, 1f)] public float poisonChance = 0.3f; // 독 공격 확률
    public float poisonMultiplier = 1.5f;              // 독 공격 배율
    public float flurryMultiplier = 0.7f;              // 스킬(연속 난도질) 한 번당 배율, 3회

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if(target != null)
        {
            bool poison = Random.value < poisonChance;
            float damage = poison ? attackPower * poisonMultiplier : attackPower;
            target.TakeDamage(damage);
            if (poison)
                Debug.Log($"🗡️ [도적단 암살자] 독 묻은 기습! {target.name}에게 {damage:F0} 데미지!");
            else
                Debug.Log($"🗡️ [도적단 암살자] 기습! {target.name}에게 {damage:F0} 데미지!");
        }
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 스킬: 연속 난도질 — 대상 3회 연타
    protected override void UseSkill()
    {
        if (target == null || target.isDead) { currentMp = 0; return; }

        Debug.Log($"🔪 [도적단 암살자] 연속 난도질!");
        for (int i = 0; i < 3; i++)
        {
            if (target != null && !target.isDead)
                target.TakeDamage(attackPower * flurryMultiplier);
        }
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "도적단 암살자", this);
        base.Die();
    }
}
