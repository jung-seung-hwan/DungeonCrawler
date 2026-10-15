using UnityEngine;

public class EnemyTemplate
{
    public readonly string Name;
    public readonly int Hp, Attack, Defense, Xp, MinDepth;
    public readonly Color Color;
    public int Speed = 1;            // 한 턴에 행동하는 횟수
    public int Range;                // 0이면 근접 전용, 1 이상이면 그 거리까지 원거리 공격
    public int Regen;                // 매 턴 회복량
    public float Scale = 1f;
    public string RangedVerb = "화살";

    // 행동 특성
    public float FleeBelow;          // 체력이 이 비율 아래로 떨어지면 도망친다 (0이면 안 도망침)
    public bool Pack;                // 플레이어를 발견하면 주변 동료(Pack)를 부른다
    public bool Kites;               // 원거리형: 붙으면 거리를 벌리려 한다
    public bool Erratic;             // 이리저리 제멋대로 날아다닌다
    public bool Splits;              // 맞으면 둘로 갈라진다
    public bool Enrages;             // 체력이 절반 아래면 격노해 공격력이 오른다
    public bool Summons;             // 해골을 불러낸다

    // 보스 패턴
    public string PhaseSummon;       // 체력 2/3, 1/3 지점에서 이 몬스터를 2마리씩 소환
    public int SlamRadius;           // 0보다 크면 범위 내려찍기를 예고 후 사용
    public bool Blinks;              // 멀리 있는 플레이어 옆으로 순간이동
    public bool Frenzy;              // 체력 30% 아래에서 광폭화 (2회 행동)

    public EnemyTemplate(string name, int hp, int attack, int defense, int xp, int minDepth, Color color)
    {
        Name = name; Hp = hp; Attack = attack; Defense = defense; Xp = xp; MinDepth = minDepth; Color = color;
    }
}

public enum PlayerClass { Warrior, Rogue, Mage }

public class ClassDef
{
    public PlayerClass Id;
    public string Name;
    public int Hp, Attack, Defense, HpPerLevel;
    public float Crit;
    public int SkillCooldown;
    public string SkillName, SkillDesc, Passive;
    public Color Color;
}

public enum ItemKind { HealthPotion, FireScroll, TeleportScroll, MapScroll, Equipment, Gold }

public enum RoomKind { Normal, Start, Stairs, Shop, Treasure, Shrine }

public enum TrapKind { Spike, Alarm, Teleport }

// 밸런스 조정은 이 파일의 숫자만 바꾸면 된다.
public static class GameData
{
    public const int BossFloorInterval = 5;

    public static readonly ClassDef[] Classes =
    {
        new()
        {
            Id = PlayerClass.Warrior, Name = "전사", Hp = 40, Attack = 6, Defense = 2, HpPerLevel = 8, Crit = 0.1f,
            SkillName = "회전베기", SkillCooldown = 5,
            SkillDesc = "주변 8칸의 모든 적에게 공격력 150% 피해",
            Passive = "튼튼한 체력, 빠른 자연 회복",
            Color = new Color(1f, 0.65f, 0.45f),
        },
        new()
        {
            Id = PlayerClass.Rogue, Name = "도적", Hp = 32, Attack = 7, Defense = 1, HpPerLevel = 7, Crit = 0.25f,
            SkillName = "그림자 숨기", SkillCooldown = 8,
            SkillDesc = "4턴간 숨어 적의 추적을 끊는다. 숨은 채로 하는 공격은 3배 피해",
            Passive = "치명타 확률 25%, 함정을 잘 발견",
            Color = new Color(0.6f, 0.95f, 0.55f),
        },
        new()
        {
            Id = PlayerClass.Mage, Name = "마법사", Hp = 24, Attack = 4, Defense = 0, HpPerLevel = 5, Crit = 0.1f,
            SkillName = "마법 화살", SkillCooldown = 3,
            SkillDesc = "가까운 적 2명에게 방어를 무시하는 마법 피해",
            Passive = "두루마리 효과 1.5배, 화염 두루마리 2개로 시작",
            Color = new Color(0.75f, 0.65f, 1f),
        },
    };

    public static ClassDef ClassOf(PlayerClass id) => System.Array.Find(Classes, c => c.Id == id);

    public static readonly EnemyTemplate[] Enemies =
    {
        new("쥐", 4, 2, 0, 2, 1, new Color(0.65f, 0.5f, 0.35f)),
        new("박쥐", 3, 2, 0, 3, 1, new Color(0.55f, 0.55f, 0.65f)) { Speed = 2, Scale = 0.75f, Erratic = true },
        new("고블린", 8, 3, 1, 4, 1, new Color(0.4f, 0.8f, 0.35f)) { Pack = true, FleeBelow = 0.3f },
        new("고블린 궁수", 6, 3, 0, 5, 2, new Color(0.75f, 0.9f, 0.4f)) { Range = 5, Pack = true, Kites = true },
        new("슬라임", 10, 3, 0, 4, 3, new Color(0.3f, 0.9f, 0.6f)) { Splits = true },
        new("오크", 14, 5, 2, 8, 3, new Color(0.85f, 0.45f, 0.2f)) { Scale = 1.1f, Enrages = true },
        new("해골", 12, 6, 1, 9, 4, new Color(0.92f, 0.9f, 0.82f)),
        new("트롤", 24, 7, 3, 15, 6, new Color(0.6f, 0.35f, 0.75f)) { Regen = 1, Scale = 1.2f },
        new("암흑 마법사", 16, 8, 1, 18, 8, new Color(0.45f, 0.3f, 0.9f)) { Range = 4, RangedVerb = "암흑 화살", Kites = true },
        new("강령술사", 18, 5, 1, 20, 9, new Color(0.4f, 0.55f, 0.45f)) { Range = 3, RangedVerb = "저주", Kites = true, Summons = true },
    };

