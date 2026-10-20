using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

/// <summary>
/// [메뉴] Tools > 초원 늑대 프리팹 만들기
/// D랭크 몬스터 '초원 늑대'를 만듭니다.
///  - 쓸 만한 늑대 스프라이트가 없어 40x26 임시 도트를 코드로 그려 PNG로 저장하고 (실행할 때마다 다시 그림),
///  - 고블린 프리팹을 본떠 Enemy_PrairieWolf 프리팹을 만들고,
///  - 로비 씬 QuestGenerator의 D랭크 적 목록(tierD_Prefabs)과 무리 목록(packs, D3/C3/B4/A4/S5)에 넣습니다.
/// 이미 있으면 스탯·연결만 다시 맞추므로 다시 실행해도 안전합니다.
/// </summary>
public class PrairieWolfBuilder : Editor
{
    const string TEMPLATE_PREFAB = "Assets/Prefabs/Characters/Enemy_Goblin.prefab";
    const string WOLF_PREFAB     = "Assets/Prefabs/Characters/Enemy_PrairieWolf.prefab";
    const string SPRITE_PATH     = "Assets/Art/Characters/PrairieWolf/prairie_wolf.png";
    const string LOBBY_SCENE     = "Assets/Scenes/LobbyScene.unity";

    // 초원 늑대 스탯 (고블린 HP 60 / 공격 16 / 이동 3.0 / 간격 0.8 보다 조금 약하고 조금 빠름. 대신 출혈)
    const float HP = 55f, ATK = 12f, RANGE = 0.8f, SPEED = 3.4f, COOLDOWN = 0.9f;

    // 40x26 임시 도트 (오른쪽을 봄). 위에서부터 한 줄씩. 낮춘 머리, 드러낸 송곳니, 붉은 눈, 곤두선 등털, 짙은 등(안장)·옅은 배
    // 크기: 40x26px / PPU 16 x 스케일 0.6 = 화면 1.5 x 0.98 (아군 캡슐 0.5 x 1.0 기준, 어깨가 사람 가슴께)
    const int SPRITE_W = 40, SPRITE_H = 26;
    static readonly string[] pixels =
    {
        "........................................",
        "........................................",
        "....................K..K.....K..........",
        ".................K.KK.KK..K..KK.........",
        "................KK.KDKDDKKK.KDMK........",
        "..............K.KDKDDDDDDDDKKDMMKKK.....",
        ".............KDKDDDDDDDDDDDKDMMMMMMKK...",
        "............KDDDDDDDDDDDDDKDMMMMMMMMMKK.",
        ".......KKKKKDDDDDDDDDDDDDDDMMMKKKMMMMMMK",
        ".....KKDDDDDDDDDDDDDDDDDDDDMMMEEKMMMMMMN",
        "...KKDDDDDDDDDDDDDDDDDDDDDDMMMMMMMMMMMKK",
        "..KDDDDMMMMMMMMMMMMMMMMMMMMMMMMMMKRWRRWK",
        ".KDDGGGGGMMMMMMMMMMMMMMMMMLLMMMMKRRRRRK.",
        "KDDMMMMMMMMMMMMDMMMMMMMMMMLLLMMMKWRRWK..",
        "KDDMMMMMMMMMMMMMDMMMMMMMMMKLLLKKKKKKK...",
        "KKDKKKGGGGGGGGGGDGGGGGGGGGLLLK..........",
        "..K...KGGGGGGGGGDGGGGGGGGGGMLK..........",
        ".......KGGGGGGGGKKKKKGGGGGGMK...........",
        "........KGGGGGGK.....KMMMMMMK...........",
        "........KGGKGGK......KMMMKMMMK..........",
        "........KGK.KGK......KMMK.KMMK..........",
        "........KLK.KLK......KGGK.KMMK..........",
        "........KLK.KLLK.....KGGK..KMMK.........",
        ".......KLLK.KLLK....KGGGK..KMMMK........",
        ".......CKCK.CKCK....CKCKC..CKCKC........",
        "........................................",
    };

    static Color PixelColor(char c)
    {
        switch (c)
        {
            case 'K': return new Color32(24, 20, 26, 255);    // 외곽선
            case 'D': return new Color32(58, 52, 54, 255);    // 짙은 털 (등·갈기·꼬리 끝)
            case 'M': return new Color32(102, 92, 84, 255);   // 중간 털
            case 'G': return new Color32(140, 128, 112, 255); // 밝은 털
            case 'L': return new Color32(188, 178, 158, 255); // 배·가슴·다리 아래
            case 'E': return new Color32(236, 44, 30, 255);   // 붉은 눈
            case 'W': return new Color32(244, 240, 226, 255); // 송곳니
            case 'R': return new Color32(112, 20, 26, 255);   // 입속
            case 'N': return new Color32(10, 8, 10, 255);     // 코
            case 'C': return new Color32(214, 206, 190, 255); // 발톱
            default:  return new Color(0f, 0f, 0f, 0f);
        }
    }

