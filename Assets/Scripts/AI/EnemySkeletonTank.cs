using UnityEngine;
using System.Collections.Generic;

// 스켈레톤 탱커: 갑옷을 걸쳐 받는 피해가 적음. 어그로가 높아 아군의 공격을 끌어당기고, 자기 편 후열을 지킴
// (출혈 같은 지속 피해는 갑옷과 상관없이 들어감)
public class EnemySkeletonTank : BattleUnit
{
    public override CombatRole Role => CombatRole.Guardian;

    [Header("스켈레톤 탱커 전용")]
    [Tooltip("갑옷: 받는 피해 감소 비율")]
    [Range(0f, 0.9f)] public float armorReduction = 0.4f;
    [Tooltip("스킬(방패 태세) 동안의 피해 감소 비율")]
    [Range(0f, 0.95f)] public float guardReduction = 0.7f;
    public float guardDuration = 3f;

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    private float guardTimeLeft;

    protected override void Update()
    {
        if (guardTimeLeft > 0f) guardTimeLeft -= Time.deltaTime;
        base.Update();
    }

    public override void TakeDamage(float damage)
    {
        float reduction = guardTimeLeft > 0f ? Mathf.Max(guardReduction, armorReduction) : armorReduction;
        base.TakeDamage(damage * (1f - reduction));
    }

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🛡️ [스켈레톤 탱커] 방패 밀치기! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 방패 태세 — 잠시 받는 피해를 크게 줄임
    protected override void UseSkill()
    {
        guardTimeLeft = guardDuration;
        Debug.Log($"🛡️ [스켈레톤 탱커] 방패 태세! {guardDuration:F0}초 동안 받는 피해 {Mathf.RoundToInt(guardReduction * 100f)}% 감소");
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "스켈레톤 탱커", this);
        base.Die();
    }
}
