using System;
using System.Collections.Generic;

// 장비 칸 (1차: 무기·방어구. 장신구는 2차). 저장 파일이 숫자로 기억하므로 뒤에만 추가
public enum EquipSlot { Weapon, Armor }

// 장비 한 개. id가 비어 있으면 "없음" (JsonUtility·인스펙터는 null 대신 빈 객체를 만들기 때문)
[Serializable]
public class EquipmentItem
{
    public string id = "";
    public EquipSlot slot;
    public RankType grade;
    public JobType job;      // 무기만 의미 있음 (그 직업만 낄 수 있음)
    public int plus;         // 강화 단계 (+0 ~ +최대)

    public EquipmentItem Clone() => (EquipmentItem)MemberwiseClone();
}

// 장비 수치 (GameManager 인스펙터에서 조정). 등급별 배열은 D, C, B, A, S 순서
[Serializable]
public class EquipmentSettings
{
    [UnityEngine.Header("기본 수치 (+0 기준)")]
    [UnityEngine.Tooltip("무기 공격력 = 이 값 × 직업 배율")]
    public float[] weaponAtkPerGrade = { 3f, 6f, 11f, 18f, 28f };
    public float[] armorHpPerGrade   = { 15f, 30f, 55f, 90f, 140f };
    public float[] armorDefPerGrade  = { 2f, 4f, 6f, 9f, 13f };

    [UnityEngine.Tooltip("무기 공격력 직업 배율 (전사, 마법사, 궁수, 사제, 도적 = JobType 순서)")]
    public float[] weaponJobFactor = { 1.0f, 1.4f, 1.2f, 0.7f, 1.1f };

    [UnityEngine.Header("강화")]
    public int maxPlus = 5;
    [UnityEngine.Tooltip("강화 한 단계마다 기본 수치에 더하는 비율 (+5면 ×1.5)")]
    public float plusStep = 0.1f;
    [UnityEngine.Tooltip("강화 골드 = 등급 값 × (지금 단계 + 1)")]
    public int[] upgradeGoldPerGrade = { 60, 120, 240, 400, 650 };
    [UnityEngine.Tooltip("강화 재료 (등급별 전리품 이름). 개수 = 지금 단계 + 1")]
    public string[] upgradeMaterialPerGrade = { "가죽", "뼛가루", "오크의 도끼", "오크의 깃발", "드래곤의 비늘" };

    [UnityEngine.Header("얻는 곳")]
    [UnityEngine.Tooltip("보스를 쓰러뜨렸을 때 장비가 나올 확률 (등급 = 의뢰 랭크)")]
    public float bossDropChance = 0.35f;
    [UnityEngine.Tooltip("방을 정리하고 상자를 열 때 장비가 나올 확률")]
    public float chestDropChance = 0.05f;
    [UnityEngine.Tooltip("상점 무기 가격 (0이면 팔지 않음)")]
    public int[] shopWeaponPrice = { 120, 260, 0, 0, 0 };
    [UnityEngine.Tooltip("상점 방어구 가격 (0이면 팔지 않음)")]
    public int[] shopArmorPrice = { 100, 220, 0, 0, 0 };
    [UnityEngine.Tooltip("창고 장비를 팔 때 받는 비율 (상점 가격 또는 등급 기준가의)")]
    public float sellRatio = 0.4f;
    [UnityEngine.Tooltip("상점에서 팔지 않는 등급의 기준가 (판매가 계산용)")]
    public int[] baseValuePerGrade = { 110, 240, 480, 800, 1300 };

    [UnityEngine.Header("이름")]
    public string[] gradePrefix = { "낡은", "쓸 만한", "정교한", "명장의", "전설의" };
    [UnityEngine.Tooltip("무기 이름 (JobType 순서: 전사, 마법사, 궁수, 사제, 도적)")]
    public string[] weaponNames = { "검", "지팡이", "활", "홀", "단검" };
    public string armorName = "갑옷";
}

// 장비 규칙 (순수 로직)
public static class EquipmentRules
{
    public static bool IsEmpty(EquipmentItem e) => e == null || string.IsNullOrEmpty(e.id);

    static float ByGrade(float[] t, RankType g) { if (t == null || t.Length == 0) return 0f; return t[Math.Max(0, Math.Min((int)g, t.Length - 1))]; }
    static int ByGrade(int[] t, RankType g) => EconomyRules.ByRank(t, g, 0);
    static float PlusFactor(EquipmentSettings s, int plus) => 1f + Math.Max(0, plus) * s.plusStep;

    public static EquipmentItem Create(EquipSlot slot, RankType grade, JobType job, int serial)
        => new EquipmentItem { id = $"eq{serial}", slot = slot, grade = grade, job = job, plus = 0 };

