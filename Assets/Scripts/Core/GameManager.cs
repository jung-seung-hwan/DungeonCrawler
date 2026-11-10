using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 게임 전체 흐름: 런 시작, 층 생성, 입력 처리, 카메라.
// 기능별 코드는 partial 파일로 나뉘어 있다.
//   GameManager.Combat.cs  플레이어 행동, 몬스터 AI, 전투, 성장
//   GameManager.Items.cs   아이템 생성, 줍기, 소모품 사용
//   GameManager.Rooms.cs   특수 방(보물 창고, 제단), 함정
//   GameManager.Shop.cs    상인, 상점
//   GameManager.Hud.cs     HUD, 미니맵, 데미지 숫자, 화면 흔들림
//   GameManager.Title.cs   타이틀 화면, 직업 선택 화면의 마우스·커서 조작
//   GameManager.Meta.cs    영구 성장(영혼석, 영구 강화)
//   GameManager.Records.cs 기록 화면(직업별 최고 층, 상위 10판, 누적 통계)
//   GameManager.Audio.cs   효과음 재생, 배경음악 전환, 음량·음소거
//   GameManager.Debug.cs   자동 플레이 봇, 스크린샷, 밸런스 시뮬레이션
public partial class GameManager : MonoBehaviour
{
    const int MapWidth = 60;
    const int MapHeight = 36;
    const int FovRadius = 8;
    const int MaxLogLines = 6;
    const string BestDepthKey = "BestDepth";

    readonly List<Actor> enemies = new();
    readonly List<Item> items = new();
    readonly List<string> log = new();
    readonly Dictionary<ItemKind, int> inventory = new();
    readonly InputReader input = new();

    System.Random rng;
    DungeonMap map;
    List<RectInt> rooms;
    Vector2Int stairsPos;
    Actor player;
    Actor boss;
    Transform floorRoot;
    Camera cam;

    int depth;
    int level;
    int xp;
    int kills;
    int turn;
    int turnsOnFloor;
    int bestDepth;
    bool newRecord;
    bool gameOver;
    bool showHelp = true;
    bool helpAutoHidden;
    string killedBy;

    int XpToNextLevel => 8 * level + 2 * level * level;
    bool IsBossFloor => depth % GameData.BossFloorInterval == 0;
    bool StairsSealed => boss != null && !boss.IsDead;

    void Awake()
    {
        Loc.Init(TryGetArg("-lang", out var lang) ? lang : null);
        SetupCamera();
        SetupAudio();
        ShowTitle();
        StartDebugModes();
    }

    void Update()
    {
        UpdateMusic();
        if (autopilotActive) return;

        var cmd = input.Read();
        if (HandleSoundInput(cmd)) return;

        if (choosingClass)
        {
            if (showingTitle) HandleTitleInput(cmd);
            else HandleClassSelectInput(cmd);
            return;
        }
        if (gameOver)
        {
            if (cmd.Kind == CommandKind.Restart) ShowClassSelect();
            return;
        }
        if (shopOpen)
        {
            HandleShopInput(cmd);
            return;
        }

        switch (cmd.Kind)
        {
            case CommandKind.Move: TryMovePlayer(cmd.Dir); break;
            case CommandKind.Wait: EndPlayerTurn(); break;
            case CommandKind.UseItem: UseConsumable(GameData.Consumables[cmd.Slot]); break;
            case CommandKind.Descend: TryDescend(); break;
            case CommandKind.Skill: UseSkill(); break;
            case CommandKind.ToggleHelp: showHelp = !showHelp; break;
        }
    }

