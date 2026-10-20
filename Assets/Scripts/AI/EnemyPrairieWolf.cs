using UnityEngine;
using System.Collections.Generic;

// 초원 늑대 (D랭크): 날쌘 암살자형. 후열을 노리거나 등 뒤로 돌아가 물어뜯음
// 스탯은 프리팹(Enemy_PrairieWolf)의 직렬화 값을 그대로 씀
public class EnemyPrairieWolf : BattleUnit
{
    // 전투 성향 — 우회 (도적·암살자 계열)
    public override CombatRole Role => CombatRole.Flanker;

    [Header("초원 늑대 전용 — 스킬: 출혈 공격")]
    public float bleedBiteMultiplier = 1.0f;   // 출혈 공격 자체의 피해 배율
    public float bleedPerSecondRatio = 0.35f;  // 출혈로 1초마다 입히는 피해 (공격력 대비, 의뢰 랭크 배율을 따라감)
    public float bleedDuration = 5f;           // 출혈 지속 시간(초)

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🐺 [초원 늑대] 물어뜯기! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 스킬: 출혈 공격 — 마나가 차면(평타 5번) 물고 나서 일정 시간 동안 1초마다 피해
    protected override void UseSkill()
    {
        if (target == null || target.isDead) { currentMp = 0; return; }

        BattleUnit victim = target;
        victim.TakeDamage(attackPower * bleedBiteMultiplier);
        float bleedPerSecond = attackPower * bleedPerSecondRatio;
        if (!victim.isDead) victim.ApplyBleed(bleedPerSecond, bleedDuration);
        Debug.Log($"🩸 [초원 늑대] 출혈 공격! {victim.name}에게 {attackPower * bleedBiteMultiplier:F0} 데미지 + {bleedDuration:F0}초 동안 초당 {bleedPerSecond:F1} 출혈");
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "초원 늑대", this);
        base.Die();
    }
}
