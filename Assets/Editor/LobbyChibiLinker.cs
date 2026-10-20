using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > 로비 치비 외형 연결
///  - 로비 씬 LobbyManager의 직업별 치비 겉모습 칸에 전투용 모험가 프리팹(Prefab_Warrior 등)을 연결
///  - 로비 치비는 소환될 때 그 프리팹의 몸통·무기 그림을 그대로 씀 (LobbyChibiLook)
/// </summary>
public static class LobbyChibiLinker
{
    const string LOBBY_SCENE = "Assets/Scenes/LobbyScene.unity";
    const string DIR = "Assets/Prefabs/Characters/";

    [MenuItem("Tools/로비 치비 외형 연결")]
    public static void Link()
    {
        GameObject warrior = Load("Prefab_Warrior"), mage = Load("Prefab_Mage"), archer = Load("Prefab_Archer"),
                   healer = Load("Prefab_Healer"), rogue = Load("Prefab_Rogue");

        Scene lobby = SceneManager.GetSceneByPath(LOBBY_SCENE);
        bool openedHere = !lobby.isLoaded;
        if (openedHere) lobby = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Additive);

        int connected = 0;
        foreach (GameObject root in lobby.GetRootGameObjects())
        {
            foreach (LobbyManager lm in root.GetComponentsInChildren<LobbyManager>(true))
            {
                lm.chibiWarriorPrefab = warrior;
                lm.chibiMagePrefab    = mage;
                lm.chibiArcherPrefab  = archer;
                lm.chibiHealerPrefab  = healer;
                lm.chibiRoguePrefab   = rogue;
                EditorUtility.SetDirty(lm);
                connected++;
            }
        }

        if (connected == 0) Debug.LogWarning("[LobbyChibiLinker] 로비 씬에서 LobbyManager를 찾지 못했습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            Debug.Log($"[LobbyChibiLinker] LobbyManager {connected}개에 직업별 치비 겉모습 연결 (전사 {warrior != null}, 마법사 {mage != null}, 궁수 {archer != null}, 사제 {healer != null}, 도적 {rogue != null})");
        }
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
    }

    static GameObject Load(string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DIR + name + ".prefab");
        if (prefab == null) Debug.LogWarning($"[LobbyChibiLinker] {DIR}{name}.prefab 없음");
        return prefab;
    }
}
