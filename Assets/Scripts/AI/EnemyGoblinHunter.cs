using UnityEngine;

// 고블린 사냥꾼: 활 대신 작살을 던지는 원거리 전투원. 지휘를 받으면 거리를 유지하며 던짐
public class EnemyGoblinHunter : GoblinUnit
{
    protected override CombatRole CommandedRole => CombatRole.Ranged;

    [Header("고블린 사냥꾼 전용")]
    public GameObject harpoonPrefab;          // 작살 투사체
    public float heavyThrowMultiplier = 1.8f; // 스킬(힘껏 던지기) 배율

    protected override void Attack()
    {
        EnemyCombat.Shoot(this, target, harpoonPrefab, attackPower);
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 힘껏 던지기 — 강한 작살 한 발
    protected override void UseSkill()
    {
        EnemyCombat.Shoot(this, target, harpoonPrefab, attackPower * heavyThrowMultiplier);
        Debug.Log($"🔱 [고블린 사냥꾼] 힘껏 던진 작살!");
        currentMp = 0;
    }
}
