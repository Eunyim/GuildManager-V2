using UnityEngine;

// 고블린 전사: 무리의 앞줄. 지휘를 받으면 후열(사냥꾼·사제)을 노리는 적을 먼저 막음
public class EnemyGoblinWarrior : GoblinUnit
{
    protected override CombatRole CommandedRole => CombatRole.Guardian;

    [Header("고블린 전사 전용")]
    public float smashMultiplier = 1.6f; // 스킬(강타) 배율

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🗡️ [고블린 전사] 베기! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 강타
    protected override void UseSkill()
    {
        if (target != null && !target.isDead)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower * smashMultiplier);
            Debug.Log($"💢 [고블린 전사] 강타! {victim.name}에게 {attackPower * smashMultiplier:F0} 데미지!");
        }
        currentMp = 0;
    }
}
