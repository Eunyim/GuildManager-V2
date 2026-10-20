using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// [메뉴] Tools > 종족별 드랍 아이템 연결
/// 종족별 전리품 아이템(MonsterDropData)을 만들고, 그 종족 몬스터 프리팹(일반·무리 구성원·보스)의 드랍 표에 넣습니다.
/// 로비 씬 상점(ShopPanel)의 판매 목록에도 추가해 상점에서 팔 수 있게 합니다.
///  - 초원 늑대: 가죽 10G, 송곳니 10G
///  - 스켈레톤: 뼛가루 15G, 스켈레톤의 갑옷 20G
///  - 도적단: 도적단의 증표 10G
///  - 고블린: 고블린 귀 15G, 고블린의 금목걸이 20G
///  - 오크: 오크의 도끼 20G, 오크의 깃발 25G
///  - 드래곤: 드래곤의 비늘 25G, 드래곤의 심장 50G (5%)
/// 드래곤의 심장 말고는 확률을 정하지 않아서 임시로: 기본 재료 50%, 비싼 재료 20~30% (아래 표에서 조정).
/// 슬라임(슬라임 점액)과 미믹은 건드리지 않습니다. 다시 실행해도 안전합니다 (드랍 표를 이 값으로 다시 씀).
/// </summary>
public class DropTableBuilder : Editor
{
    const string ITEM_FOLDER = "Assets/Data/Drops";
    const string CHARACTERS  = "Assets/Prefabs/Characters";
    const string ICON_SOURCE = "Assets/Data/Drops/Item_SlimeGoo.asset"; // 아이콘은 기존 아이템 것을 임시로 씀
    const string LOBBY_SCENE = "Assets/Scenes/LobbyScene.unity";

    struct Item { public string file, name, description; public int price; public float rate; }
    struct Race { public string name; public Item[] items; public string[] prefabs; }

    static readonly Race[] races =
    {
        new Race { name = "초원 늑대",
            items = new[] { new Item { file = "Item_WolfHide",  name = "가죽",   price = 10, rate = 50f, description = "초원 늑대의 질긴 가죽입니다." },
                            new Item { file = "Item_WolfFang",  name = "송곳니", price = 10, rate = 30f, description = "초원 늑대의 날카로운 송곳니입니다." } },
            prefabs = new[] { "Enemy_PrairieWolf", "Enemy_AlphaWolf" } },
        new Race { name = "스켈레톤",
            items = new[] { new Item { file = "Item_BoneDust",     name = "뼛가루",         price = 15, rate = 50f, description = "부서진 스켈레톤에서 나온 하얀 가루입니다." },
                            new Item { file = "Item_SkeletonArmor", name = "스켈레톤의 갑옷", price = 20, rate = 20f, description = "녹이 슬었지만 아직 쓸 만한 낡은 갑옷입니다." } },
            prefabs = new[] { "Enemy_Skeleton", "Enemy_SkeletonWarrior", "Enemy_SkeletonArcher", "Enemy_SkeletonTank", "Enemy_SkeletonNecromancer" } },
        new Race { name = "도적단",
            items = new[] { new Item { file = "Item_BanditToken", name = "도적단의 증표", price = 10, rate = 40f, description = "도적단원임을 나타내는 표식입니다." } },
            prefabs = new[] { "Enemy_Bandit", "Enemy_BanditWarrior", "Enemy_BanditArcher" } },
        new Race { name = "고블린",
            items = new[] { new Item { file = "Item_GoblinEar",      name = "고블린 귀",         price = 15, rate = 50f, description = "고블린 토벌의 증거로 쓰이는 뾰족한 귀입니다." },
                            new Item { file = "Item_GoblinNecklace", name = "고블린의 금목걸이", price = 20, rate = 20f, description = "고블린이 어디선가 훔쳐 온 금목걸이입니다." } },
            prefabs = new[] { "Enemy_Goblin", "Enemy_GoblinWarrior", "Enemy_GoblinHunter", "Enemy_GoblinPriest", "Enemy_GoblinShamanKing" } },
        new Race { name = "오크",
            items = new[] { new Item { file = "Item_OrcAxe",    name = "오크의 도끼", price = 20, rate = 30f, description = "오크가 쓰던 무거운 도끼입니다." },
                            new Item { file = "Item_OrcBanner", name = "오크의 깃발", price = 25, rate = 20f, description = "오크 부족의 문양이 그려진 깃발입니다." } },
            prefabs = new[] { "Enemy_Orc", "Enemy_OrcGrunt", "Enemy_OrcRider", "Enemy_OrcCenturion" } },
        new Race { name = "드래곤",
            items = new[] { new Item { file = "Item_DragonScale", name = "드래곤의 비늘", price = 25, rate = 50f, description = "단단하고 붉은 드래곤의 비늘입니다." },
                            new Item { file = "Item_DragonHeart", name = "드래곤의 심장", price = 50, rate = 5f,  description = "아직 온기가 남아 있는 드래곤의 심장입니다. 좀처럼 얻기 힘듭니다." } },
            prefabs = new[] { "Enemy_Dragon", "Enemy_DragonHatchling", "Enemy_BabyDragon" } },
    };

