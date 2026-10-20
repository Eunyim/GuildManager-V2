using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // TextMeshPro 사용 시 필수

// 신규 길드 신청서 화면: 길드 이름을 받아 새 게임 데이터를 만들고 로비로 보냄.
// 연출(서명·도장·화면 전환)은 GuildCreationScreen이 맡고, 없으면 바로 진행
public class GuildCreationManager : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public TMP_InputField nameInput; // 입력창 연결

    [Header("새 길드")]
    public int startingGold = 1000;     // 초기 자금
    public int maxNameLength = 12;      // 길드 이름 최대 글자 수

    [Header("[추천] 버튼이 고르는 길드 이름")]
    public string[] suggestedNames =
    {
        "새벽의 검", "은빛 늑대단", "황금 사자", "푸른 방패", "떠돌이 별", "붉은 매",
        "용감한 모험가들", "달빛 여관", "철의 맹세", "바람의 노래", "불꽃 망치", "고요한 숲"
    };

    bool committed;

    void Start()
    {
        if (nameInput != null) nameInput.characterLimit = maxNameLength;

        GuildCreationScreen screen = GuildCreationScreen.Instance;
        if (screen != null)
        {
            GameManager gm = GameManager.Instance;
            string date = gm != null ? $"{gm.month}월 {gm.week}주차" : "1월 1주차";
            int starters = gm != null && gm.starterJobs != null ? gm.starterJobs.Length : 0;
            screen.SetInfo(date, starters > 0 ? $"{startingGold:N0} G · 모험가 {starters}명" : $"{startingGold:N0} G");
        }
    }

    public void OnClickSign() // 버튼에 연결할 함수
    {
        if (committed) return;

        // 1. 유효성 검사: 이름이 비어있으면 안 됨
        string guildName = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(guildName))
        {
            Debug.Log("길드 이름을 입력해주세요!");
            if (GuildCreationScreen.Instance != null) GuildCreationScreen.Instance.ShowError("길드 이름을 적어 주세요!");
            return;
        }

        // 2. 서명 연출 뒤 길드 생성 (연출이 없으면 바로)
        if (GuildCreationScreen.Instance != null)
        {
            if (GuildCreationScreen.Instance.PlaySign(() => Commit(guildName))) committed = true;
        }
        else
        {
            committed = true;
            Commit(guildName);
        }
    }

    void Commit(string guildName)
    {
        // 데이터 저장 (싱글톤 GameManager 활용)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.guildName = guildName; // 이름 저장
            GameManager.Instance.gold = startingGold; // 초기 자금 지급
            GameManager.Instance.day = 1;     // 1일차 시작
            GameManager.Instance.GrantStarterAdventurers(); // 시작 모험가 지급
            GameManager.Instance.tutorial = TutorialProgress.NewGame(); // 로비에 들어가면 튜토리얼 시작
            GameManager.Instance.record = new GuildRecord();            // 새 판의 기록
            GameManager.Instance.equipmentStorage.Clear();
        }

        // 로비로 이동
        SceneManager.LoadScene("LobbyScene");
    }

    // [추천] 버튼: 길드 이름을 무작위로 채움 (지금 이름과 다른 것)
    public void OnClickSuggestName()
    {
        if (nameInput == null || suggestedNames == null || suggestedNames.Length == 0 || committed) return;
        string current = nameInput.text;
        string pick = current;
        for (int i = 0; i < 8 && pick == current; i++)
            pick = suggestedNames[Random.Range(0, suggestedNames.Length)];
        nameInput.text = pick;
        nameInput.caretPosition = pick.Length;
    }

    // [돌아가기] 버튼: 타이틀 화면으로
    public void OnClickBack()
    {
        if (committed) return;
        if (GuildCreationScreen.Instance != null)
        {
            if (GuildCreationScreen.Instance.PlayExit(() => SceneManager.LoadScene("TitleScene"))) committed = true;
        }
        else SceneManager.LoadScene("TitleScene");
    }
}
