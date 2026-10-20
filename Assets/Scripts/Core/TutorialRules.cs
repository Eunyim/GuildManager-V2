using System;
using System.Collections.Generic;

// 튜토리얼: 진행 상태(저장 파일에 들어감)와 안내 문구.
// 언제 다음 단계로 넘어갈지, 화면의 무엇을 가리킬지는 TutorialManager(Runtime)가 판정합니다.

// 기본 튜토리얼 단계. 저장 파일에 숫자로 들어가므로 중간에 끼워 넣지 말고 뒤에 추가할 것
public enum TutorialStep
{
    Welcome,        // 길드 마스터 환영, 상단 정보줄
    VisitReception, // 접수처 열기
    ReceptionGuide, // 접수처 화면 설명 → 닫기
    MakeParty,      // 파티 만들고 모험가 넣기
    AssignRoom,     // 2층 여관 방에 파티 배정
    AcceptQuest,    // 퀘스트 보드에서 의뢰를 받아 파견
    Battle,         // 던전: 자동 전투 설명
    Campfire,       // 모닥불: 지도·방향·귀환
    FinishDungeon,  // 보스방까지 가거나 귀환
    Result,         // 결과창 설명 → 결산 확인
    Town,           // 길드 밖 4개 건물
    PassWeek,       // 한 주 보내기, 월급
    Done
}

[Serializable]
public class TutorialProgress
{
    public bool active;          // 기본 튜토리얼 진행 중
    public int step;             // TutorialStep
    public bool introShown;      // 지금 단계의 설명 창을 이미 봤는가
    public bool tipsEnabled = true; // 상황별 도움말을 띄울지
    public List<string> seenTips = new List<string>();

    public TutorialStep Step
    {
        get => (TutorialStep)step;
        set { step = (int)value; introShown = false; }
    }

    public static TutorialProgress NewGame() => new TutorialProgress { active = true, step = (int)TutorialStep.Welcome };

    // 예전 저장 파일이나 튜토리얼 없이 시작한 경우: 기본 튜토리얼은 끝난 것으로 보고 상황별 도움말만 띄움
    public static TutorialProgress Finished() => new TutorialProgress { active = false, step = (int)TutorialStep.Done };

    public bool HasSeen(string id) => seenTips != null && seenTips.Contains(id);

    public void MarkSeen(string id)
    {
        if (seenTips == null) seenTips = new List<string>();
        if (!seenTips.Contains(id)) seenTips.Add(id);
    }

    public void Skip()
    {
        active = false;
        step = (int)TutorialStep.Done;
    }
}

// 설명 창 한 장
public class TutorialPage
{
    public string title;
    public string body;
    public TutorialPage(string title, string body) { this.title = title; this.body = body; }
}

public class TutorialStepInfo
{
    public TutorialStep step;
    public string objective;      // 화면 위 목표 문구 (null이면 설명만 보고 다음 단계로)
    public TutorialPage[] intro;  // 단계가 시작될 때 보여줄 설명 (없으면 바로 목표)
}

// 상황별 도움말 (조건이 처음 맞을 때 한 번만)
public class TutorialTip
{
    public string id;
    public TutorialPage[] pages;
    public TutorialTip(string id, params TutorialPage[] pages) { this.id = id; this.pages = pages; }
    public string Title => pages != null && pages.Length > 0 ? pages[0].title : id;
}

public static class TutorialRules
{
    // 상황별 도움말 id (조건 판정은 TutorialManager)
    public const string TipAdventurerDetail = "detail";
    public const string TipMultiDispatch    = "multi_dispatch";
    public const string TipShop             = "shop";
    public const string TipFacility         = "facility";
    public const string TipTemple           = "temple";
    public const string TipClinic           = "clinic";
    public const string TipInjured          = "injured";
    public const string TipAffliction       = "affliction";
    public const string TipStranded         = "stranded";
    public const string TipDead             = "dead";
    public const string TipPayday           = "payday";
    public const string TipLowMorale        = "morale";
    public const string TipRelation         = "relation";
    public const string TipReputationUp     = "reputation";
    public const string TipBossRoom         = "boss_room";
    public const string TipGatherQuest      = "gather_quest";
    public const string TipRescueQuest      = "rescue_quest";
    public const string TipLowHp            = "low_hp";
    public const string TipTreasureRoom     = "treasure_room";
    public const string TipWaveRoom         = "wave_room";

