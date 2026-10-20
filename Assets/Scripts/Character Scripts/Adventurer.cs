using UnityEngine;
using System.Collections.Generic;

// 직업 종류 (Enum)
public enum JobType
{
    Warrior, // 전사
    Mage,    // 마법사
    Archer,  // 궁수
    Healer,  // 사제
    Rogue    // 도적
}
// 성격 (enum). 모험가마다 하나, 랭크가 올라도 바뀌지 않음. 이름·설명·수치는 PersonalityRules / PersonalitySettings
// 저장 파일이 숫자로 기억하므로 순서를 바꾸지 말고 뒤에 추가할 것 (3번은 예전 '공명심' 자리)
public enum TraitType
{
    Normal,       // 평범한
    Coward,       // 겁쟁이
    HotTempered,  // 다혈질
    Clumsy,       // 덜렁거리는
    Careful,      // 신중한
    Lazy,         // 게으른
    Narcissist,   // 나르시시스트
    Lucky,        // 운 좋은
    Altruistic,   // 이타적인
    Extrovert,    // 외향적인
    Humble,       // 겸손한
    Carefree,     // 태평한
    Introvert     // 내향적인
}

// 등급 (Enum)
public enum RankType
{
    D, C, B, A, S
}

[System.Serializable] // 이 줄이 있어야 나중에 인스펙터에서 보입니다!
public class Adventurer
{
    public Adventurer() { }
    public string name;      // 이름
    public JobType job;      // 직업
    public RankType rank;    // 등급
    public TraitType trait; // 성격
    public List<TraitData> traits = new List<TraitData>();
    
    // 능력치

    public int level = 1;
    public int exp = 0;
    public int expToNextLevel = 100; // 레벨 N → N+1 필요 경험치 = N * 100

    public int hp;
    public int atk;
    public int def;          // 방어력: 받는 피해 × 100 ÷ (100 + 방어력)
    // 레벨업 성장의 소수 이월 (레벨마다 반올림한 값을 받고 남은 소수는 다음 레벨업에 더함)
    public float hpGrowthCarry, atkGrowthCarry, defGrowthCarry;
    public float speed;

    // 장비 (id가 비어 있으면 없음). 수치는 EquipmentRules / GameManager.equipment
    public EquipmentItem weapon = new EquipmentItem();
    public EquipmentItem armor = new EquipmentItem();

    public EquipmentItem GetEquipment(EquipSlot slot) => slot == EquipSlot.Weapon ? weapon : armor;
    public void SetEquipment(EquipSlot slot, EquipmentItem item)
    {
        if (slot == EquipSlot.Weapon) weapon = item ?? new EquipmentItem();
        else armor = item ?? new EquipmentItem();
    }

    public int currentHp;
    public bool isDead = false;

    // 잠재력 (1~5): 레벨업 시 스탯 성장률에 영향. 높을수록 성장이 빠름
    public int potential = 1;

    public int recoveryWeeks = 0;

    // 전사 정보 (유해 수습 의뢰 생성에 사용)
    public string diedAtQuestName = ""; // 전사한 의뢰 이름

    // 마지막으로 떠난 의뢰의 지역과 랭크 (구출·유해 수습 의뢰를 같은 지역·같은 랭크로 만들 때 사용)
    public string lastQuestRegion = "";
    public string lastQuestRank = "";

    // 유해를 이미 수습해 영혼석을 얻었는가 (신전 부활 대기 상태로 전사자 목록에 남고, 유해 수습 의뢰는 다시 생기지 않음)
    public bool corpseRecovered = false;

    // 도주 시 던전에 남겨진 상태 (낙오)
    public bool isStranded = false;

    // 낙오 후 구출 가능 남은 주 수 (0이 되면 사망)
    public int strandedWeeksRemaining = 0;

    // 사기 (-0.5 ~ +0.2). 음수면 하락, 양수면 고조. 다음 파견 때 HP·공격력에 (1 + 사기)를 곱함
    // 도주 등으로 떨어지고(주점이 회복), 던전을 깔끔하게 돌파하면 오름(매주 조금씩 식음). 규칙은 MoraleRules
    public float morale = 0f;

    // -1이면 "대기 중(무소속)", 0, 1, 2...이면 해당 파티 소속
    public int assignedPartyIndex = -1;

    // 겉모습 (성별·체형·헤어·옷·무기·색). 없으면 그림을 만들 때 무작위로 정함 (AdventurerSprites.Ensure)
    public AdventurerLook look;

    // 우울함(파티원 사망으로 얻는 부정 특성)이 저절로 사라지기까지 남은 주. 0이면 우울함 없음
    public int depressionWeeks = 0;

    // 무드: 길드 생활의 기분 (-1 ~ +1, 0이 보통). 사기와 별개로 행동에 관여 (RelationshipRules / MoodSettings)
    public float mood = 0f;
    // 마지막으로 말풍선을 띄운 뒤 바뀐 무드 합계 (로비에서 :) / :( 표시용, 저장하지 않음)
    [System.NonSerialized] public float moodShownDelta = 0f;
    // 던전에서 쓰러질 때 쓰러뜨린 적의 계열 (의형제의 복수심 특성용, 저장하지 않음)
    [System.NonSerialized] public string lastKillerFamily = "";
    
    // 생성자 (Constructor): 모험가 태어날 때 기본 세팅
    public Adventurer(string _name, JobType _job, RankType _rank, TraitType _trait)
    {
        name = _name;
        job = _job;
        rank = _rank;
        trait = _trait;
        level = 1;
        
        // 일단 기본값 (나중에 직업별로 다르게 설정 가능)
        hp = 100;
        atk = 10;

        currentHp = hp;
    }
    // 성격 이름 (직업 앞 수식어 형태: "겁쟁이", "운 좋은")
    public string GetTraitName() => PersonalityRules.GetName(trait);

    // 성격 설명 (수치는 GameManager의 성격 설정에서)
    public string GetTraitDescription() =>
        PersonalityRules.GetDescription(trait, GameManager.Instance != null ? GameManager.Instance.personality : null);

    // 상세 정보 직업란: "겁쟁이 궁수"
    public string GetJobWithPersonality() => $"{GetTraitName()} {DisplayNames.JobToKorean(job)}";
}