    public static int Atk(EquipmentSettings s, EquipmentItem e)
    {
        s = s ?? new EquipmentSettings();
        if (IsEmpty(e) || e.slot != EquipSlot.Weapon) return 0;
        float factor = s.weaponJobFactor != null && s.weaponJobFactor.Length > (int)e.job ? s.weaponJobFactor[(int)e.job] : 1f;
        return (int)Math.Round(ByGrade(s.weaponAtkPerGrade, e.grade) * factor * PlusFactor(s, e.plus), MidpointRounding.AwayFromZero);
    }

    public static int Hp(EquipmentSettings s, EquipmentItem e)
    {
        s = s ?? new EquipmentSettings();
        if (IsEmpty(e) || e.slot != EquipSlot.Armor) return 0;
        return (int)Math.Round(ByGrade(s.armorHpPerGrade, e.grade) * PlusFactor(s, e.plus), MidpointRounding.AwayFromZero);
    }

    public static int Def(EquipmentSettings s, EquipmentItem e)
    {
        s = s ?? new EquipmentSettings();
        if (IsEmpty(e) || e.slot != EquipSlot.Armor) return 0;
        return (int)Math.Round(ByGrade(s.armorDefPerGrade, e.grade) * PlusFactor(s, e.plus), MidpointRounding.AwayFromZero);
    }

    // 이 직업이 낄 수 있는가 (무기는 같은 직업만, 방어구는 누구나)
    public static bool CanEquip(EquipmentItem e, JobType job) => !IsEmpty(e) && (e.slot == EquipSlot.Armor || e.job == job);

    public static string Name(EquipmentSettings s, EquipmentItem e)
    {
        s = s ?? new EquipmentSettings();
        if (IsEmpty(e)) return "없음";
        string prefix = s.gradePrefix != null && s.gradePrefix.Length > (int)e.grade ? s.gradePrefix[(int)e.grade] : e.grade.ToString();
        string baseName = e.slot == EquipSlot.Armor ? s.armorName
            : s.weaponNames != null && s.weaponNames.Length > (int)e.job ? s.weaponNames[(int)e.job] : "무기";
        return e.plus > 0 ? $"{prefix} {baseName} +{e.plus}" : $"{prefix} {baseName}";
    }

    // "공격력 +8" / "체력 +30, 방어력 +4"
    public static string StatText(EquipmentSettings s, EquipmentItem e)
    {
        if (IsEmpty(e)) return "";
        return e.slot == EquipSlot.Weapon ? $"공격력 +{Atk(s, e)}" : $"체력 +{Hp(s, e)}, 방어력 +{Def(s, e)}";
    }

    public static bool CanUpgrade(EquipmentSettings s, EquipmentItem e) => !IsEmpty(e) && e.plus < (s ?? new EquipmentSettings()).maxPlus;
    public static int UpgradeGold(EquipmentSettings s, EquipmentItem e) => ByGrade((s ?? new EquipmentSettings()).upgradeGoldPerGrade, e.grade) * (e.plus + 1);
    public static int UpgradeMaterialCount(EquipmentItem e) => e.plus + 1;

    public static string UpgradeMaterial(EquipmentSettings s, EquipmentItem e)
    {
        string[] t = (s ?? new EquipmentSettings()).upgradeMaterialPerGrade;
        if (t == null || t.Length == 0) return "";
        return t[Math.Max(0, Math.Min((int)e.grade, t.Length - 1))];
    }

    public static int ShopPrice(EquipmentSettings s, EquipSlot slot, RankType grade)
    {
        s = s ?? new EquipmentSettings();
        return ByGrade(slot == EquipSlot.Weapon ? s.shopWeaponPrice : s.shopArmorPrice, grade);
    }

    // 창고 장비 판매가 = (상점가 또는 기준가) × 판매 비율, 강화 단계마다 강화 골드의 절반을 더 받음
    public static int SellPrice(EquipmentSettings s, EquipmentItem e)
    {
        s = s ?? new EquipmentSettings();
        if (IsEmpty(e)) return 0;
        int baseValue = ShopPrice(s, e.slot, e.grade);
        if (baseValue <= 0) baseValue = ByGrade(s.baseValuePerGrade, e.grade);
        int spent = 0;
        for (int p = 0; p < e.plus; p++) spent += ByGrade(s.upgradeGoldPerGrade, e.grade) * (p + 1);
        return (int)Math.Round(baseValue * s.sellRatio + spent * 0.5f);
    }

    // 무작위 장비 한 개의 칸·직업 고르기 (roll 0~1). 무기는 jobs 중에서 (파티 직업 등)
    public static EquipSlot RollSlot(float roll) => roll < 0.5f ? EquipSlot.Weapon : EquipSlot.Armor;

    public static JobType PickJob(IList<JobType> jobs, float roll)
    {
        if (jobs == null || jobs.Count == 0) return JobType.Warrior;
        int i = Math.Min(jobs.Count - 1, (int)(roll * jobs.Count));
        return jobs[Math.Max(0, i)];
    }
}
