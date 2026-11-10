using UnityEngine;

public class EnemyTemplate
{
    public readonly string Name;     // 한국어 이름. 픽셀아트 조회와 EnemyNamed의 키로도 쓴다.
    public readonly string NameEn;
    public readonly int Hp, Attack, Defense, Xp, MinDepth;
    public readonly Color Color;
    public int Speed = 1;            // 한 턴에 행동하는 횟수
    public int Range;                // 0이면 근접 전용, 1 이상이면 그 거리까지 원거리 공격
    public int Regen;                // 매 턴 회복량
    public float Scale = 1f;
    public Txt RangedVerb = new("화살", "arrow");

    public string DisplayName => Loc.T(Name, NameEn);

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

    public EnemyTemplate(string name, string nameEn, int hp, int attack, int defense, int xp, int minDepth, Color color)
    {
        Name = name; NameEn = nameEn; Hp = hp; Attack = attack; Defense = defense; Xp = xp; MinDepth = minDepth; Color = color;
    }
}

public enum PlayerClass { Warrior, Rogue, Mage }

public class ClassDef
{
    public PlayerClass Id;
    public Txt Name;
    public int Hp, Attack, Defense, HpPerLevel;
    public float Crit;
    public int SkillCooldown;
    public Txt SkillName, SkillDesc, Passive;
    public Color Color;

    public string ArtKey => Name.Ko;   // PixelArt는 한국어 이름으로 그림을 찾는다.
}

public enum MetaUpgrade { Vitality, Might, Guard, Potion, Greed }

// 영혼석으로 사는 영구 강화 한 종류.
public class MetaUpgradeDef
{
    public MetaUpgrade Id;
    public Txt Name;
    public Txt Effect;         // "{0}" 자리에 수치가 들어간다
    public int PerLevel;       // 단계당 수치
    public int MaxLevel;
    public int BaseCost;       // n단계(1부터) 가격 = BaseCost × n

    public int CostFor(int currentLevel) => BaseCost * (currentLevel + 1);

