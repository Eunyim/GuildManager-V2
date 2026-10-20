using UnityEngine;

public class Ally_Healer : BattleUnit
{
    // 전투 성향 — 지원: 적이 다가오면 도망침
    public override CombatRole Role => CombatRole.Support;

    // protected (부모와 맞춤)
    protected override void Start()
    {
        // Initialize()가 호출되지 않은 경우(테스트 모드)에만 기본값 적용
        if (!isInitialized)
        {
            maxHp = 80f;
            attackPower = 10f;
        }
        attackRange = 4.0f; // 원거리 평타 (항상 적용)
        mpRegenOnHit = 25f; // 4대 때리면 힐 가능 (항상 적용)

        // ★ 새로 추가되는 AI 스탯 (적의 어그로를 덜 끌도록 아주 낮게 설정)
        aggro = 5f;

        base.Start();
    }

    [Header("사제 패시브")]
    [Tooltip("전투 중 초당 차오르는 마나 (이동 중에도). 때릴 때 얻는 마나(mpRegenOnHit)와 별도")]
    public float mpRegenPerSecond = 10f;

    // 사제는 늘 적을 대상으로 잡고 때림 (예전엔 다친 아군 곁으로 가서 마나만 모았음 → 사제만 남으면 전투가 안 끝남)
    // 마나는 전투 중 저절로 차고, 가득 차 있고 다친 아군이 있으면 어디서든 바로 아군 전체 힐
    protected override void Update()
    {
        base.Update();
        if (isDead || isResting) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.isBattleActive) return;

        currentMp = Mathf.Min(currentMp + mpRegenPerSecond * Time.deltaTime, maxMp);
        if (currentMp >= maxMp && AnyAllyInjured()) UseSkill();
    }

    // 아군 전체 힐. 다친 아군이 없으면 마나를 아껴 두고 평타
    protected override void UseSkill()
    {
        if (!AnyAllyInjured())
        {
            Attack();
            return;
        }

        float healAmount = attackPower * 2.0f;
        foreach (BattleUnit unit in BattleUnit.Active)
        {
            if (unit == null || unit.isDead || !unit.CompareTag(gameObject.tag)) continue;
            float before = unit.currentHp;
            unit.currentHp = Mathf.Min(unit.currentHp + healAmount, unit.maxHp);

            // 실제로 회복된 만큼만 표시
            if (BattleManager.Instance != null && unit.currentHp > before)
                BattleManager.Instance.ShowHealText(unit.transform.position, unit.currentHp - before);
            unit.NotifyHealed(); // 겁쟁이 성격은 힐을 받으면 다시 싸움
        }
        Debug.Log($"✨ {name}의 파티 힐! 아군 전체 {healAmount} 회복");
        currentMp = 0;
    }

    bool AnyAllyInjured()
    {
        foreach (BattleUnit unit in BattleUnit.Active)
            if (unit != null && !unit.isDead && unit.CompareTag(gameObject.tag) && unit.currentHp < unit.maxHp) return true;
        return false;
    }
}
