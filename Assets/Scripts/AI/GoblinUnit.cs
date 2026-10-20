using UnityEngine;
using System.Collections.Generic;

// 고블린 무리 공통: 고블린 사제가 지휘하는 동안에는 성향대로 영리하게 움직이고(수호·원거리·지원),
// 마지막 사제가 쓰러지면 명령체계를 잃음 → 능력치 감소 + 단순한 움직임(돌격: 후열 노리기·물러나기 없이 곧장 감)
// 스탯은 프리팹의 직렬화 값을 그대로 씀
public abstract class GoblinUnit : BattleUnit
{
    // 지휘를 받을 때의 전투 성향 (직업마다 다름)
    protected abstract CombatRole CommandedRole { get; }
    public override CombatRole Role => CommandLost ? CombatRole.Charger : CommandedRole;

    [Header("고블린 무리 공통")]
    [Tooltip("명령체계를 잃었을 때 최대 체력·체력·공격력 감소 비율")]
    [Range(0f, 1f)] public float commandLossPenalty = 0.1f;

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    public bool CommandLost { get; private set; }

    private float commandAttackMultiplier = 1f; // 명령체계 상실 (0.9)
    private float blessingMultiplier = 1f;      // 고블린 사제의 축복
    private float blessingTimeLeft;
    private float unbuffedAttack;               // 랭크 배율까지 적용된 원래 공격력

    protected override void Start()
    {
        base.Start();
        unbuffedAttack = attackPower;
    }

    protected override void Update()
    {
        if (blessingTimeLeft > 0f)
        {
            blessingTimeLeft -= Time.deltaTime;
            if (blessingTimeLeft <= 0f) { blessingMultiplier = 1f; RefreshAttack(); }
        }
        base.Update();
    }

    // 고블린 사제의 축복: 일정 시간 공격력 증가 (겹치지 않고, 더 센 쪽·더 긴 쪽으로 갱신)
    public void ApplyBlessing(float multiplier, float duration)
    {
        if (isDead) return;
        blessingMultiplier = Mathf.Max(blessingTimeLeft > 0f ? blessingMultiplier : 1f, multiplier);
        blessingTimeLeft = Mathf.Max(blessingTimeLeft, duration);
        RefreshAttack();
    }

    // 명령체계 상실: 한 번만 적용
    public void LoseCommand()
    {
        if (isDead || CommandLost) return;
        CommandLost = true;
        float keep = 1f - commandLossPenalty;
        maxHp *= keep;
        currentHp = Mathf.Min(currentHp * keep, maxHp);
        commandAttackMultiplier = keep;
        RefreshAttack();
        target = null; // 새 성향으로 대상을 다시 고름
    }

    // 샤먼킹 오라 안이면 받는 피해 감소 (출혈 같은 지속 피해는 해당 없음)
    public override void TakeDamage(float damage)
    {
        float reduction = EnemyGoblinShamanKing.AuraReductionAt(this);
        base.TakeDamage(damage * (1f - reduction));
    }

    void RefreshAttack()
    {
        attackPower = unbuffedAttack * commandAttackMultiplier * blessingMultiplier;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, DisplayNames.EnemyToKorean(name), this);
        base.Die();
    }

    // 살아 있는 같은 편 고블린 목록
    protected List<GoblinUnit> LivingGoblins()
    {
        var list = new List<GoblinUnit>();
        foreach (BattleUnit u in Active)
            if (u is GoblinUnit g && g != null && !g.isDead && g.CompareTag(gameObject.tag)) list.Add(g);
        return list;
    }
}