    [MenuItem("Tools/종족별 드랍 아이템 연결")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[DropTableBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }

        MonsterDropData iconSource = AssetDatabase.LoadAssetAtPath<MonsterDropData>(ICON_SOURCE);
        var allItems = new List<MonsterDropData>();
        int prefabCount = 0;

        foreach (Race race in races)
        {
            var table = new List<DropItem>();
            foreach (Item spec in race.items)
            {
                MonsterDropData item = EnsureItem(spec, iconSource);
                allItems.Add(item);
                table.Add(new DropItem { item = item, dropRate = spec.rate });
            }
            foreach (string prefab in race.prefabs)
                if (SetDropTable(prefab, table)) prefabCount++;
        }
        AssetDatabase.SaveAssets();

        AddToShop(allItems);
        Debug.Log($"=== 종족별 드랍 아이템 연결 완료! (아이템 {allItems.Count}개, 몬스터 프리팹 {prefabCount}개) ===");
    }

    static MonsterDropData EnsureItem(Item spec, MonsterDropData iconSource)
    {
        string path = $"{ITEM_FOLDER}/{spec.file}.asset";
        MonsterDropData item = AssetDatabase.LoadAssetAtPath<MonsterDropData>(path);
        if (item == null)
        {
            item = CreateInstance<MonsterDropData>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.itemName    = spec.name;
        item.type        = ItemType.Resource;
        item.price       = spec.price;
        item.description = spec.description;
        if (item.icon == null && iconSource != null) item.icon = iconSource.icon;
        EditorUtility.SetDirty(item);
        return item;
    }

    // 프리팹 스크립트의 dropTable 칸에 표를 넣음 (스크립트마다 dropTable이 따로 선언되어 있어 리플렉션으로)
    static bool SetDropTable(string prefabFile, List<DropItem> table)
    {
        string path = $"{CHARACTERS}/{prefabFile}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[DropTableBuilder] {path} 없음"); return false; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        bool done = false;
        foreach (MonoBehaviour mb in root.GetComponents<MonoBehaviour>())
        {
            var field = mb != null ? mb.GetType().GetField("dropTable") : null;
            if (field == null) continue;
            field.SetValue(mb, new List<DropItem>(table));
            done = true;
        }
        if (done) PrefabUtility.SaveAsPrefabAsset(root, path);
        else Debug.LogWarning($"[DropTableBuilder] {prefabFile}에 드랍 표(dropTable)가 없습니다.");
        PrefabUtility.UnloadPrefabContents(root);
        return done;
    }

    static void AddToShop(List<MonsterDropData> items)
    {
        Scene lobby = SceneManager.GetSceneByPath(LOBBY_SCENE);
        bool openedHere = !lobby.isLoaded;
        if (openedHere) lobby = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Additive);

        int shops = 0;
        foreach (GameObject go in lobby.GetRootGameObjects())
        {
            foreach (ShopPanel shop in go.GetComponentsInChildren<ShopPanel>(true))
            {
                if (shop.catalog == null) shop.catalog = new List<MonsterDropData>();
                foreach (MonsterDropData item in items)
                    if (item != null && !shop.catalog.Contains(item)) shop.catalog.Add(item);
                EditorUtility.SetDirty(shop);
                shops++;
            }
        }

        if (shops == 0) Debug.LogWarning("[DropTableBuilder] 로비 씬에서 상점(ShopPanel)을 찾지 못했습니다. 새 아이템을 상점에서 팔 수 없습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            Debug.Log($"[DropTableBuilder] 상점 {shops}곳의 판매 목록에 아이템 {items.Count}개 추가");
        }
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
    }
}
