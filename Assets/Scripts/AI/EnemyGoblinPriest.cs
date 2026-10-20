using UnityEngine;

// 고블린 사제: 무리를 지휘하는 후열. 고블린들에게 축복(공격력 증가)을 내림
// 마지막 고블린 사제가 쓰러지면 남은 고블린들은 명령체계를 잃음 (GoblinUnit.LoseCommand)
public class EnemyGoblinPriest : GoblinUnit
{
    protected override CombatRole CommandedRole => CombatRole.Support;

    [Header("고블린 사제 전용")]
    public float blessingMultiplier = 1.2f; // 축복: 고블린 공격력 배율
    public float blessingDuration = 6f;     // 축복 지속 시간(초)
    public float mpRegenPerSecond = 8f;     // 전투 중 저절로 차는 마나 (뒤에 숨어 있어도 축복을 쓸 수 있게)

    protected override void Update()
    {
        base.Update();
        if (isDead || isResting) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.isBattleActive) return;

        currentMp = Mathf.Min(currentMp + mpRegenPerSecond * Time.deltaTime, maxMp);
        if (currentMp >= maxMp) UseSkill();
    }

    // 평타: 약한 저주 (원거리에서 바로 맞힘)
    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🪄 [{DisplayNames.EnemyToKorean(name)}] 저주! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 축복 — 살아 있는 고블린 전원(자신 포함) 공격력 증가
    protected override void UseSkill()
    {
        int count = 0;
        foreach (GoblinUnit goblin in LivingGoblins())
        {
            goblin.ApplyBlessing(blessingMultiplier, blessingDuration);
            count++;
        }
        Debug.Log($"📯 [{DisplayNames.EnemyToKorean(name)}] 축복! 고블린 {count}마리 공격력 {Mathf.RoundToInt((blessingMultiplier - 1f) * 100f)}% 증가 ({blessingDuration:F0}초)");
        currentMp = 0;
    }

    protected override void Die()
    {
        // 다른 고블린 사제가 살아 있으면 지휘는 이어짐
        bool otherPriestAlive = false;
        foreach (GoblinUnit goblin in LivingGoblins())
            if (goblin != this && goblin is EnemyGoblinPriest) { otherPriestAlive = true; break; }

        if (!otherPriestAlive)
        {
            int count = 0;
            foreach (GoblinUnit goblin in LivingGoblins())
            {
                if (goblin == this) continue;
                goblin.LoseCommand();
                count++;
            }
            if (count > 0)
                Debug.Log($"📉 [고블린] {DisplayNames.EnemyToKorean(name)}이(가) 쓰러져 명령체계가 무너졌습니다! 고블린 {count}마리 능력치 {Mathf.RoundToInt(commandLossPenalty * 100f)}% 감소, 움직임이 단순해짐");
        }
        base.Die();
    }
}
