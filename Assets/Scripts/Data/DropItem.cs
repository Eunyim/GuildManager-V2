using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct DropItem
{
    public MonsterDropData item;      // 떨굴 아이템
    [Range(0, 100)]
    public float dropRate;     // 확률 (0~100%)

    // 드랍 테이블을 한 번 굴려 당첨된 아이템을 전리품 가방에 넣음. 모든 적이 쓰러질 때 이것을 부름
    //  아이템마다 따로 굴림 (한 아이템이 빠져도 다른 아이템 확률은 그대로)
    //  재료 수급 의뢰 중이면 그 재료만 확률이 바뀜 (배율 상향 / 다 모으면 0 / 막판 보장 100) → BattleManager.GetDropRate
    public static void Roll(List<DropItem> table, string monsterName, BattleUnit source = null)
    {
        BattleManager bm = BattleManager.Instance;
        if (table != null)
        {
            foreach (DropItem loot in table)
            {
                if (loot.item == null) continue;
                float rate = bm != null ? bm.GetDropRate(loot.item, loot.dropRate, source) : loot.dropRate;
                if (rate <= 0f || Random.Range(0f, 100f) > rate) continue;
                Debug.Log($"💎 {monsterName}에게서 [{loot.item.itemName}] 획득!");
                if (bm != null) bm.AddItemToLootBag(loot.item);
            }
        }
        if (bm != null) bm.OnDropSourceDied(source);
    }
}