    [MenuItem("Tools/초원 늑대 프리팹 만들기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[PrairieWolfBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }

        Sprite sprite = EnsureSprite();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(WOLF_PREFAB) == null && !AssetDatabase.CopyAsset(TEMPLATE_PREFAB, WOLF_PREFAB))
        {
            Debug.LogError($"[PrairieWolfBuilder] {TEMPLATE_PREFAB} 복사 실패");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(WOLF_PREFAB);
        root.name = "Enemy_PrairieWolf";
        root.tag  = "Enemy";

        // 고블린 스크립트 → 늑대 스크립트 (체력바 프리팹은 옮겨 담음)
        BattleUnit old = root.GetComponent<BattleUnit>();
        GameObject hpBar = old != null ? old.hpBarPrefab : null;
        if (old != null && !(old is EnemyPrairieWolf)) DestroyImmediate(old);
        EnemyPrairieWolf wolf = root.GetComponent<EnemyPrairieWolf>();
        if (wolf == null) wolf = root.AddComponent<EnemyPrairieWolf>();
        if (hpBar != null) wolf.hpBarPrefab = hpBar;

        wolf.maxHp          = HP;
        wolf.attackPower    = ATK;
        wolf.attackRange    = RANGE;
        wolf.moveSpeed      = SPEED;
        wolf.attackCooldown = COOLDOWN;
        wolf.aggro          = 10f;
        wolf.mpRegenOnHit   = 20f;  // 평타 5번으로 마나가 차면 스킬(출혈 공격)
        if (wolf.dropTable == null) wolf.dropTable = new System.Collections.Generic.List<DropItem>(); // 늑대 전용 전리품은 아직 없음

        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (sprite != null) sr.sprite = sprite;
            sr.color = Color.white; // 고블린 임시 스프라이트 색조 제거
        }

        // 충돌 박스를 덩치에 맞춤 (로컬 2.2 x 1.3 → 화면 1.32 x 0.78)
        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) { col.size = new Vector2(2.2f, 1.3f); col.offset = Vector2.zero; }
        root.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        PrefabUtility.SaveAsPrefabAsset(root, WOLF_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        ConnectQuestGenerator();
        Debug.Log("=== 초원 늑대 프리팹 만들기 완료! (D랭크 적 목록 연결) ===");
    }

    // 도트를 PNG로 저장하고 도트 설정(PPU 16, Point, 무압축)으로 가져옴
    static Sprite EnsureSprite()
    {
        // 도트를 고치면 반영되도록 매번 다시 그림
        {
            Texture2D tex = new Texture2D(SPRITE_W, SPRITE_H, TextureFormat.RGBA32, false);
            for (int row = 0; row < SPRITE_H; row++)
                for (int col = 0; col < SPRITE_W; col++)
                    tex.SetPixel(col, SPRITE_H - 1 - row, PixelColor(pixels[row][col])); // 텍스처는 아래가 y=0
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(SPRITE_PATH));
            File.WriteAllBytes(SPRITE_PATH, tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SPRITE_PATH);
        }

        TextureImporter importer = AssetImporter.GetAtPath(SPRITE_PATH) as TextureImporter;
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PATH);
        if (sprite == null) Debug.LogWarning("[PrairieWolfBuilder] 늑대 스프라이트를 만들지 못했습니다. 고블린 스프라이트로 남습니다.");
        return sprite;
    }

    static void ConnectQuestGenerator()
    {
        GameObject wolfAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WOLF_PREFAB);
        Scene lobby = SceneManager.GetSceneByPath(LOBBY_SCENE);
        bool openedHere = !lobby.isLoaded;
        if (openedHere) lobby = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Additive);

        int connected = 0;
        foreach (GameObject go in lobby.GetRootGameObjects())
        {
            foreach (QuestGenerator generator in go.GetComponentsInChildren<QuestGenerator>(true))
            {
                if (!generator.tierD_Prefabs.Contains(wolfAsset)) generator.tierD_Prefabs.Add(wolfAsset);
                if (generator.packs == null) generator.packs = new System.Collections.Generic.List<EnemyPack>();
                if (!generator.packs.Exists(pk => pk != null && pk.prefab == wolfAsset))
                    generator.packs.Add(new EnemyPack { prefab = wolfAsset, sizePerRank = new[] { 3, 3, 4, 4, 5 } });
                EditorUtility.SetDirty(generator);
                connected++;
            }
        }

        if (connected == 0) Debug.LogWarning("[PrairieWolfBuilder] 로비 씬에서 QuestGenerator를 찾지 못했습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            Debug.Log($"[PrairieWolfBuilder] QuestGenerator {connected}개의 D랭크 적 목록·무리 목록에 초원 늑대 추가");
        }
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
    }
}
