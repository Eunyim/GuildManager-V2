using UnityEngine;

// BattleUnit을 상속받음 (: BattleUnit)
public class Ally_Warrior : BattleUnit
{
    // 전투 성향 — 수호: 우리 후열을 노리는 적을 먼저 막음
    public override CombatRole Role => CombatRole.Guardian;

    [Header("전사 방어")]
    [Range(0f, 1f)]
    public float damageReduction = 0.2f; // 패시브: 받는 피해 감소 비율
    public int blockCount = 3;           // 액티브(방패 막기): 스킬 사용 후 무효화하는 공격 횟수
    public float mpGainOnDamaged = 10f;  // 피해를 받을 때마다 차는 마나 (공격할 때 차는 양과 별개). 방패 막기로 막은 공격은 제외

    private int blocksLeft = 0; // 남은 무효화 횟수

    [Header("전사 공격")]
    public float attacksPerSecond = 0.6f;   // 초당 공격 횟수 (쿨타임 = 1 / 이 값)
    public float knockbackDistance = 0.4f;  // 베기에 맞은 적이 밀려나는 거리
    public float knockbackDuration = 0.15f; // 밀려나는 데 걸리는 시간(초) = 경직 시간. 그동안 적은 움직이거나 공격하지 못하고 공격 쿨타임도 멈춤

    protected override void Start()
    {
        // Initialize()가 호출되지 않은 경우(테스트 모드)에만 기본값 적용
        if (!isInitialized)
        {
            maxHp = 150f;
            currentHp = maxHp;
            moveSpeed = 3.5f;
        }
        attackRange = 1.1f; // 전사 전용 사거리 (항상 적용)
        if (attacksPerSecond > 0f) attackCooldown = 1f / attacksPerSecond; // 프리팹 값(1초)보다 우선

        base.Start(); // 부모의 Start 실행 (HP바 연결 등)
    }

    protected override void Attack()
    {
        if(target != null)
        {
            // 이 공격으로 대상이 죽으면 target이 비워질 수 있으므로 따로 잡아 둠
            BattleUnit victim = target;
            Debug.Log($"⚔️ {name}의 베기 공격!");
            float damage = RollBasicAttack(attackPower, out bool crit);
            victim.TakeDamageFrom(this, damage, crit);
            if (victim != null && !victim.isDead)
                victim.ApplyKnockback(victim.transform.position - transform.position, knockbackDistance, knockbackDuration);
        }
        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 액티브: 다음 공격 몇 번을 튕겨냄 (남은 횟수에 더하지 않고 다시 채움)
    protected override void UseSkill()
    {
        blocksLeft = blockCount;
        Debug.Log($"🛡️ {name}의 방패 막기! (다음 공격 {blocksLeft}회 무효)");
        currentMp = 0;
    }

    public override void TakeDamage(float damage)
    {
        if (isDead) return;

        // 방패 막기가 남아 있으면 이번 공격은 피해 없음
        if (blocksLeft > 0)
        {
            blocksLeft--;
            if (BattleManager.Instance != null) BattleManager.Instance.ShowBlockText(transform.position);
            return;
        }

        // 패시브: 받는 피해 감소
        base.TakeDamage(damage * (1f - damageReduction));

        // 탱커는 맞을수록 방패 막기가 빨리 나옴
        if (!isDead && damage > 0f) currentMp = Mathf.Min(maxMp, currentMp + mpGainOnDamaged);
    }
}