    // 던전 안에서만 진행되는 단계 (로비로 돌아왔는데 이 단계면 결과 단계로 넘김)
    public static bool IsDungeonStep(TutorialStep step) =>
        step == TutorialStep.Battle || step == TutorialStep.Campfire || step == TutorialStep.FinishDungeon;

    public static TutorialStep Next(TutorialStep step) => step >= TutorialStep.Done ? TutorialStep.Done : step + 1;

    // 진행 표시 (예: "3 / 12")
    public static int StepNumber(TutorialStep step) => Math.Min((int)step + 1, (int)TutorialStep.Done);
    public static int StepTotal => (int)TutorialStep.Done;

    static readonly Dictionary<TutorialStep, TutorialStepInfo> steps = new Dictionary<TutorialStep, TutorialStepInfo>
    {
        [TutorialStep.Welcome] = new TutorialStepInfo
        {
            step = TutorialStep.Welcome,
            intro = new[]
            {
                new TutorialPage("길드 마스터님, 환영합니다!",
                    "이제부터 이 길드를 이끌어 가실 차례입니다.\n\n" +
                    "길드 마스터는 직접 싸우지 않습니다. 모험가를 <b>고용</b>하고, <b>파티</b>를 짜서, 의뢰를 맡은 <b>던전</b>으로 보냅니다.\n" +
                    "의뢰를 완수하면 골드와 명성이 들어오고, 그 돈으로 길드를 키워 더 어려운 의뢰에 도전합니다."),
                new TutorialPage("상단 정보줄",
                    "화면 위쪽에는 길드의 살림이 표시됩니다. 아이콘에 마우스를 올리면 이름이 나옵니다.\n\n" +
                    "<color=#FFD966>골드</color>: 고용비, 월급, 시설 업그레이드에 씁니다.\n" +
                    "<color=#FFD966>명성</color>: 의뢰를 완수하면 오릅니다. 명성 레벨이 오르면 새 지역이 열립니다.\n" +
                    "<color=#FFD966>영혼석</color>: 신전에서 쓰러진 모험가를 되살릴 때 씁니다.\n" +
                    "<color=#FFD966>날짜</color>: 한 달은 4주. <color=#FF7366>매달 1주차에 모험가 월급</color>이 빠져나갑니다."),
                new TutorialPage("한 주의 흐름",
                    "길드는 한 주 단위로 돌아갑니다.\n\n" +
                    "1. 접수처에서 찾아온 모험가를 고용하고\n" +
                    "2. 파티를 짜서 여관 방에 묵게 한 뒤\n" +
                    "3. 퀘스트 보드에서 의뢰를 받아 던전으로 보냅니다.\n" +
                    "4. 돌아온 결과를 확인하면 한 주가 지나갑니다.\n\n" +
                    "지금부터 이 순서를 한 번 따라가 보겠습니다. 안내가 필요 없으시면 [건너뛰기]를 누르세요."),
            }
        },
        [TutorialStep.VisitReception] = new TutorialStepInfo
        {
            step = TutorialStep.VisitReception,
            objective = "<b>접수처</b>를 클릭해 길드를 찾아온 모험가를 만나 보세요.",
        },
        [TutorialStep.ReceptionGuide] = new TutorialStepInfo
        {
            step = TutorialStep.ReceptionGuide,
            objective = "마음에 드는 모험가가 있으면 고용하고, 접수처 화면을 닫으세요.",
            intro = new[]
            {
                new TutorialPage("접수처 - 모험가 고용",
                    "이번 주에 길드를 찾아온 모험가가 한 명씩 카드로 나옵니다.\n\n" +
                    "카드를 <color=#7CFC7C>오른쪽으로 끌면 고용</color>, <color=#FF7366>왼쪽으로 끌면 돌려보냅니다</color>.\n" +
                    "오른쪽 위에서 랭크(D~S), 직업, 능력치, 고용비를 확인하세요."),
                new TutorialPage("고용할 때 알아 둘 것",
                    "· 고용하면 고용비가 바로 빠지고, <color=#FF7366>매달 월급</color>도 나갑니다. 랭크가 높을수록 비쌉니다.\n" +
                    "· 고용하지 않은 모험가는 한 주가 지나면 떠나고, 새 모험가가 찾아옵니다.\n" +
                    "· 접수처 레벨이 2가 되면 성격·특성을, 3이 되면 잠재력까지 미리 볼 수 있습니다.\n\n" +
                    "길드에는 이미 시작 모험가 4명(전사·마법사·도적·사제)이 있으니, 지금은 꼭 고용하지 않아도 됩니다."),
            }
        },
        [TutorialStep.MakeParty] = new TutorialStepInfo
        {
            step = TutorialStep.MakeParty,
            objective = "<b>모험가 관리 → 파티 편성 → [파티 생성]</b>으로 파티를 만들고 모험가를 넣으세요.",
            intro = new[]
            {
                new TutorialPage("파티 편성",
                    "모험가는 파티 단위로 던전에 갑니다. 파티 하나에 처음에는 4명까지 넣을 수 있습니다.\n\n" +
                    "<b>앞줄</b>: 전사(적의 공격을 받아냄), 도적(빠르고 치명타가 강함)\n" +
                    "<b>뒷줄</b>: 궁수·마법사(멀리서 공격), 사제(아군 회복)\n\n" +
                    "앞줄이 버텨 주고 뒷줄이 공격·회복하는 구성이 기본입니다. 사제 한 명은 꼭 넣는 것을 추천합니다."),
            }
        },
        [TutorialStep.AssignRoom] = new TutorialStepInfo
        {
            step = TutorialStep.AssignRoom,
            objective = "<b>계단</b>으로 2층 여관에 올라가 <b>빈 방</b>을 눌러 파티를 배정하세요.",
            intro = new[]
            {
                new TutorialPage("케미와 여관 방",
                    "좋습니다! 같은 파티로 의뢰를 함께 완수할수록 모험가 사이의 <color=#FFB6D9>케미</color>가 쌓여 체력·공격력이 오릅니다. " +
                    "파티 구성을 자주 바꾸지 않는 편이 유리합니다.\n\n" +
                    "그런데 파티는 <b>여관 방</b>에 묵어야 파견할 수 있습니다. 방 하나에 파티 하나가 들어갑니다.\n" +
                    "여관 레벨을 올리면 방이 늘고, 방을 증축하면 파티 정원이 6명까지 늘어납니다."),
            }
        },
        [TutorialStep.AcceptQuest] = new TutorialStepInfo
        {
            step = TutorialStep.AcceptQuest,
            objective = "1층 <b>퀘스트 보드 → [의뢰 수행]</b>에서 의뢰를 골라 파티를 보내세요.",
            intro = new[]
            {
                new TutorialPage("의뢰 고르기",
                    "이제 출발 준비가 끝났습니다. 퀘스트 보드에서 의뢰를 고르면 됩니다.\n\n" +
                    "· 먼저 <b>지역</b>을 고릅니다. 처음에는 초원만 열려 있고, 명성 레벨이 오르면 새 지역이 열립니다.\n" +
                    "· 의뢰마다 <b>랭크</b>와 <b>권장 레벨</b>이 있습니다. 처음에는 D랭크를 고르세요.\n" +
                    "· 의뢰 종류: <b>보스 토벌</b>(보스방 정리), <b>재료 수급</b>(재료를 모으면 성공), <b>구출</b>(갇힌 사람을 찾아 구함)\n\n" +
                    "파티를 고를 때 <color=#FF7366>위험</color> 표시가 뜨면 의뢰에 비해 약하다는 뜻입니다. 한 번 더 누르면 그래도 출발합니다."),
            }
        },
        [TutorialStep.Battle] = new TutorialStepInfo
        {
            step = TutorialStep.Battle,
            objective = "방 안의 적을 모두 쓰러뜨리세요. 전투는 자동으로 진행됩니다.",
            intro = new[]
            {
                new TutorialPage("던전 - 자동 전투",
                    "던전에 들어왔습니다! 전투는 <b>자동</b>입니다. 모험가들은 직업과 성격에 따라 알아서 싸웁니다.\n\n" +
                    "· 유닛에 마우스를 올리면 체력과 상태를 볼 수 있습니다.\n" +
                    "· 오른쪽 위 <b>미니맵</b>에 던전의 방 구조가 나옵니다. 빨간 해골(☠)이 보스방입니다.\n" +
                    "· 적을 모두 쓰러뜨리면 모험가가 상자를 열고 모닥불에서 쉽니다."),
                new TutorialPage("위험할 때는 도주",
                    "싸움이 불리하면 <color=#FF7366>[도주]</color> 버튼으로 바로 빠져나올 수 있습니다.\n\n" +
                    "다만 도주하면 모험가가 던전에 <b>낙오</b>하거나 사기가 떨어질 수 있습니다. " +
                    "낙오한 모험가는 몇 주 안에 구출 의뢰로 데려오지 않으면 목숨을 잃습니다.\n\n" +
                    "체력이 바닥난 모험가는 <b>전사</b>합니다. 무리하지 마세요.\n(설명을 읽는 동안 전투는 멈춰 있습니다.)"),
            }
        },
        [TutorialStep.Campfire] = new TutorialStepInfo
        {
            step = TutorialStep.Campfire,
            objective = "지도에서 갈 <b>방향</b>을 골라 다음 방으로 이동하세요.",
            intro = new[]
            {
                new TutorialPage("모닥불 휴식",
                    "방을 정리했습니다! 방을 처음 정리할 때마다 모닥불에서 체력을 조금 회복합니다.\n\n" +
                    "· 지도에서 이어진 방향을 골라 다음 방으로 갑니다. 안 가 본 방은 종류를 알 수 없습니다.\n" +
                    "· 방 종류: 일반, <color=#7CC07C>휴식</color>(크게 회복), <color=#E0B040>보물</color>(상자, 가끔 미믹), " +
                    "<color=#A080D0>웨이브</color>(적이 여러 번 몰려오지만 보상이 큼), <color=#E05040>보스</color>\n" +
                    "· <b>보스방을 정리하면 의뢰 완수</b>입니다."),
                new TutorialPage("귀환",
                    "상황이 나쁘면 <b>[귀환]</b>을 눌러 돌아갈 수 있습니다.\n\n" +
                    "모닥불에서 귀환하면 도주와 달리 <color=#7CFC7C>낙오 걱정이 없고</color> 지금까지 얻은 전리품도 챙깁니다. " +
                    "대신 의뢰를 완수하지 못했으니 의뢰 보상과 경험치는 없습니다.\n\n" +
                    "체력 상태를 보고 더 나아갈지, 돌아갈지 판단하는 것이 길드 마스터의 일입니다."),
            }
        },
        [TutorialStep.FinishDungeon] = new TutorialStepInfo
        {
            step = TutorialStep.FinishDungeon,
            objective = "보스방(☠)을 찾아 정리하면 의뢰 완수! 위험하면 모닥불에서 귀환하세요.",
        },
        [TutorialStep.Result] = new TutorialStepInfo
        {
            step = TutorialStep.Result,
            objective = "결과를 확인하고 <b>[확인]</b>을 누르세요. 한 주가 지나갑니다.",
            intro = new[]
            {
                new TutorialPage("의뢰 결과",
                    "길드로 돌아왔습니다. 결과창에서 이번 파견의 성과를 확인하세요.\n\n" +
                    "· <color=#FFD966>골드·명성</color>: 의뢰 보상과 던전에서 얻은 돈\n" +
                    "· <color=#FFD966>전리품</color>: 길드 창고에 들어갑니다. 길드 밖 상점에서 골드로 팔 수 있습니다.\n" +
                    "· <color=#FFD966>레벨업·관계</color>: 경험치를 얻은 모험가는 강해지고, 함께 고생한 동료와 관계가 생기기도 합니다.\n\n" +
                    "다친 모험가는 몇 주 쉬어야 다시 나갈 수 있습니다. 결과를 확인하면 한 주가 흐릅니다."),
            }
        },
        [TutorialStep.Town] = new TutorialStepInfo
        {
            step = TutorialStep.Town,
            objective = "<b>길드 문</b>을 클릭해 길드 밖으로 나가 보세요.",
        },
        [TutorialStep.PassWeek] = new TutorialStepInfo
        {
            step = TutorialStep.PassWeek,
            intro = new[]
            {
                new TutorialPage("길드 밖",
                    "길드 밖에는 네 곳의 건물이 있습니다.\n\n" +
                    "<b>건축 공방</b>: 접수처·여관·주점·신전·퀘스트 보드를 업그레이드합니다.\n" +
                    "<b>상점</b>: 던전에서 얻은 전리품을 골드로 팝니다.\n" +
                    "<b>신전</b>: 영혼석으로 쓰러진 모험가를 되살리고, 트라우마를 치료합니다.\n" +
                    "<b>치유소</b>: 외팔이·절름발이 같은 결함을 골드로 치료합니다.\n\n" +
                    "처음에는 전리품을 팔아 자금을 모으고, <b>여관</b>과 <b>퀘스트 보드</b>를 올려 파티를 늘리는 것을 추천합니다."),
                new TutorialPage("한 주 보내기",
                    "1층 화면의 <b>[한 주 보내기]</b> 버튼을 누르면 파견 없이 시간만 넘길 수 있습니다.\n" +
                    "부상자를 쉬게 하거나, 새 모험가를 기다릴 때 씁니다.\n\n" +
                    "한 주가 지나면 다친 모험가가 회복하고, 접수처에 새 모험가가 찾아옵니다.\n" +
                    "<color=#FF7366>매달 1주차에는 월급이 나갑니다. 월급을 낼 골드가 없으면 길드가 파산합니다!</color>"),
                new TutorialPage("기본 안내 끝",
                    "기본적인 흐름은 여기까지입니다. 수고하셨습니다!\n\n" +
                    "앞으로 부상, 낙오, 트라우마, 관계처럼 처음 겪는 일이 생기면 그때마다 짧은 도움말이 뜹니다.\n" +
                    "왼쪽 아래 <b>[도움말]</b> 버튼으로 지금까지의 설명을 언제든 다시 볼 수 있습니다.\n\n" +
                    "명성을 쌓아 새 지역을 열고, 최고의 길드를 만들어 보세요!"),
            }
        },
    };

