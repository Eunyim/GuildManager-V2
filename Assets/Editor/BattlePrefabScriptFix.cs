using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// [메뉴] Tools > 전투 프리팹 스크립트 연결 고치기
/// 전투 프리팹에 잘못 붙어 있는 스크립트를 전용 스크립트로 바꿉니다.
///  - 적 프리팹(고블린/스켈레톤/오크/도적단)이 전부 EnemySlime을 쓰고 있어, 스탯·스킬이 모두 슬라임과 같았습니다.
///  - 파이어볼 프리팹이 Arrow 스크립트를 쓰고 있어, 마법사의 화염구가 발사되지 않았습니다.
/// 스크립트만 바꾸므로 체력바, 드랍 테이블 등 이름이 같은 설정값은 그대로 유지됩니다.
/// 이미 올바른 프리팹은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class BattlePrefabScriptFix : Editor
{
    const string CHARACTER_FOLDER = "Assets/Prefabs/Characters";
    const string OBJECT_FOLDER    = "Assets/Prefabs/Objects";
    const string SCRIPT_FOLDER    = "Assets/Scripts/AI";
    const string FIREBALL_PREFAB  = OBJECT_FOLDER + "/Prefab_Fireball.prefab";

    // (프리팹 파일, 써야 하는 스크립트)
    static readonly (string prefab, string script)[] enemyScripts =
    {
        ("Enemy_Goblin.prefab",   "EnemyGoblin"),
        ("Enemy_Skeleton.prefab", "EnemySkeleton"),
        ("Enemy_Orc.prefab",      "EnemyOrc"),
        ("Enemy_Bandit.prefab",   "EnemyBandit"),
    };

    [MenuItem("Tools/전투 프리팹 스크립트 연결 고치기")]
    static void Fix()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[BattlePrefabScriptFix] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        int changed = 0;

        // 1. 적 프리팹: EnemySlime -> 전용 스크립트
        foreach (var entry in enemyScripts)
            changed += SwapScript<BattleUnit>($"{CHARACTER_FOLDER}/{entry.prefab}", entry.script);

        // 2. 파이어볼 프리팹: Arrow -> Fireball
        changed += SwapScript<MonoBehaviour>(FIREBALL_PREFAB, "Fireball");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. 드래곤은 전용 스크립트가 없어 EnemySlime을 임시로 쓰는 중 → 슬라임처럼 분열하지는 않게 함
        changed += DisableSplit($"{CHARACTER_FOLDER}/Enemy_Dragon.prefab");

        AssetDatabase.SaveAssets();
        Debug.Log($"=== 전투 프리팹 스크립트 연결 고치기 완료! ({changed}건 변경) ===");
    }

    // 프리팹 루트에 붙은 T 타입 컴포넌트의 스크립트를 scriptName으로 교체. 바꿨으면 1
    static int SwapScript<T>(string prefabPath, string scriptName) where T : MonoBehaviour
    {
        if (!File.Exists(prefabPath))
        {
            Debug.LogWarning($"[BattlePrefabScriptFix] 프리팹을 찾을 수 없습니다: {prefabPath}");
            return 0;
        }

        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>($"{SCRIPT_FOLDER}/{scriptName}.cs");
        if (script == null)
        {
            Debug.LogWarning($"[BattlePrefabScriptFix] 스크립트를 찾을 수 없습니다: {scriptName}.cs");
            return 0;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            // 화살/파이어볼처럼 여러 MonoBehaviour가 있을 수 있으므로, 전투 관련 스크립트(AI 폴더)만 대상으로 함
            T component = null;
            foreach (T candidate in root.GetComponents<T>())
            {
                if (candidate == null) continue;
                string path = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(candidate));
                if (path.StartsWith(SCRIPT_FOLDER)) { component = candidate; break; }
            }
            if (component == null)
            {
                Debug.LogWarning($"[BattlePrefabScriptFix] {Path.GetFileName(prefabPath)}: 바꿀 전투 스크립트를 찾지 못했습니다.");
                return 0;
            }

            string current = component.GetType().Name;
            if (current == scriptName) return 0;

            SerializedObject so = new SerializedObject(component);
            so.FindProperty("m_Script").objectReferenceValue = script;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[BattlePrefabScriptFix] {Path.GetFileName(prefabPath)}: {current} → {scriptName}");
            return 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static int DisableSplit(string prefabPath)
    {
        if (!File.Exists(prefabPath)) return 0;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            EnemySlime slime = root.GetComponent<EnemySlime>();
            if (slime == null || slime.maxSplitCount == 0) return 0;

            slime.maxSplitCount = 0;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[BattlePrefabScriptFix] {Path.GetFileName(prefabPath)}: 분열 끔 (maxSplitCount = 0)");
            return 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
