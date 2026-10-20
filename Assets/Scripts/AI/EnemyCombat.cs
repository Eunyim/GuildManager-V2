using UnityEngine;
using System.Collections.Generic;

// 몬스터들이 함께 쓰는 공격 보조 기능
public static class EnemyCombat
{
    // 투사체(화살·작살 등)를 대상에게 날림. 투사체 프리팹이 없으면 바로 맞힘 (연결 누락 대비)
    public static void Shoot(BattleUnit shooter, BattleUnit target, GameObject projectilePrefab, float damage)
    {
        if (shooter == null || target == null || target.isDead) return;
        if (projectilePrefab == null)
        {
            target.TakeDamage(damage);
            return;
        }
        GameObject projectile = Object.Instantiate(projectilePrefab, shooter.transform.position, Quaternion.identity);
        Arrow proj = projectile.GetComponent<Arrow>();
        if (proj != null) proj.Setup(target.transform.position, damage, target.tag);
        else target.TakeDamage(damage);
    }

    // center에서 radius 안에 있는, self의 상대편 유닛 (범위 공격용)
    public static List<BattleUnit> OpponentsInRadius(BattleUnit self, Vector3 center, float radius)
    {
        var list = new List<BattleUnit>();
        if (self == null) return list;
        foreach (BattleUnit u in new List<BattleUnit>(BattleUnit.Active))
        {
            if (u == null || u.isDead || u.CompareTag(self.tag)) continue;
            if (Vector2.Distance(center, u.transform.position) <= radius) list.Add(u);
        }
        return list;
    }

    // self의 상대편 유닛 전부
    public static List<BattleUnit> AllOpponents(BattleUnit self) =>
        OpponentsInRadius(self, self != null ? self.transform.position : Vector3.zero, float.MaxValue);
}
