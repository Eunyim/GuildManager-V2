using System;
using System.Collections.Generic;

// 저장 파일에 들어가는 데이터 구조 (순수 데이터). 실제 값 옮기기는 GameManager, 파일 입출력은 SaveSystem이 담당

[Serializable]
public class AdventurerSave
{
    public string name;
    public JobType job;
    public RankType rank;
    public TraitType trait;
    public List<string> traitNames = new List<string>(); // 특성은 이름으로 저장하고, 불러올 때 특성 풀에서 다시 찾음

    public int level;
    public int exp;
    public int expToNextLevel;
    public int hp;
    public int atk;
    public int def; // 0이면 예전 저장 파일 → 불러올 때 다시 계산
    public float hpGrowthCarry, atkGrowthCarry, defGrowthCarry; // 레벨업 성장 소수 이월 (예전 저장 파일은 0)
    public float speed;
    public int currentHp;
    public bool isDead;
    public int potential;
    public int recoveryWeeks;
    public string diedAtQuestName;
    public string lastQuestRegion;
    public string lastQuestRank;
    public bool corpseRecovered;
    public bool isStranded;
    public int strandedWeeksRemaining;
    public float moralePenalty; // 예전 저장 파일용 (하락량). 불러올 때 morale이 0이면 -moralePenalty로 옮김
    public float morale;
    public int assignedPartyIndex;
    public int depressionWeeks;
    public float mood;
    public AdventurerLook look; // 겉모습 (예전 저장 파일은 없음 → 불러올 때 새로 정함)
    public EquipmentItem weapon; // 장비 (예전 저장 파일은 없음 → 없음)
    public EquipmentItem armor;
}

// 모험가 두 명 사이의 관계 (연인·친우·의형제·라이벌·불화)
[Serializable]
public class RelationSave
{
    public string a, b;
    public RelationType type;
}

[Serializable]
public class PartySave
{
    public string partyName;
    public int roomIndex;
    public List<int> memberIndices = new List<int>(); // 모험가 명단에서의 순번
    public int chemistryCompletions;   // 예전 저장 파일용 (파티 케미). 불러올 때 구성원끼리의 관계로 옮김
    public string chemistryMemberKey;
}

// 모험가 두 명 사이의 관계 (함께 완수한 횟수)
[Serializable]
public class BondSave
{
    public string a, b;
    public int completions;
}

[Serializable]
public class InventoryEntrySave
{
    public string itemName;
    public int count;
}

[Serializable]
public class SaveData
{
    public int version = 1;

    // 길드
    public string guildName;
    public int gold;
    public int reputation;
    public int day;
    public int guildLevel;
    public int month;
    public int week;

    // 시설 레벨
    public int receptionLevel;
    public int innLevel;
    public int pubLevel;
    public int templeLevel;
    public int questBoardLevel;

    // 모험가 / 파티 / 창고
    public List<AdventurerSave> adventurers     = new List<AdventurerSave>();
    public List<AdventurerSave> deadAdventurers = new List<AdventurerSave>();
    public List<AdventurerSave> visitors        = new List<AdventurerSave>();
    public bool visitorsGenerated;
    public List<PartySave> parties = new List<PartySave>();
    public List<BondSave> bonds = new List<BondSave>();   // 모험가 사이의 관계 (케미)
    public List<RelationSave> relations = new List<RelationSave>(); // 모험가 사이의 관계 (연인·친우 등)
    public List<int> roomLevels = new List<int>();        // 여관 방 레벨 (방 번호 순서, 증축)
    public List<InventoryEntrySave> inventory = new List<InventoryEntrySave>();

    public TutorialProgress tutorial; // 예전 저장 파일은 없음 → 튜토리얼을 끝낸 것으로 봄
    public GuildRecord record;        // 한 판의 기록 (예전 저장 파일은 없음 → 빈 기록)
    public List<EquipmentItem> equipment = new List<EquipmentItem>(); // 길드 창고의 장비 (끼지 않은 것)
    public int equipmentSerial;       // 장비 id 번호
}
