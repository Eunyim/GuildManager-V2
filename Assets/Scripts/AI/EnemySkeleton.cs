using UnityEngine;
using System.Collections.Generic;

public class EnemySkeleton : BattleUnit
{
    // 전투 성향 — 수호: 자기 편 후열을 노리는 적을 먼저 막음 (지금은 몬스터 후열이 없어 돌격과 같음)
    public override CombatRole Role => CombatRole.Guardian;

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable; 

    protected override void Start()
    {
        // 스켈레톤 스탯: 평균적이지만 죽지 않는 느낌
        maxHp = 120f;
        attackRange = 1.0f; 
        moveSpeed = 1.5f; 
        attackPower = 24f;
        attackCooldown = 1.5f;

        base.Start();
    }

    protected override void Attack()
    {
        if(target != null)
        {
            target.TakeDamage(attackPower);
            Debug.Log($"💀 [스켈레톤] 뼈를 깎는 공격! {target.name}에게 {attackPower} 데미지!");
        }
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 스킬: 골격 재생 — 자기 HP를 최대 체력의 30% 회복
    protected override void UseSkill()
    {
        float healAmount = maxHp * 0.3f;
        float before = currentHp;
        currentHp = Mathf.Min(currentHp + healAmount, maxHp);
        if (BattleManager.Instance != null)
            BattleManager.Instance.ShowHealText(transform.position, currentHp - before);
        Debug.Log($"🦴 [스켈레톤] 골격 재생! HP {healAmount:F0} 회복! (현재: {currentHp:F0}/{maxHp:F0})");
        currentMp = 0;
    }

    protected override void Die()
    {
        CheckDrop();
        base.Die();
    }

    void CheckDrop()
    {
        DropItem.Roll(dropTable, "스켈레톤", this);
    }
}
