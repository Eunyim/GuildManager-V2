using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// [메뉴] Tools > 던전 프리팹 크기 맞추기
/// 아이소메트릭 던전(바닥 한 칸 = 가로 1 x 세로 0.5)에 맞게 전투 프리팹의 크기를 줄입니다.
/// 모험가 / 몬스터 / 보물상자 / 투사체 프리팹의 루트 스케일을 아래 값으로 설정합니다.
/// 절대값으로 설정하므로 여러 번 실행해도 결과가 같고, 이후 프리팹에서 직접 조정해도 됩니다.
/// (유닛의 좌우 방향 전환은 프리팹 크기를 유지하도록 BattleUnit.SetFacing이 처리합니다)
/// </summary>
public class DungeonPrefabScaleSetup : Editor
{
    const string CHARACTER_FOLDER = "Assets/Prefabs/Characters";
    const string OBJECT_FOLDER    = "Assets/Prefabs/Objects";

    // 모험가(캡슐, 원래 가로 1 x 세로 2) → 가로 0.5 x 세로 1: 바닥 한 칸에 서는 크기
    static readonly Vector3 allyScale  = new Vector3(0.5f, 0.5f, 1f);
    // 몬스터(사각, 원래 1 x 1) → 0.6 x 0.6
    static readonly Vector3 enemyScale = new Vector3(0.6f, 0.6f, 1f);

    // 그 밖의 오브젝트 (원래 값의 절반)
    static readonly (string file, Vector3 scale)[] objectScales =
    {
        ("Prefab_TreasureChest.prefab", new Vector3(0.75f, 0.4f, 1f)),   // 원래 1.5 x 0.8
        ("Prefab_Arrow.prefab",         new Vector3(0.4f, 0.1f, 1f)),    // 원래 0.8 x 0.2
        ("Prefab_Fireball.prefab",      new Vector3(0.75f, 0.75f, 1f)),  // 원래 1.5 x 1.5
    };

    [MenuItem("Tools/던전 프리팹 크기 맞추기")]
    static void Apply()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[DungeonPrefabScaleSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        int count = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CHARACTER_FOLDER }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileName(path);

            // 이름 규칙: Enemy_* = 몬스터, Prefab_* = 모험가
            if (fileName.StartsWith("Enemy_"))       count += SetRootScale(path, enemyScale);
            else if (fileName.StartsWith("Prefab_")) count += SetRootScale(path, allyScale);
            else Debug.Log($"[DungeonPrefabScaleSetup] 이름 규칙에 맞지 않아 건너뜀: {fileName}");
        }

        foreach (var entry in objectScales)
            count += SetRootScale($"{OBJECT_FOLDER}/{entry.file}", entry.scale);

        AssetDatabase.SaveAssets();
        Debug.Log($"=== 던전 프리팹 크기 맞추기 완료! ({count}개 변경) ===\n" +
                  $"모험가 {allyScale.x}, 몬스터 {enemyScale.x} (루트 스케일). 더 조정하려면 각 프리팹의 Scale을 바꾸세요.");
    }

    // 프리팹 루트의 스케일을 설정. 바뀌었으면 1, 아니면 0
    static int SetRootScale(string path, Vector3 scale)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[DungeonPrefabScaleSetup] 프리팹을 찾을 수 없습니다: {path}");
            return 0;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.localScale == scale) return 0;

            Debug.Log($"[DungeonPrefabScaleSetup] {Path.GetFileName(path)}: {root.transform.localScale} → {scale}");
            root.transform.localScale = scale;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
