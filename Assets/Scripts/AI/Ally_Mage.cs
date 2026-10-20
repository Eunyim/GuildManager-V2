using UnityEngine;

public class Ally_Mage : BattleUnit
{
    // 전투 성향 — 원거리: 적과 거리를 유지하며 쏨
    public override CombatRole Role => CombatRole.Ranged;

    [Header("마법사 전용")]
    public GameObject fireballPrefab; 

    protected override void Start()
    {
        // Initialize()가 호출되지 않은 경우(테스트 모드)에만 기본값 적용
        if (!isInitialized)
        {
            maxHp = 60f;
            attackPower = 10f;
            moveSpeed = 2.0f;
        }
        maxMp = 80f;
        attackRange = 5.5f;
        mpRegenOnHit = 20f;

        base.Start();
    }

    protected override void Attack()
    {
        base.Attack();
    }

    protected override void UseSkill()
    {
        Debug.Log($"🔥 {name}의 메테오 스트라이크!");

        if (target == null || fireballPrefab == null) return;

        GameObject fireball = Instantiate(fireballPrefab, transform.position, Quaternion.identity);
        
        Fireball proj = fireball.GetComponent<Fireball>();
        if (proj != null)
        {
            // 메테오 스트라이크: 광역 폭발 (반경 2.0)
            proj.Setup(target.transform.position, attackPower * 2.5f, target.tag, 2.0f, this);
        }

        currentMp = 0;
    }
}