    static readonly List<TutorialTip> tips = new List<TutorialTip>
    {
        new TutorialTip(TipAdventurerDetail, new TutorialPage("모험가 상세 정보",
            "모험가마다 <b>성격</b>, <b>특성</b>, 사기, 무드, 관계가 있습니다.\n\n" +
            "· <b>성격</b>: 전투 행동을 바꿉니다. 겁쟁이는 다치면 숨고, 다혈질은 가까운 적에게 돌진합니다.\n" +
            "· <b>특성</b>: 능력을 올려 주는 좋은 특성과, 크게 다쳐서 생기는 트라우마·결함이 있습니다.\n" +
            "· <b>사기</b>: 도주·패배로 떨어지면 능력치가 줄어듭니다. 주점이 있으면 매주 회복됩니다.\n" +
            "· <b>무드</b>: 기분입니다. 너무 나쁘면 전투 중에 멍하니 멈추기도 합니다.\n\n" +
            "<u>밑줄 친 이름</u>에 마우스를 올리면 자세한 설명이 나옵니다.")),

        new TutorialTip(TipMultiDispatch, new TutorialPage("동시 파견",
            "동시 파견은 여러 파티를 한꺼번에 보내고 <b>결과만</b> 받는 방식입니다.\n\n" +
            "직접 지휘처럼 도주·귀환을 판단할 수 없어, 파티 실력에 따라 결과가 정해집니다.\n" +
            "한 번에 보낼 수 있는 팀 수는 <b>퀘스트 보드 레벨</b>에 따라 늘어납니다.\n\n" +
            "익숙한 의뢰는 동시 파견으로, 어려운 의뢰는 직접 지휘로 보내는 것이 좋습니다.")),

        new TutorialTip(TipShop, new TutorialPage("상점",
            "던전에서 얻은 전리품을 골드로 팝니다.\n\n" +
            "재료 수급 의뢰에 필요한 재료는 의뢰를 받을 때까지 남겨 두는 것도 방법입니다.\n" +
            "월급날이 다가오는데 골드가 모자라면 여기서 급히 마련하세요.")),

        new TutorialTip(TipFacility, new TutorialPage("건축 공방",
            "시설마다 효과와 업그레이드 비용이 표시됩니다.\n\n" +
            "<b>접수처</b>: 찾아오는 모험가 수, 성격·특성·잠재력 미리 보기\n" +
            "<b>여관</b>: 방 수(파티 수)와 최대 모험가 수\n" +
            "<b>주점</b>: 매주 사기 회복\n" +
            "<b>신전</b>: 치료 비용 할인\n" +
            "<b>퀘스트 보드</b>: 동시 파견 팀 수")),

        new TutorialTip(TipTemple, new TutorialPage("신전",
            "<b>부활</b>: 유해를 수습한 전사자를 영혼석으로 되살립니다.\n" +
            "<b>트라우마 치료</b>: 공포증이나 우울함 같은 정신적 상처를 치료합니다.\n\n" +
            "영혼석은 귀하니 아끼는 모험가에게 쓰세요.")),

        new TutorialTip(TipClinic, new TutorialPage("치유소",
            "크게 다친 모험가에게 생긴 <b>결함</b>(외팔이, 절름발이, 외눈박이, 약골)을 골드로 치료합니다.\n" +
            "랭크가 높은 모험가일수록 비용이 비쌉니다. 의뢰를 나간 모험가는 치료할 수 없습니다.")),

        new TutorialTip(TipInjured, new TutorialPage("부상",
            "던전에서 체력이 많이 깎인 모험가는 <color=#FF7366>부상</color>을 입고 몇 주 쉬어야 합니다.\n\n" +
            "명단에서 이름 옆에 남은 주 수가 빨갛게 표시됩니다. 부상자는 시간이 지나면 낫습니다.\n" +
            "크게 다칠수록 트라우마나 결함이 생길 확률도 올라가니, 체력이 낮으면 귀환을 고려하세요.")),

        new TutorialTip(TipAffliction, new TutorialPage("트라우마와 결함",
            "크게 다친 모험가에게 마음이나 몸의 상처가 남았습니다.\n\n" +
            "<color=#FF7366>트라우마</color>(공포증, 우울함): 특정 적에게 약해지거나 기분이 가라앉습니다. → <b>신전</b>에서 치료\n" +
            "<color=#FFA040>결함</color>(외팔이, 절름발이 등): 능력치가 크게 떨어집니다. → <b>치유소</b>에서 치료\n\n" +
            "모험가 상세 정보에서 이름에 마우스를 올리면 효과를 볼 수 있습니다.")),

        new TutorialTip(TipStranded, new TutorialPage("낙오한 모험가",
            "도주하는 도중 모험가가 던전에 <color=#FF7366>낙오</color>했습니다!\n\n" +
            "퀘스트 보드에 <b>긴급 구출</b> 의뢰가 올라옵니다. 정해진 주 안에 구하지 못하면 그 모험가는 목숨을 잃습니다.\n" +
            "구출 의뢰는 던전 어딘가에 갇힌 모험가를 찾아내면 성공입니다.")),

        new TutorialTip(TipDead, new TutorialPage("전사자",
            "모험가가 던전에서 쓰러졌습니다.\n\n" +
            "유해가 던전에 남아 있으면 <b>유해 수습</b> 의뢰로 데려올 수 있고, 데려온 전사자는 신전에서 <b>영혼석</b>으로 되살릴 수 있습니다.\n" +
            "동료를 잃은 모험가는 우울해질 수 있습니다.")),

        new TutorialTip(TipPayday, new TutorialPage("다음 주는 월급날",
            "다음 주가 되면 모든 모험가의 월급이 한꺼번에 빠져나갑니다.\n\n" +
            "<color=#FF7366>월급을 낼 골드가 모자라면 길드가 파산합니다.</color>\n" +
            "한 주 보내기 확인창에 지출과 보유 골드가 나오니 꼭 확인하세요. 모자라면 전리품을 팔거나 의뢰를 다녀오세요.")),

        new TutorialTip(TipLowMorale, new TutorialPage("사기 하락",
            "도주하거나 의뢰에 실패하면 모험가의 <color=#8FA6FF>사기</color>가 떨어집니다. 이름 옆에 ↓사기 표시가 붙습니다.\n\n" +
            "사기가 떨어진 동안은 능력치가 줄어듭니다. 의뢰를 완수하면 오르고, " +
            "<b>주점</b>을 지으면 매주 조금씩 회복됩니다.")),

        new TutorialTip(TipRelation, new TutorialPage("모험가 사이의 관계",
            "함께 던전을 다녀온 모험가 사이에 관계가 생겼습니다.\n\n" +
            "<color=#FFB6D9>친우·연인·의형제</color>는 서로를 지키려 하고 기분이 좋아집니다.\n" +
            "<color=#B080FF>라이벌</color>은 경쟁하며 강해지고, <color=#B080FF>불화</color>는 같은 파티일 때 케미가 쌓이지 않습니다.\n\n" +
            "모험가 상세 정보의 오른쪽 칸에서 관계를 볼 수 있습니다.")),

        new TutorialTip(TipReputationUp, new TutorialPage("명성 레벨 상승",
            "명성 레벨이 올랐습니다! 퀘스트 보드에서 <b>새 지역</b>의 의뢰를 받을 수 있게 되었을 수 있습니다.\n\n" +
            "지역마다 나오는 몬스터 종족과 의뢰 랭크가 다릅니다. 새 지역은 파티를 충분히 키운 뒤에 도전하세요.")),

        new TutorialTip(TipBossRoom, new TutorialPage("보스방",
            "보스방에 들어왔습니다! 보스는 체력이 많고 고유한 기술을 씁니다.\n\n" +
            "위쪽에 보스 체력바가 나옵니다. 보스를 쓰러뜨리면 의뢰 완수입니다.\n" +
            "아군이 하나둘 쓰러지기 시작하면 무리하지 말고 도주를 고려하세요.")),

        new TutorialTip(TipGatherQuest, new TutorialPage("재료 수급 의뢰",
            "이번 의뢰는 <b>재료 수급</b>입니다. 특정 몬스터가 떨어뜨리는 재료를 정해진 개수만큼 모으면 성공입니다.\n\n" +
            "재료를 다 모았다면 보스방까지 가지 않고 모닥불에서 <b>귀환</b>해도 의뢰 완수로 처리됩니다.")),

        new TutorialTip(TipRescueQuest, new TutorialPage("구출 의뢰",
            "이번 의뢰는 <b>구출</b>입니다. 던전 어딘가의 방에 갇힌 사람이 있습니다.\n\n" +
            "그 방을 정리하면 구출 성공이고, 구출한 뒤에는 모닥불에서 귀환해도 의뢰 완수입니다.\n" +
            "보스까지 쓰러뜨리면 보상이 더 좋습니다.")),

        new TutorialTip(TipLowHp, new TutorialPage("모험가가 위험합니다",
            "체력이 크게 줄어든 모험가가 있습니다.\n\n" +
            "체력이 0이 되면 그 모험가는 <color=#FF7366>전사</color>합니다. " +
            "사제가 회복하지 못할 것 같으면 <b>[도주]</b>로 빠져나오는 것도 길드 마스터의 판단입니다.")),

        new TutorialTip(TipTreasureRoom, new TutorialPage("보물방",
            "전투 없이 보물 상자가 놓인 방입니다. 상자를 열면 골드를 얻습니다.\n\n" +
            "<color=#FF7366>가끔 상자가 미믹(상자 괴물)일 때가 있습니다.</color> 신중한 성격의 모험가는 미믹을 알아채기도 합니다.")),

        new TutorialTip(TipWaveRoom, new TutorialPage("웨이브 방",
            "적이 여러 차례에 걸쳐 몰려오는 방입니다. 모두 막아내면 큰 골드 보상이 있습니다.\n\n" +
            "오래 싸워야 하니 체력이 넉넉할 때 들어오는 것이 좋습니다.")),
    };

    public static TutorialStepInfo GetStep(TutorialStep step) =>
        steps.TryGetValue(step, out TutorialStepInfo info) ? info : null;

    public static TutorialTip GetTip(string id) => tips.Find(t => t.id == id);

    public static IReadOnlyList<TutorialTip> AllTips => tips;

    // 도움말 목록에 보여줄 기본 튜토리얼 설명 (설명 창이 있는 단계만, 순서대로)
    public static List<TutorialStepInfo> StepsWithIntro()
    {
        var list = new List<TutorialStepInfo>();
        for (TutorialStep s = TutorialStep.Welcome; s < TutorialStep.Done; s++)
        {
            TutorialStepInfo info = GetStep(s);
            if (info != null && info.intro != null && info.intro.Length > 0) list.Add(info);
        }
        return list;
    }
}
