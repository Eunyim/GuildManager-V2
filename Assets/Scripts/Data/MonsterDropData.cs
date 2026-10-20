using UnityEngine;

// 아이템 종류 (잡동사니, 장비, 소비품)
public enum ItemType
{
    Resource,   // 재료/부산물 (슬라임 점액 등)
    Equipment,  // 장비 (칼, 갑옷)
    Consumable  // 소비 (포션)
}

[CreateAssetMenu(fileName = "New Item", menuName = "Dungeon/Item Data")]
public class MonsterDropData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName; // 아이템 이름
    public Sprite icon;     // 아이콘 이미지
    public ItemType type;   // 타입
    public int price;       // 상점 판매가
    
    [Header("설명")]
    [TextArea]
    public string description; // 아이템 설명
}
