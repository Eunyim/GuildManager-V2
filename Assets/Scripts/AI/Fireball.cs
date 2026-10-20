using UnityEngine;

public class Fireball : MonoBehaviour
{
    private Vector3 targetPos;
    private float speed = 5.0f;
    private float damage;
    private string targetTag; // "Enemy" 또는 "Player"
    private bool isLaunched = false;
    private float explosionRadius = 0f; // 0이면 단일 타겟, 0보다 크면 광역
    private BattleUnit owner; // 쏜 유닛 (특성의 대상별 피해 배율용. 없으면 배율 없음)

    // 1. 발사 설정 (누가, 어디로, 얼마만큼, 폭발 반경)
    public void Setup(Vector3 _targetPos, float _damage, string _targetTag, float _explosionRadius = 0f, BattleUnit _owner = null)
    {
        owner = _owner;
        targetPos = _targetPos;
        damage = _damage;
        targetTag = _targetTag;
        explosionRadius = _explosionRadius;
        isLaunched = true;
        
        // 3초 뒤 자동 삭제 (못 맞췄을 경우 대비)
        Destroy(gameObject, 3.0f);
        
        // 발사 방향으로 회전
        Vector3 dir = (targetPos - transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void Update()
    {
        if (!isLaunched) return;

        // 2. 날아가기
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        // 3. 도착 체크 (거의 다 왔으면)
        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            Explode();
            Destroy(gameObject);
        }
    }

    // 광역 폭발 처리
    private void Explode()
    {
        if (explosionRadius <= 0f) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, explosionRadius);
        foreach (Collider2D col in hits)
        {
            if (!col.CompareTag(targetTag)) continue;
            BattleUnit unit = col.GetComponent<BattleUnit>();
            if (unit != null && !unit.isDead)
            {
                unit.TakeDamageFrom(owner, damage);
                Debug.Log($"💥 메테오 폭발! {unit.name}에게 {damage} 데미지");
            }
        }
    }

    // 4. 충돌 처리 (단일 타겟 — 광역이면 충돌 무시하고 도착 시 폭발)
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (explosionRadius > 0f) return; // 광역은 도착 시 Explode()로 처리

        if (collision.CompareTag(targetTag))
        {
            BattleUnit unit = collision.GetComponent<BattleUnit>();
            if (unit != null)
            {
                unit.TakeDamageFrom(owner, damage);
                Debug.Log($"💥 명중! {damage} 데미지");
            }
            Destroy(gameObject);
        }
    }
    
    // 3D일 경우를 대비해 3D 충돌도 추가
    void OnTriggerEnter(Collider collision)
    {
        if (explosionRadius > 0f) return;

        if (collision.CompareTag(targetTag))
        {
            BattleUnit unit = collision.GetComponent<BattleUnit>();
            if (unit != null)
            {
                unit.TakeDamageFrom(owner, damage);
            }
            Destroy(gameObject);
        }
    }
}