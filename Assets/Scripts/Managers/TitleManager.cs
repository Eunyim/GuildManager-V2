using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동 필수 네임스페이스
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [Header("불러오기 버튼 (저장 파일이 없으면 비활성화)")]
    public Button loadButton;

    void Start()
    {
        if (loadButton != null) loadButton.interactable = SaveSystem.HasSave;
    }

    public void OnClickNewGame()
    {
        Debug.Log("새로운 길드를 창설합니다...");
        ExitThen(() => SceneManager.LoadScene("GuildCreationScene"));
    }

    // [불러오기] 버튼: 저장된 길드를 이어서 로비로
    public void OnClickLoadGame()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager가 없어 불러올 수 없습니다.");
            return;
        }
        if (!SaveSystem.TryLoad(out SaveData data))
        {
            Debug.LogWarning("불러올 저장 파일이 없습니다.");
            if (loadButton != null) loadButton.interactable = false;
            return;
        }

        ExitThen(() =>
        {
            GameManager.Instance.ApplySave(data);
            SceneManager.LoadScene("LobbyScene");
        });
    }

    public void OnClickQuit()
    {
        ExitThen(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    // 타이틀 연출이 있으면 화면을 어둡게 한 뒤 실행 (없으면 바로)
    static void ExitThen(System.Action action)
    {
        if (TitleScreen.Instance != null) TitleScreen.Instance.PlayExit(action);
        else action();
    }
}
