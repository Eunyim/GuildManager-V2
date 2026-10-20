using UnityEngine;
using System.Collections.Generic;

// 스켈레톤 네크로맨서 (스켈레톤 보스): 뒤에서 저주를 쏘고, 마나가 차면 땅속에서 스켈레톤 전사를 불러냄
// 마나 통 150 (보통의 1.5배). 스탯은 프리팹(Enemy_SkeletonNecromancer)의 직렬화 값을 그대로 씀
public class EnemySkeletonNecromancer : BattleUnit
{
    // 전투 성향 — 지원: 적이 다가오면 불러낸 스켈레톤 뒤로 피함
    public override CombatRole Role => CombatRole.Support;

    [Header("네크로맨서 전용 — 스킬: 스켈레톤 전사 소환")]
    public GameObject summonPrefab;        // 불러낼 스켈레톤 전사
    public int summonCount = 2;            // 한 번에 불러내는 수
    public int maxAliveSummons = 6;        // 불러낸 스켈레톤이 이만큼 살아 있으면 더 부르지 않음 (끝없이 늘어나지 않게)
    public float summonRadius = 1.2f;      // 네크로맨서 주변 이 거리 안에서 솟아오름
    public float mpRegenPerSecond = 5f;    // 전투 중 저절로 차는 마나 (뒤에 숨어 있어도 소환할 수 있게)

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    private readonly List<BattleUnit> summons = new List<BattleUnit>();

    protected override void Update()
    {
        base.Update();
        if (isDead || isResting) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.isBattleActive) return;

        currentMp = Mathf.Min(currentMp + mpRegenPerSecond * Time.deltaTime, maxMp);
        if (currentMp >= maxMp) UseSkill();
    }

    // 평타: 저주 (원거리에서 바로 맞힘)
    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"💀 [스켈레톤 네크로맨서] 저주! {victim.name}에게 {attackPower:F0} 데미지!");
        }
        currentMp = Mathf.Min(currentMp + mpRegenOnHit, maxMp);
    }

    // 스킬: 땅속에서 스켈레톤 전사 소환
    protected override void UseSkill()
    {
        currentMp = 0;
        summons.RemoveAll(s => s == null || s.isDead);
        int count = Mathf.Min(summonCount, maxAliveSummons - summons.Count);
        if (summonPrefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * summonRadius;
            GameObject skeleton = Instantiate(summonPrefab, transform.position + new Vector3(offset.x, offset.y * 0.5f, 0f), Quaternion.identity);
            skeleton.tag = gameObject.tag;
            Vector3 scale = skeleton.transform.localScale;
            scale.x = -Mathf.Abs(scale.x); // 모험가 쪽(왼쪽)을 봄
            skeleton.transform.localScale = scale;

            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.ApplyQuestRankScaling(skeleton);
                BattleManager.Instance.OnEnemyAdded();
            }
            BattleUnit unit = skeleton.GetComponent<BattleUnit>();
            if (unit != null) summons.Add(unit);
        }
        Debug.Log($"💀 [스켈레톤 네크로맨서] 땅속에서 스켈레톤 전사 {count}마리를 불러냈다!");
    }

    protected override void Die()
    {
        DropItem.Roll(dropTable, "스켈레톤 네크로맨서", this);
        base.Die();
    }
}
