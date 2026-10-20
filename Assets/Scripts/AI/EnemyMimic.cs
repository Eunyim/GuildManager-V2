using UnityEngine;
using System.Collections.Generic;

// 보물상자로 위장해 있다가 상자를 열면 튀어나오는 적
// 스탯은 프리팹(Enemy_Mimic)의 직렬화 값을 그대로 씀
public class EnemyMimic : BattleUnit
{
    [Header("미믹 외형")]
    public Sprite idleSprite;          // 평소 모습 (입을 살짝 벌린 상자)
    public Sprite biteSprite;          // 물 때 모습 (입을 크게 벌린 상자)
    public float biteSpriteTime = 0.25f;

    [Header("드랍 아이템 설정")]
    public List<DropItem> dropTable;

    private SpriteRenderer spriteRenderer;
    private float biteEndTime;

    protected override void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && idleSprite != null) spriteRenderer.sprite = idleSprite;

        base.Start();
    }

    protected override void Update()
    {
        base.Update();

        if (spriteRenderer != null && idleSprite != null && biteEndTime > 0f && Time.time >= biteEndTime)
        {
            spriteRenderer.sprite = idleSprite;
            biteEndTime = 0f;
        }
    }

    protected override void Attack()
    {
        if (target != null)
        {
            BattleUnit victim = target;
            victim.TakeDamage(attackPower);
            Debug.Log($"📦 [미믹] 콱! {victim.name}에게 {attackPower} 데미지!");
        }

        if (spriteRenderer != null && biteSprite != null)
        {
            spriteRenderer.sprite = biteSprite;
            biteEndTime = Time.time + biteSpriteTime;
        }

        currentMp += mpRegenOnHit;
        if (currentMp > maxMp) currentMp = maxMp;
    }

    protected override void Die()
    {
        CheckDrop();
        base.Die();
    }

    void CheckDrop()
    {
        DropItem.Roll(dropTable, "미믹", this);
    }
}
