using UnityEngine;
using System.Collections.Generic;

// 드래곤 (S랭크): 크고 튼튼한 몬스터. 특별한 움직임 없이 대상에게 곧장 감
// 스탯은 프리팹(Enemy_Dragon)의 직렬화 값을 그대로 씀 (예전에는 슬라임 스크립트를 쓰고 있었음)
public class EnemyDragon : BattleUnit
{
    public override CombatRole Role => CombatRole.Charger;

    [Header("드래곤 전용")]
    public float biteMultiplier = 2.0f; // 스킬(물어뜯기) 배율

    [Header("새끼 드래곤 (해츨링·베이비 드래곤)")]
    [Tooltip("능력치를 물려받을 어른 드래곤 프리팹. 비워 두면 이 프리팹의 값을 그대로 씀")]
    public GameObject statSource;
    [Tooltip("어른 드래곤 HP·공격력에 곱하는 비율 (해츨링 0.2, 베이비 드래곤 0.4)")]
    [Range(0f, 1f)] public float statRatio = 1f;
    [Tooltip("어른 드래곤 HP에 곱하는 비율. 0 이하면 statRatio를 씀 (어른 HP를 올려도 새끼 HP는 그대로 두려고 따로 둠)")]
    [Range(0f, 1f)] public float hpRatio = 0f;

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Start()
    {
        // 새끼 드래곤: 어른 드래곤의 HP·공격력 x 비율 (랭크 배율은 base.Start에서 곱해짐)
        EnemyDragon adult = statSource != null ? statSource.GetComponent<EnemyDragon>() : null;
        if (adult != null && adult != this)
        {
            maxHp = adult.maxHp * (hpRatio > 0f ? hpRatio : statRatio);
            attackPower = adult.attackPower * statRatio;
        }
        base.Start();
    }

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🐉 [{DisplayNames.EnemyToKorean(name)}] 발톱 할퀴기! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    [Header("드래곤 브레스 (어른 드래곤 스킬. 새끼 드래곤은 꺼 두고 물어뜯기)")]
    public bool useBreath = true;
    public float breathRange = 5f;          // 드래곤 앞쪽 이 거리 안
    public float breathMultiplier = 1.5f;   // 맞는 순간 피해 (공격력 배율)
    public float burnPerSecondRatio = 0.25f; // 화상: 1초마다 공격력의 이 비율
    public float burnDuration = 4f;

    // 스킬: 드래곤 브레스(앞쪽 범위 피해 + 화상) 또는 물어뜯기
    protected override void UseSkill()
    {
        if (useBreath) { Breath(); return; }

        if (target != null && !target.isDead)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower * biteMultiplier);
            Debug.Log($"🐉 [{DisplayNames.EnemyToKorean(name)}] 물어뜯기! {victim.name}에게 {attackPower * biteMultiplier:F0} 데미지!");
        }
        currentMp = 0;
    }

    // 대상 쪽으로 불을 뿜음: 드래곤 앞쪽(대상이 있는 쪽) breathRange 안의 모험가 전원
    void Breath()
    {
        currentMp = 0;
        float facing = target != null ? Mathf.Sign(target.transform.position.x - transform.position.x) : -1f;
        if (facing == 0f) facing = -1f;

        float damage = attackPower * breathMultiplier;
        float burn = attackPower * burnPerSecondRatio;
        int hit = 0;
        foreach (BattleUnit u in EnemyCombat.OpponentsInRadius(this, transform.position, breathRange))
        {
            if ((u.transform.position.x - transform.position.x) * facing < -0.3f) continue; // 등 뒤는 안 맞음
            u.TakeDamage(damage);
            if (!u.isDead) u.ApplyBurn(burn, burnDuration);
            hit++;
        }
        Debug.Log($"🔥 [{DisplayNames.EnemyToKorean(name)}] 드래곤 브레스! 모험가 {hit}명에게 {damage:F0} 데미지 + {burnDuration:F0}초 동안 초당 {burn:F0} 화상");
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, DisplayNames.EnemyToKorean(name), this);
        base.Die();
    }
}