    public static EnemyTemplate EnemyNamed(string name) => System.Array.Find(Enemies, t => t.Name == name);
    public static EnemyTemplate Skeleton => EnemyNamed("해골");

    // 보스는 5층마다 등장한다. 목록을 넘어가면 마지막 보스가 더 강해져서 나온다.
    public static readonly EnemyTemplate[] Bosses =
    {
        new("고블린 왕", 100, 10, 3, 30, 5, new Color(1f, 0.85f, 0.25f))
            { Scale = 1.5f, PhaseSummon = "고블린", SlamRadius = 1 },
        new("오우거 족장", 170, 15, 6, 60, 10, new Color(0.9f, 0.35f, 0.3f))
            { Scale = 1.7f, Regen = 1, SlamRadius = 2, Frenzy = true },
        new("리치", 200, 17, 6, 100, 15, new Color(0.5f, 0.95f, 0.9f))
            { Scale = 1.5f, Range = 4, RangedVerb = "죽음의 광선", PhaseSummon = "해골", Blinks = true },
        new("심연의 용", 300, 20, 9, 200, 20, new Color(0.95f, 0.25f, 0.55f))
            { Scale = 1.9f, Range = 3, RangedVerb = "화염 숨결", SlamRadius = 2, Frenzy = true, Blinks = true },
    };

    // 숫자 키 1~4에 대응하는 소모품 순서.
    public static readonly ItemKind[] Consumables =
        { ItemKind.HealthPotion, ItemKind.FireScroll, ItemKind.TeleportScroll, ItemKind.MapScroll };

    static readonly (ItemKind kind, float weight)[] ItemWeights =
    {
        (ItemKind.HealthPotion, 20), (ItemKind.FireScroll, 15), (ItemKind.TeleportScroll, 8),
        (ItemKind.MapScroll, 10), (ItemKind.Equipment, 16),
    };

    public static ItemKind RollItem(System.Random rng)
    {
        float total = 0;
        foreach (var (_, w) in ItemWeights) total += w;
        double roll = rng.NextDouble() * total;
        foreach (var (kind, w) in ItemWeights)
        {
            roll -= w;
            if (roll <= 0) return kind;
        }
        return ItemKind.HealthPotion;
    }

    // 상점 가격. 깊은 층일수록 비싸지만 그만큼 골드도 많이 모인다.
    public static int Price(ItemKind kind, int depth) => kind switch
    {
        ItemKind.HealthPotion => 20 + depth * 3,
        ItemKind.FireScroll => 30 + depth * 3,
        ItemKind.TeleportScroll => 25 + depth * 2,
        ItemKind.MapScroll => 20 + depth * 2,
        _ => 10,
    };

    public static int Price(Equipment eq, int depth) => 30 + eq.Score * 3 + depth * 3;

    public static int GoldFor(int xpReward, System.Random rng) => xpReward / 4 + rng.Next(1, 3);

    public static string TrapName(TrapKind kind) => kind switch
    {
        TrapKind.Spike => "가시 함정",
        TrapKind.Alarm => "경보 함정",
        _ => "순간이동 함정",
    };

    public static Color TrapTint(TrapKind kind) => kind switch
    {
        TrapKind.Spike => new Color(1f, 0.45f, 0.45f),
        TrapKind.Alarm => new Color(1f, 0.85f, 0.3f),
        _ => new Color(0.7f, 0.55f, 1f),
    };

    public static string ItemName(ItemKind kind) => kind switch
    {
        ItemKind.Gold => "금화",
        ItemKind.HealthPotion => "체력 물약",
        ItemKind.FireScroll => "화염 두루마리",
        ItemKind.TeleportScroll => "순간이동 두루마리",
        ItemKind.MapScroll => "지도 두루마리",
        ItemKind.Equipment => "장비",
        _ => kind.ToString(),
    };

    public static string ShortName(ItemKind kind) => kind switch
    {
        ItemKind.HealthPotion => "물약",
        ItemKind.FireScroll => "화염",
        ItemKind.TeleportScroll => "순간이동",
        ItemKind.MapScroll => "지도",
        _ => ItemName(kind),
    };

    public static Color ItemTint(ItemKind kind) => kind switch
    {
        ItemKind.FireScroll => new Color(1f, 0.55f, 0.4f),
        ItemKind.TeleportScroll => new Color(0.75f, 0.6f, 1f),
        ItemKind.MapScroll => new Color(0.6f, 1f, 0.65f),
        _ => Color.white,
    };
}
