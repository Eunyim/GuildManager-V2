using UnityEngine;
using System.Collections.Generic;

public class EnemyOrc : BattleUnit
{
    // 전투 성향 — 수호: 자기 편 후열을 노리는 적을 먼저 막음 (지금은 몬스터 후열이 없어 돌격과 같음)
    public override CombatRole Role => CombatRole.Guardian;

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable; 

    protected override void Start()
    {
        // 오크 스탯: 튼튼하고 강하지만 느림
        maxHp = 250f;
        attackRange = 1.3f; 
        moveSpeed = 1.2f; 
        attackPower = 50f;
        attackCooldown = 2.0f; // 공격 속도 느림

        base.Start();
    }

    protected override void Attack()
    {
        if(target != null)
        {
            target.TakeDamage(attackPower);
            Debug.Log($"🪓 [오크] 육중한 공격! {target.name}에게 {attackPower} 데미지!");
        }
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    protected override void Die()
    {
        CheckDrop();
        base.Die();
    }

    void CheckDrop()
    {
        DropItem.Roll(dropTable, "오크", this);
    }
}