    void LateUpdate()
    {
        if (player == null) return;
        var target = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
        var smooth = Vector3.Lerp(cam.transform.position - shakeOffset, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
        UpdateShake();
        cam.transform.position = smooth + shakeOffset;
    }

    // ───────────────────────── 런 / 층 생성 ─────────────────────────

    void BeginRun(PlayerClass playerClassId)
    {
        choosingClass = false;
        showingTitle = false;
        rng = new System.Random();
        depth = 1;
        level = 1;
        xp = 0;
        kills = 0;
        bossKills = 0;
        soulStonesEarned = 0;
        turn = 0;
        gold = 0;
        gameOver = false;
        newRecord = false;
        showHelp = true;
        helpAutoHidden = false;
        killedBy = null;
        killerKey = null;
        bestDepth = PlayerPrefs.GetInt(BestDepthKey, 0);
        log.Clear();
        floatingTexts.Clear();
        inventory.Clear();
        inventory[ItemKind.HealthPotion] = 2 + MetaBonus(MetaUpgrade.Potion);

        if (player != null) Destroy(player.gameObject);
        player = Actor.Create(Ko ? "당신" : "You", SpriteFactory.Player, Color.white, 3, transform);
        player.IsPlayer = true;
        ApplyClass(playerClassId);
        Play(Sfx.Start);

        Log(Ko ? $"{playerClass.Name}{Josa(playerClass.Name, "으로", "로")} 던전에 들어섰다. 노란 계단을 찾아 더 깊이 내려가자!"
               : $"You enter the dungeon as a {playerClass.Name}. Find the yellow stairs and go deeper!");
        Log(Ko ? $"X 키: {playerClass.SkillName} — {playerClass.SkillDesc}" : $"X key: {playerClass.SkillName} — {playerClass.SkillDesc}");
        BuildFloor();
    }

    void BuildFloor()
    {
        if (floorRoot != null) Destroy(floorRoot.gameObject);
        floorRoot = new GameObject($"Floor {depth}").transform;
        floorRoot.SetParent(transform, false);
        enemies.Clear();
        items.Clear();
        boss = null;
        bossSeen = false;
        turnsOnFloor = 0;
        ResetRoomState();

        var result = DungeonGenerator.Generate(MapWidth, MapHeight, rng);
        map = new DungeonMap(result.Tiles, floorRoot);
        rooms = result.Rooms;
        stairsPos = result.Stairs;
        player.SetPos(result.Start, snap: true);

        AssignRoomKinds();
        PopulateRooms();
        if (IsBossFloor) SpawnBoss();
        PlaceTraps();

        if (cam != null)
            cam.transform.position = new Vector3(result.Start.x, result.Start.y, -10f);
        map.ComputeFov(player.Pos, FovRadius);
        RefreshVisibility();
        RebuildMinimap();
    }

    bool TryRandomFreeCell(RectInt room, out Vector2Int cell)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            cell = new Vector2Int(rng.Next(room.xMin, room.xMax), rng.Next(room.yMin, room.yMax));
            if (map.Get(cell) == TileType.Floor && !IsOccupied(cell) && ItemAt(cell) == null && !traps.ContainsKey(cell))
                return true;
        }
        cell = default;
        return false;
    }

    static float SpawnWeight(EnemyTemplate t, int atDepth) => 1f / (1 + (atDepth - t.MinDepth) * 0.35f);

    Actor SpawnEnemy(Vector2Int pos)
    {
        // 출현 가능한 몬스터 중 최근에 등장한 종류일수록 자주 나온다.
        var candidates = new List<EnemyTemplate>();
        var weights = new List<float>();
        foreach (var t in GameData.Enemies)
        {
            if (t.MinDepth > depth) continue;
            candidates.Add(t);
            weights.Add(SpawnWeight(t, depth));
        }
        float roll = (float)rng.NextDouble() * weights.Sum();
        int index = 0;
        while (index < weights.Count - 1 && (roll -= weights[index]) > 0) index++;

        var enemy = Actor.FromTemplate(candidates[index], depth, floorRoot);
        enemy.SetPos(pos, snap: true);
        enemies.Add(enemy);
        return enemy;
    }

    // 보스는 계단이 있는 마지막 방에서 계단을 지키고 있다.
    void SpawnBoss()
    {
        // 어느 보스가 어떤 능력치로 나올지는 GameData.BossFor가 정한다(반복 회차는 색이 바뀌고 더 강하다).
        var spec = GameData.BossFor(depth);
        boss = Actor.FromTemplate(spec.Template, spec.Template.MinDepth, floorRoot);
        boss.IsBoss = true;
        boss.DisplayName = spec.DisplayName;
        boss.MaxHp = boss.Hp = spec.Hp;
        boss.Attack = spec.Attack;
        boss.Defense = spec.Defense;
        boss.XpReward = spec.Xp;
        if (spec.Round > 0)
        {
            var recolored = PixelArt.Get(spec.Template.Name, GameData.VariantHue(spec.Round));
            if (recolored != null) boss.SetSprite(recolored);
        }

        var bossRoom = rooms[^1];
        var pos = stairsPos;
        foreach (var dir in Pathfinder.Directions8)
        {
            var candidate = stairsPos + dir;
            if (bossRoom.Contains(candidate) && map.Get(candidate) == TileType.Floor && !IsOccupied(candidate))
            {
                pos = candidate;
                break;
            }
        }
        boss.SetPos(pos, snap: true);
        enemies.Add(boss);
        Log(spec.IsFinal
            ? (Ko ? "<color=#c58cff>공허가 숨을 쉰다... 이 층의 주인이 계단을 봉인하고 있다.</color>"
                  : "<color=#c58cff>The void is breathing... The master of this floor has sealed the stairs.</color>")
            : (Ko ? "강력한 기운이 느껴진다... 계단이 봉인되어 있다." : "You sense a powerful presence... The stairs are sealed."));
    }

    // ───────────────────────── 보조 함수 ─────────────────────────

    void RefreshVisibility()
    {
        bool newEnemySpotted = false;
        foreach (var enemy in enemies)
        {
            bool isVisible = map.IsVisible(enemy.Pos);
            if (isVisible && !enemy.IsShown)
            {
                newEnemySpotted = true;
                if (enemy.IsBoss && !bossSeen)
                {
                    bossSeen = true;
                    Log(Ko ? $"<color=#ffcc44>보스 {enemy.DisplayName}{Josa(enemy.DisplayName, "이", "가")} 나타났다!</color>"
                           : $"<color=#ffcc44>A boss appears: the {enemy.DisplayName}!</color>");
                    Shake(0.4f, 0.25f);
                    Play(Sfx.BossAppear);
                }
                else if (!enemy.IsBoss)
                {
                    Log(Ko ? $"{enemy.DisplayName}{Josa(enemy.DisplayName, "이", "가")} 나타났다!" : $"{Cap(Loc.A(enemy.DisplayName))} appears!");
                }
            }
            enemy.SetVisible(isVisible);
        }
        foreach (var item in items)
            item.SetVisible(map.IsExplored(item.Pos));
        if (merchant != null) merchant.SetVisible(map.IsExplored(merchant.Pos));
        RefreshTraps();
        CheckRoomDiscovery();

        // 새 몬스터가 보이면 꾹 누르기 이동을 멈춰서 갑자기 부딪히지 않게 한다.
        if (newEnemySpotted) input.CancelRepeat();
        UpdateMinimap();
    }

    Actor EnemyAt(Vector2Int pos)
    {
        foreach (var enemy in enemies)
            if (enemy.Pos == pos) return enemy;
        return null;
    }

    Item ItemAt(Vector2Int pos)
    {
        foreach (var item in items)
            if (item.Pos == pos) return item;
        return null;
    }

    bool IsOccupied(Vector2Int pos) => player.Pos == pos || EnemyAt(pos) != null || IsMerchantAt(pos);

    int Count(ItemKind kind) => inventory.TryGetValue(kind, out var n) ? n : 0;

    static bool Ko => Loc.IsKorean;

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

    // 받침 유무에 따라 조사(이/가, 을/를)를 고른다. 한국어 문장에서만 쓴다.
    static string Josa(string word, string withBatchim, string withoutBatchim)
    {
        char last = word[^1];
        if (last < 0xAC00 || last > 0xD7A3) return withoutBatchim;
        int batchim = (last - 0xAC00) % 28;
        if (batchim == 8 && withBatchim == "으로") return withoutBatchim; // ㄹ 받침은 '로' (예: 칼로)
        return batchim != 0 ? withBatchim : withoutBatchim;
    }

    void Log(string message)
    {
        log.Add(message);
        if (log.Count > MaxLogLines) log.RemoveAt(0);
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7f; // 한 칸이 크게 보여 픽셀아트가 잘 보이도록
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
    }
}
