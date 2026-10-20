using UnityEngine;

public class Ally_Rogue : BattleUnit
{
    // 전투 성향 — 우회: 상대 후열을 먼저 노리고, 없으면 등 뒤로 돌아감
    public override CombatRole Role => CombatRole.Flanker;

    [Header("도적 공격 (유리대포: 빠르고 아프지만 약함)")]
    public float attacksPerSecond = 3.0f;            // 초당 공격 횟수 (쿨타임 = 1 / 이 값)
    // 기본 공격 치명타 확률은 GameManager.combatStats.rogueCritChance (BattleUnit.critChance)
    public float critMultiplier = 2.0f;              // 치명타 배율 (기본 공격, 은신 후 기습 공용)

    [Header("스킬: 은신")]
    public float stealthDuration = 2.0f; // 숨어 있는 시간(초). 숨는 순간 나를 노리던 적은 대상을 잃고, 숨어 있는 동안은 아무도 나를 노리지 않음
    [Range(0f, 1f)] public float stealthAlpha = 0.35f; // 숨어 있을 때 반투명 정도

    [Header("패시브: 기습 준비")]
    public float openingStealthDuration = 3.0f; // 매 방 전투가 시작될 때 이 시간만큼 은신한 채로 시작 (0이면 끔)

    private float stealthLeft;  // 남은 은신 시간
    private bool ambushReady;   // 다시 나타난 뒤 첫 공격은 반드시 치명타

    protected override float CritMultiplier => critMultiplier;

    public override bool IsHidden => stealthLeft > 0f;
    public override bool IsDamageImmune => stealthLeft > 0f; // 은신 중에는 피해 면역

    // 부모가 protected이므로 여기도 protected
    protected override void Start()
    {
        // Initialize()가 호출되지 않은 경우(테스트 모드)에만 기본값 적용
        if (!isInitialized)
        {
            maxHp = 70f;
            moveSpeed = 4.5f;
        }
        attackRange = 1.2f;
        if (attacksPerSecond > 0f) attackCooldown = 1f / attacksPerSecond; // 프리팹 값보다 우선
        mpRegenOnHit = 15f;

        base.Start();
    }

    protected override void Update()
    {
        if (stealthLeft > 0f)
        {
            stealthLeft -= Time.deltaTime;
            if (stealthLeft <= 0f)
            {
                stealthLeft = 0f;
                ambushReady = true;
                SetAlpha(1f);
                Debug.Log($"🗡️ {name}이(가) 그림자에서 나타났다!");
            }
        }
        base.Update();
    }

    // 부모가 protected이므로 여기도 protected
    protected override void Attack()
    {
        if (IsHidden || target == null) return; // 숨어 있는 동안에는 공격하지 않고 자리만 잡음

        bool isCritical = ambushReady || Random.value < critChance;
        float finalDamage = isCritical ? attackPower * critMultiplier : attackPower;
        if (ambushReady) Debug.Log($"🗡️ {name}의 기습! 치명타 {finalDamage:F0} 데미지!");
        ambushReady = false;

        target.TakeDamageFrom(this, finalDamage, isCritical);

        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    // 스킬: 은신 — 즉시 어그로를 끊고 숨었다가, 다시 나타나 첫 공격을 치명타로
    protected override void UseSkill()
    {
        Debug.Log($"🌫️ {name}의 은신! ({stealthDuration:F1}초)");
        currentMp = 0;
        BeginStealth(stealthDuration);
    }

    // 패시브: 매 방 전투를 은신한 채로 시작
    public override void OnRoomBattleStart()
    {
        if (openingStealthDuration <= 0f) return;
        Debug.Log($"🌫️ {name}이(가) 그림자에 숨은 채 전투를 시작합니다. ({openingStealthDuration:F1}초)");
        BeginStealth(openingStealthDuration);
    }

    void BeginStealth(float duration)
    {
        stealthLeft = Mathf.Max(stealthLeft, duration);
        ambushReady = false;
        DropAggro();
        SetAlpha(stealthAlpha);
    }

    // 몸과 손에 든 무기(자식 스프라이트)를 함께 반투명하게
    void SetAlpha(float alpha)
    {
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