    public string Describe(int currentLevel)
    {
        string now = string.Format(Effect, currentLevel * PerLevel);
        return currentLevel >= MaxLevel ? now : $"{now}  →  {string.Format(Effect, (currentLevel + 1) * PerLevel)}";
    }
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
            Id = PlayerClass.Warrior, Name = new("전사", "Warrior"), Hp = 40, Attack = 6, Defense = 2, HpPerLevel = 8, Crit = 0.1f,
            SkillName = new("회전베기", "Whirlwind"), SkillCooldown = 5,
            SkillDesc = new("주변 8칸의 모든 적에게 공격력 150% 피해", "Hits every enemy in the 8 surrounding tiles for 150% attack damage."),
            Passive = new("튼튼한 체력, 빠른 자연 회복", "High health, fast natural regeneration."),
            Color = new Color(1f, 0.65f, 0.45f),
        },
        new()
        {
            Id = PlayerClass.Rogue, Name = new("도적", "Rogue"), Hp = 32, Attack = 7, Defense = 1, HpPerLevel = 7, Crit = 0.25f,
            SkillName = new("그림자 숨기", "Shadow Cloak"), SkillCooldown = 8,
            SkillDesc = new("4턴간 숨어 적의 추적을 끊는다. 숨은 채로 하는 공격은 3배 피해", "Hide for 4 turns so enemies lose track of you. An attack from hiding deals triple damage."),
            Passive = new("치명타 확률 25%, 함정을 잘 발견", "25% critical chance, spots traps easily."),
            Color = new Color(0.6f, 0.95f, 0.55f),
        },
        new()
        {
            Id = PlayerClass.Mage, Name = new("마법사", "Mage"), Hp = 24, Attack = 4, Defense = 0, HpPerLevel = 5, Crit = 0.1f,
            SkillName = new("마법 화살", "Magic Missile"), SkillCooldown = 3,
            SkillDesc = new("가까운 적 2명에게 방어를 무시하는 마법 피해", "Hits the 2 nearest enemies with magic damage that ignores defense."),
            Passive = new("두루마리 효과 1.5배, 화염 두루마리 2개로 시작", "Scrolls are 1.5x stronger. Starts with 2 Fire Scrolls."),
            Color = new Color(0.75f, 0.65f, 1f),
        },
    };

    // 영구 강화. 직업 선택 화면에서 영혼석으로 산다.
    public static readonly MetaUpgradeDef[] MetaUpgrades =
    {
        new() { Id = MetaUpgrade.Vitality, Name = new("강인함", "Toughness"), Effect = new("시작 최대 체력 +{0}", "Starting max HP +{0}"), PerLevel = 4, MaxLevel = 5, BaseCost = 10 },
        new() { Id = MetaUpgrade.Might, Name = new("날카로움", "Sharpness"), Effect = new("시작 공격력 +{0}", "Starting attack +{0}"), PerLevel = 1, MaxLevel = 3, BaseCost = 25 },
        new() { Id = MetaUpgrade.Guard, Name = new("단단함", "Sturdiness"), Effect = new("시작 방어력 +{0}", "Starting defense +{0}"), PerLevel = 1, MaxLevel = 2, BaseCost = 40 },
        new() { Id = MetaUpgrade.Potion, Name = new("비상 물약", "Spare Potion"), Effect = new("시작 체력 물약 +{0}", "Starting Health Potions +{0}"), PerLevel = 1, MaxLevel = 2, BaseCost = 30 },
        new() { Id = MetaUpgrade.Greed, Name = new("황금 손", "Golden Touch"), Effect = new("골드 획득 +{0}%", "Gold gained +{0}%"), PerLevel = 10, MaxLevel = 3, BaseCost = 20 },
    };

    public static MetaUpgradeDef MetaOf(MetaUpgrade id) => System.Array.Find(MetaUpgrades, m => m.Id == id);

    // 런이 끝났을 때 받는 영혼석: 내려간 층, 쓰러뜨린 보스, 처치 수.
    public static int SoulStonesFor(int depth, int kills, int bossKills) => (depth - 1) * 2 + bossKills * 8 + kills / 8;

    public static ClassDef ClassOf(PlayerClass id) => System.Array.Find(Classes, c => c.Id == id);

    public static readonly EnemyTemplate[] Enemies =
    {
        new("쥐", "Rat", 4, 2, 0, 2, 1, new Color(0.65f, 0.5f, 0.35f)),
        new("박쥐", "Bat", 3, 2, 0, 3, 1, new Color(0.55f, 0.55f, 0.65f)) { Speed = 2, Scale = 0.75f, Erratic = true },
        new("고블린", "Goblin", 8, 3, 1, 4, 1, new Color(0.4f, 0.8f, 0.35f)) { Pack = true, FleeBelow = 0.3f },
        new("고블린 궁수", "Goblin Archer", 6, 3, 0, 5, 2, new Color(0.75f, 0.9f, 0.4f)) { Range = 5, Pack = true, Kites = true },
        new("슬라임", "Slime", 10, 3, 0, 4, 3, new Color(0.3f, 0.9f, 0.6f)) { Splits = true },
        new("오크", "Orc", 14, 5, 2, 8, 3, new Color(0.85f, 0.45f, 0.2f)) { Scale = 1.1f, Enrages = true },
        new("해골", "Skeleton", 12, 6, 1, 9, 4, new Color(0.92f, 0.9f, 0.82f)),
        new("트롤", "Troll", 24, 7, 3, 15, 6, new Color(0.6f, 0.35f, 0.75f)) { Regen = 1, Scale = 1.2f },
        new("암흑 마법사", "Dark Mage", 16, 8, 1, 18, 8, new Color(0.45f, 0.3f, 0.9f)) { Range = 4, RangedVerb = new("암흑 화살", "dark bolt"), Kites = true },
        new("강령술사", "Necromancer", 18, 5, 1, 20, 9, new Color(0.4f, 0.55f, 0.45f)) { Range = 3, RangedVerb = new("저주", "curse"), Kites = true, Summons = true },
    };

    public static EnemyTemplate EnemyNamed(string name) => System.Array.Find(Enemies, t => t.Name == name);
    public static EnemyTemplate Skeleton => EnemyNamed("해골");

    // 보스는 5층마다 등장한다. 목록을 넘어가면 마지막 보스가 더 강해져서 나온다.
    public static readonly EnemyTemplate[] Bosses =
    {
        new("고블린 왕", "Goblin King", 100, 10, 3, 30, 5, new Color(1f, 0.85f, 0.25f))
            { Scale = 1.5f, PhaseSummon = "고블린", SlamRadius = 1 },
        new("오우거 족장", "Ogre Chieftain", 170, 15, 6, 60, 10, new Color(0.9f, 0.35f, 0.3f))
            { Scale = 1.7f, Regen = 1, SlamRadius = 2, Frenzy = true },
        new("리치", "Lich", 200, 17, 6, 100, 15, new Color(0.5f, 0.95f, 0.9f))
            { Scale = 1.5f, Range = 4, RangedVerb = new("죽음의 광선", "death ray"), PhaseSummon = "해골", Blinks = true },
        new("심연의 용", "Abyssal Dragon", 300, 20, 9, 200, 20, new Color(0.95f, 0.25f, 0.55f))
            { Scale = 1.9f, Range = 3, RangedVerb = new("화염 숨결", "fire breath"), SlamRadius = 2, Frenzy = true, Blinks = true },
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
        TrapKind.Spike => Loc.T("가시 함정", "spike trap"),
        TrapKind.Alarm => Loc.T("경보 함정", "alarm trap"),
        _ => Loc.T("순간이동 함정", "teleport trap"),
    };

    public static Color TrapTint(TrapKind kind) => kind switch
    {
        TrapKind.Spike => new Color(1f, 0.45f, 0.45f),
        TrapKind.Alarm => new Color(1f, 0.85f, 0.3f),
        _ => new Color(0.7f, 0.55f, 1f),
    };

    public static string ItemName(ItemKind kind) => kind switch
    {
        ItemKind.Gold => Loc.T("금화", "Gold"),
        ItemKind.HealthPotion => Loc.T("체력 물약", "Health Potion"),
        ItemKind.FireScroll => Loc.T("화염 두루마리", "Fire Scroll"),
        ItemKind.TeleportScroll => Loc.T("순간이동 두루마리", "Teleport Scroll"),
        ItemKind.MapScroll => Loc.T("지도 두루마리", "Map Scroll"),
        ItemKind.Equipment => Loc.T("장비", "Equipment"),
        _ => kind.ToString(),
    };

    public static string ShortName(ItemKind kind) => kind switch
    {
        ItemKind.HealthPotion => Loc.T("물약", "Potion"),
        ItemKind.FireScroll => Loc.T("화염", "Fire"),
        ItemKind.TeleportScroll => Loc.T("순간이동", "Warp"),
        ItemKind.MapScroll => Loc.T("지도", "Map"),
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
