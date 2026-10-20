using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;

/// <summary>
/// [메뉴] Tools > 타이틀 불러오기 버튼 연결
/// TitleScene의 [불러오기] 버튼(LoadGame)을 TitleManager.OnClickLoadGame에 연결하고,
/// 저장 파일이 없을 때 버튼이 꺼지도록 TitleManager.loadButton에 등록합니다.
/// 이미 연결돼 있으면 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class TitleLoadButtonSetup : Editor
{
    const string BUTTON_NAME = "LoadGame";
    const string UNDO_NAME   = "타이틀 불러오기 버튼 연결";

    [MenuItem("Tools/타이틀 불러오기 버튼 연결")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[TitleLoadButtonSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        TitleManager title = Object.FindFirstObjectByType<TitleManager>();
        if (title == null)
        {
            Debug.LogError("[TitleLoadButtonSetup] TitleManager를 찾을 수 없습니다. TitleScene을 열고 실행해주세요.");
            return;
        }

        Button button = null;
        foreach (Button candidate in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate.name == BUTTON_NAME) { button = candidate; break; }
        if (button == null)
        {
            Debug.LogError($"[TitleLoadButtonSetup] '{BUTTON_NAME}' 버튼을 찾을 수 없습니다.");
            return;
        }

        bool alreadyLinked = false;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == title
                && button.onClick.GetPersistentMethodName(i) == nameof(TitleManager.OnClickLoadGame))
                alreadyLinked = true;

        if (!alreadyLinked)
        {
            Undo.RecordObject(button, UNDO_NAME);
            UnityEventTools.AddPersistentListener(button.onClick, title.OnClickLoadGame);
            EditorUtility.SetDirty(button);
        }

        Undo.RecordObject(title, UNDO_NAME);
        title.loadButton = button;
        EditorUtility.SetDirty(title);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(title.gameObject.scene);
        Debug.Log("=== 타이틀 불러오기 버튼 연결 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }
}
