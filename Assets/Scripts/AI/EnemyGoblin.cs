using UnityEngine;
using System.Collections.Generic;

public class EnemyGoblin : BattleUnit
{
    // 전투 성향 — 우회: 날쌘 고블린은 후열을 노리거나 등 뒤로 돌아감
    public override CombatRole Role => CombatRole.Flanker;

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable; 

    protected override void Start()
    {
        // 고블린 스탯: 빠르고 약함
        maxHp = 60f;
        attackRange = 0.9f; 
        moveSpeed = 3.0f; 
        attackPower = 16f;
        attackCooldown = 0.8f; // 공격 속도 빠름

        base.Start();
    }

    protected override void Attack()
    {
        if(target != null)
        {
            target.TakeDamage(attackPower);
            Debug.Log($"🔪 [고블린] 슥삭! {target.name}에게 {attackPower} 데미지!");
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
        DropItem.Roll(dropTable, "고블린", this);
    }
}
