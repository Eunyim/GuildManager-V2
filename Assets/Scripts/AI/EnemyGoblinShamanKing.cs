using UnityEngine;

// 샤먼킹 (고블린 보스): 고블린 무리와 함께 나와 고블린 사제 대신 무리를 지휘함
//  - 축복: 사제보다 강함 (기본 공격력 +35% 8초, 사제는 +20% 6초)
//  - 오라: 살아 있는 동안 주변 고블린이 받는 피해 감소 (GoblinUnit.TakeDamage)
//  - 쓰러지면 사제와 똑같이 남은 고블린이 명령체계를 잃음 (EnemyGoblinPriest를 물려받음)
public class EnemyGoblinShamanKing : EnemyGoblinPriest
{
    [Header("샤먼킹 전용 — 오라")]
    public float auraRadius = 5f;                         // 이 거리 안의 고블린에게 오라
    [Range(0f, 0.9f)] public float auraDamageReduction = 0.2f; // 오라를 받는 고블린이 받는 피해 감소 비율

    // 이 위치에 샤먼킹 오라가 닿으면 감소 비율, 아니면 0 (가장 센 오라 하나만)
    public static float AuraReductionAt(GoblinUnit goblin)
    {
        float best = 0f;
        foreach (BattleUnit u in Active)
        {
            if (!(u is EnemyGoblinShamanKing king) || king.isDead || !king.CompareTag(goblin.tag)) continue;
            if (Vector2.Distance(king.transform.position, goblin.transform.position) <= king.auraRadius)
                best = Mathf.Max(best, king.auraDamageReduction);
        }
        return best;
    }
}
