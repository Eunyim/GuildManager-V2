using UnityEngine;

public class TreasureChest : MonoBehaviour
{
    [Header("미믹 설정")]
    [Range(0f, 1f)]
    public float mimicChance = 0.15f; // 미믹 확률 (기본 15%)
    public GameObject mimicEnemyPrefab; // 미믹으로 소환할 적 프리팹 (인스펙터 연결)

    [HideInInspector] public bool canBeMimic = true; // 보스방 상자처럼 미믹이 나오면 안 되는 곳에서 false로 설정

    private bool isOpened = false;
    private bool isMimic  = false;

    private void Start()
    {
        // 스폰 시 미믹 여부 결정
        isMimic = canBeMimic && (Random.value < mimicChance);
        if (isMimic)
            Debug.Log("👀 [미믹] 보물상자가 미믹이었다!");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isOpened) return;
        if (!collision.CompareTag("Player")) return;

        isOpened = true;

        // mimicEnemyPrefab이 없으면 현재 스테이지 적 목록에서 자동으로 가져옴
        GameObject prefabToSpawn = mimicEnemyPrefab;
        if (prefabToSpawn == null && BattleManager.Instance != null)
        {
            var stages = BattleManager.Instance.dungeonStages;
            if (stages != null && stages.Count > 0)
            {
                int idx = Mathf.Clamp(BattleManager.Instance.currentStageIndex, 0, stages.Count - 1);
                var prefabs = stages[idx].enemyPrefabs;
                if (prefabs != null && prefabs.Count > 0)
                    prefabToSpawn = prefabs[Random.Range(0, prefabs.Count)];
            }
        }

        if (isMimic && prefabToSpawn != null)
        {
            // 미믹 발동: 적 소환
            Debug.Log("💀 [미믹 발동] 상자가 열리자 적이 튀어나왔다!");
            // 상자와 미믹은 이빨·혀 말고는 같은 그림이므로, 상자 자리에서 그대로 입을 벌리는 것처럼 같은 위치에 소환
            Vector3 spawnPos = transform.position;
            GameObject mimic = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            mimic.tag = "Enemy";
            EnemySlime.LinkPrefab(mimic, prefabToSpawn);

            // 신중한 파티원이 미리 알아챘으면 미믹이 기절한 채로 나옴
            if (BattleManager.Instance != null && BattleManager.Instance.TryDetectMimic())
            {
                BattleUnit unit = mimic.GetComponent<BattleUnit>();
                if (unit != null) unit.stunOnStart = GameManager.Instance.personality.carefulMimicStun;
                BattleManager.Instance.ShowStatusText(spawnPos, "!");
            }

            // 배틀매니저에 적 추가 알림
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.ApplyQuestRankScaling(mimic);
                BattleManager.Instance.OnMimicSpawned();
            }

            Destroy(gameObject);
        }
        else
        {
            // 일반 상자
            Debug.Log($"🎁 {collision.name}가 보물상자를 열었습니다!");
            if (BattleManager.Instance != null)
                BattleManager.Instance.OnChestOpened();
            Destroy(gameObject);
        }
    }
}
