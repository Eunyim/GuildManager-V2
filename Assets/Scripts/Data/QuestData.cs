using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Quest", menuName = "Guild/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("의뢰 기본 정보")]
    public string questName;        
    [TextArea] 
    public string description;      
    public string rank;             
    public string region = "";      // 던전 지역 이름 (초원, 숲 등)

    [Header("조건 및 보상")]
    public int rewardGold; //보수
    public int duration; //소요 시간           
    
    //권장 파티 레벨
    public int recommendedLevel;    

    public GameObject enemyPrefab;

    [Header("던전 스테이지 구성 (랜덤 생성 시 자동 설정)")]
    public List<StageData> stages = new List<StageData>();

    // 방들이 어떻게 이어져 있는지 (rooms[i]가 stages[i]에 대응). 비어 있으면 방이 한 줄로 이어진 것으로 봄
    public DungeonLayout layout;

    [Header("의뢰 종류")]
    public QuestType questType = QuestType.BossHunt;

    [Header("보스 토벌 전용")]
    public GameObject targetBoss;          // 쓰러뜨릴 보스 (보스방에 이 보스가 나옴)

    [Header("재료 수급 전용")]
    public MonsterDropData gatherItem;     // 모아 올 재료
    public int gatherCount;                // 필요한 개수
    public int gatherFee;                  // 의뢰비 (재료 값은 rewardGold - gatherFee)
    public float gatherDropRateMultiplier = 1.5f; // 의뢰 중 그 재료 드랍률 배율 (QuestTypeSettings에서 복사)
    public bool gatherGuaranteeAtEnd = true;      // 막판 보장 (QuestTypeSettings에서 복사)

    [Header("구출 전용 (인물 구출 + 낙오 모험가 긴급 구출)")]
    public int captiveRoom = -1;           // 갇힌 인물이 있는 방 (layout.rooms 번호)
    public string captiveName = "";        // 갇힌 인물
    public int rescueDeepBonus;            // 구출 뒤 보스까지 쓰러뜨리면 더 받는 골드

    [Header("구출 퀘스트 전용")]
    public bool isRescueQuest = false;
    public string rescueTargetName = ""; // 구출 대상 모험가 이름

    [Header("유해 수습 퀘스트 전용")]
    public bool isCorpseQuest = false;
    public string corpseTargetName = ""; // 유해 주인 모험가 이름
}