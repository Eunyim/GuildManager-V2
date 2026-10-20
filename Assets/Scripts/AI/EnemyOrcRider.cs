using UnityEngine;
using System.Collections.Generic;

// 오크 기수: 초원 늑대를 탄 오크. 빨라서 후열을 노리고 돌아 들어감
// HP가 0이 되면 오크는 낙마하고, 타고 있던 초원 늑대가 그 자리에서 계속 싸움
public class EnemyOrcRider : BattleUnit
{
    public override CombatRole Role => CombatRole.Flanker;

    [Header("오크 기수 전용")]
    public GameObject mountPrefab;       // 낙마 후 남는 초원 늑대 프리팹
    public float chargeMultiplier = 1.8f; // 스킬(돌진 찌르기) 배율

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"🏇 [오크 기수] 창 찌르기! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 돌진 찌르기
    protected override void UseSkill()
    {
        if (target != null && !target.isDead)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower * chargeMultiplier);
            Debug.Log($"🏇 [오크 기수] 돌진 찌르기! {victim.name}에게 {attackPower * chargeMultiplier:F0} 데미지!");
        }
        currentMp = 0;
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "오크 기수", this);
        Dismount(); // 적 수를 먼저 늘려 둠 (기수가 마지막 적이어도 늑대가 남아 있으므로 전투가 끝나지 않음)
        base.Die();
    }

    void Dismount()
    {
        if (mountPrefab == null) return;

        GameObject wolf = Instantiate(mountPrefab, transform.position, Quaternion.identity);
        wolf.tag = gameObject.tag;
        Vector3 scale = wolf.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(transform.localScale.x); // 기수가 보던 방향 그대로
        wolf.transform.localScale = scale;

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.ApplyQuestRankScaling(wolf);
            BattleManager.Instance.OnEnemyAdded();
        }
        Debug.Log($"🐺 [오크 기수] 낙마! 타고 있던 초원 늑대가 달려듭니다.");
    }
}
