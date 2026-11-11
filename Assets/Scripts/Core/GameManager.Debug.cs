using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 개발·검증용 자동 플레이. 실행 인자로 켠다.
//   -autoshot <경로>    봇이 플레이하면서 타이틀/직업 선택/전투/보스/마지막 장면을 <경로>_combat.png 등으로 저장하고 종료
//   -simulate <횟수>    화면 없이 여러 판을 빠르게 돌려 도달 층수 통계를 로그에 남기고 종료 (밸런스 확인용)
//   -startdepth <층>    해당 층에서, 그 층에 맞게 성장한 상태로 시작
//   -class <직업>       warrior / rogue / mage 중 하나로 고정 (없으면 시뮬레이션은 직업을 돌아가며, 스크린샷은 무작위)
//   -bossonly          (-simulate, -startdepth <보스 층>과 함께) 그 층의 다른 몬스터를 치우고 보스전 승률만 본다
//   -lang <ko|en>      언어 고정 (저장된 선택은 바꾸지 않는다. GameManager.Awake에서 읽는다)
public partial class GameManager
{
    const int SimMaxStepsPerRun = 8000;
    const int StuckTurnsOnFloor = 900;

    // -startdepth 근사에 쓰는 값 (BeginDebugRun).
    const float DebugKillsBase = 6f, DebugKillsPerDepth = 0.9f, DebugKillsMax = 26f;   // 층당 처치 수
    const int DebugFloorsPerBlessing = 14;
    const int DebugGearRolls = 3;

    bool autopilotActive;
    int debugStartDepth = 1;
    PlayerClass? debugClass;
    bool debugBossOnly;
    bool botShopVisited;
    int botShopsUsed;

    void StartDebugModes()
    {
        if (TryGetArg("-startdepth", out var d) && int.TryParse(d, out var startDepth)) debugStartDepth = Mathf.Max(1, startDepth);
        if (TryGetArg("-class", out var c) && System.Enum.TryParse<PlayerClass>(c, true, out var cls)) debugClass = cls;
        debugBossOnly = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-bossonly") >= 0;
        if (TryGetArg("-simulate", out var n) && int.TryParse(n, out var runs)) StartCoroutine(SimulateRuns(runs));
        else if (TryGetArg("-gallery", out var galleryPath)) StartCoroutine(GalleryShot(galleryPath));
        else if (TryGetArg("-autoshot", out var path)) StartCoroutine(AutoShot(path));
    }

    bool galleryMode;

    IEnumerator GalleryShot(string path)
    {
        autopilotActive = true;
        galleryMode = true;
        yield return new WaitForSeconds(0.5f);
        yield return Capture(path);
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    // 봇이 플레이하거나 -startdepth로 시작한 판. 최고 기록·기록 화면·영혼석에 반영하지 않는다.
    bool IsTestRun => autopilotActive || debugStartDepth > 1;

    // -startdepth가 없으면 BeginRun과 똑같다. 직업 선택 화면에서 사람이 시작할 때도 이 함수를 거친다.
    void BeginDebugRun(PlayerClass cls)
    {
        BeginRun(cls);
        if (debugStartDepth <= 1) return;

        // 1층부터 내려왔다면 쌓였을 성장을 근사한다. 수치는 1층에서 시작한 봇의 층별 실측치([SIM] arrive 줄)에 맞췄다.
        for (int d = 1; d < debugStartDepth; d++)
        {
            // 경험치: 그 층에서 잡았을 몬스터 수 × 그 층 몬스터의 평균 경험치(+ 보스).
            float weightSum = 0, xpSum = 0;
            foreach (var t in GameData.Enemies)
            {
                if (t.MinDepth > d) continue;
                float w = SpawnWeight(t, d);
                weightSum += w;
                xpSum += w * GameData.EnemyXp(t, d);
            }
            xp += Mathf.RoundToInt(Mathf.Min(DebugKillsBase + d * DebugKillsPerDepth, DebugKillsMax) * xpSum / weightSum);
            if (d % GameData.BossFloorInterval == 0) xp += GameData.BossFor(d).Xp;
            while (xp >= XpToNextLevel)
            {
                xp -= XpToNextLevel;
                level++;
                AddBaseStats(maxHp: playerClass.HpPerLevel, attack: 1);
            }
            // 제단 축복(체력·공격·방어)은 평균적으로 이 정도 간격으로 한 번씩 받는다.
            if (d % DebugFloorsPerBlessing == 0) AddBaseStats(maxHp: 8, attack: 2, defense: 1);
        }
        // 장비는 최근 몇 층에서 주웠을 것 중 가장 좋은 것을 칸마다 쥐여준다.
        foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
        {
            Equipment best = null;
            for (int back = 1; back <= DebugGearRolls; back++)
            {
                var candidate = EquipmentFactory.Generate(rng, Mathf.Max(1, debugStartDepth - back), slot: slot);
                if (best == null || candidate.Score > best.Score) best = candidate;
            }
            SetEquipped(slot, best);
        }
        player.Hp = player.MaxHp;
        // 소모품과 골드도 실측치에 맞춘다: 물약은 10층 무렵 가장 많이 쌓였다가(약 20개) 깊어질수록 줄어든다.
        int d0 = debugStartDepth;
        inventory[ItemKind.HealthPotion] = d0 < 10 ? 2 + d0 * 2 : Mathf.Clamp(30 - d0, 10, 20);
        inventory[ItemKind.TeleportScroll] = d0 * 7 / 10;
        inventory[ItemKind.FireScroll] = Mathf.Max(Count(ItemKind.FireScroll), 1);
        gold = d0 * 70;
        depth = debugStartDepth;
        BuildFloor();

        // -bossonly: 보스전만 따로 보려고 그 층의 다른 몬스터를 치운다.
        if (debugBossOnly && boss != null)
        {
            foreach (var enemy in enemies.Where(e => e != boss).ToList())
            {
                enemies.Remove(enemy);
                Destroy(enemy.gameObject);
            }
        }
        Log(Ko ? $"<color=#99ddff>테스트 시작: 지하 {depth}층에 맞게 성장한 상태다. 이 판은 기록과 영혼석에 남지 않는다.</color>"
               : $"<color=#99ddff>Test start: grown to match floor {depth}. This run is not saved to records or Soul Stones.</color>");
    }

    // ───────────────────────── 자동 플레이 봇 ─────────────────────────

    void AutopilotStep()
    {
        if (gameOver) return;

        if (shopOpen) { BotShop(); return; }

        // 보스의 내려찍기 범위 안이면 무엇보다 먼저 빠져나온다.
        var charging = enemies.FirstOrDefault(e => e.SlamCharging);
        if (charging != null && Pathfinder.Distance(charging.Pos, player.Pos) <= charging.Template.SlamRadius)
        {
            int radius = charging.Template.SlamRadius;
            var escape = Pathfinder.Directions8.Select(dir => player.Pos + dir)
                .Where(cell => map.CanStep(player.Pos, cell) && !IsOccupied(cell) && Pathfinder.Distance(cell, charging.Pos) > radius)
                .Select(cell => (Vector2Int?)cell).FirstOrDefault();
            if (escape.HasValue) { TryMovePlayer(escape.Value - player.Pos); return; }
        }

        if (player.Hp < player.MaxHp * 0.4f && Count(ItemKind.HealthPotion) > 0 && UseAndCheck(ItemKind.HealthPotion)) return;

        Actor nearest = null;
        foreach (var e in enemies)
            if (map.IsVisible(e.Pos) && (nearest == null || DistanceToPlayer(e) < DistanceToPlayer(nearest)))
                nearest = e;

        if (nearest != null)
        {
            if (SkillReady && BotWantsSkill(nearest) && UseSkillAndCheck()) return;
            if (Count(ItemKind.FireScroll) > 0 && (nearest.IsBoss || player.Hp < player.MaxHp / 2) && UseAndCheck(ItemKind.FireScroll)) return;
            if (player.Hp < player.MaxHp / 4 && Count(ItemKind.TeleportScroll) > 0 && UseAndCheck(ItemKind.TeleportScroll)) return;
            // 벽 모서리 너머 대각선에 있는 적은 때릴 수 없으니 돌아서 다가간다.
            if (map.CanStep(player.Pos, nearest.Pos)) { TryMovePlayer(nearest.Pos - player.Pos); return; }
            if (BotStep(p => p == nearest.Pos)) return;
        }

        if (turnsOnFloor <= 1 && Count(ItemKind.MapScroll) > 0 && UseAndCheck(ItemKind.MapScroll)) return;

        // 주변이 안전하고 체력이 낮으면 쉬면서 회복한다.
        if (nearest == null && player.Hp < player.MaxHp * 0.6f && turnsOnFloor < 500) { EndPlayerTurn(); return; }

        // 상점, 제단, 눈에 보이는 아이템·금화를 챙긴다.
        if (merchant != null && !botShopVisited && map.IsExplored(merchant.Pos) && shopOffers.Any(o => o.Stock > 0 && o.Price <= gold)
            && BotStep(p => IsMerchantAt(p))) return;
        if (altarPos.HasValue && map.IsExplored(altarPos.Value) && BotStep(p => p == altarPos.Value)) return;
        if (turnsOnFloor < 400 && BotStep(p => map.IsExplored(p) && ItemAt(p) != null)) return;

        if (player.Pos == stairsPos && !StairsSealed) { TryDescend(); return; }
        if (turnsOnFloor < 250 && BotStep(p => !map.IsExplored(p))) return;
        if (StairsSealed && BotStep(p => p == boss.Pos)) return;
        if (BotStep(p => p == stairsPos)) return;
        EndPlayerTurn();
    }

    bool UseAndCheck(ItemKind kind)
    {
        int before = turn;
        UseConsumable(kind);
        return turn != before;
    }

    bool UseSkillAndCheck()
    {
        int before = turn;
        UseSkill();
        return turn != before;
    }

    // 직업별로 스킬을 쓸 만한 상황인지 판단한다.
    bool BotWantsSkill(Actor nearest)
    {
        int adjacent = enemies.Count(e => map.CanStep(player.Pos, e.Pos));
        return playerClass.Id switch
        {
            PlayerClass.Warrior => adjacent >= 2 || (adjacent == 1 && nearest.IsBoss),
            PlayerClass.Rogue => (player.Hp < player.MaxHp / 2 && adjacent > 0) || (nearest.IsBoss && DistanceToPlayer(nearest) <= 3),
            _ => true, // 마법사는 쿨다운마다 마법 화살
        };
    }

    bool BotStep(System.Func<Vector2Int, bool> isGoal)
    {
        // 발견한 함정은 피해 가되, 함정을 밟지 않고는 못 가는 길이면 그냥 지나간다.
        var step = Pathfinder.FirstStep(map, player.Pos, isGoal,
                       p => EnemyAt(p) != null || IsMerchantAt(p) || IsRevealedTrap(p))
                   ?? Pathfinder.FirstStep(map, player.Pos, isGoal, p => EnemyAt(p) != null || IsMerchantAt(p))
                   // 보이지 않는 몬스터가 외길을 막고 있으면 영영 기다리게 되므로, 마지막에는 보이는 몬스터만 피해서 간다.
                   ?? Pathfinder.FirstStep(map, player.Pos, isGoal, p => (EnemyAt(p) != null && map.IsVisible(p)) || IsMerchantAt(p));
        if (!step.HasValue) return false;
        int before = turn;
        TryMovePlayer(step.Value - player.Pos);
        return turn != before || shopOpen;
    }

    // 지금보다 좋은 장비 → 물약 → 두루마리 순으로 살 수 있는 만큼 산다.
    void BotShop()
    {
        for (int i = 0; i < shopOffers.Count; i++)
        {
            var eq = shopOffers[i].Equipment;
            if (eq == null || shopOffers[i].Stock <= 0 || shopOffers[i].Price > gold) continue;
            var current = GetEquipped(eq.Slot);
            if (current != null && eq.Score <= current.Score) continue;
            Buy(i);
            botShopsUsed++;
        }

        var priority = new[] { ItemKind.HealthPotion, ItemKind.FireScroll, ItemKind.TeleportScroll };
        foreach (var kind in priority)
        {
            int index = shopOffers.FindIndex(o => o.Kind == kind);
            while (index >= 0 && shopOffers[index].Stock > 0 && shopOffers[index].Price <= gold)
            {
                Buy(index);
                botShopsUsed++;
            }
        }
        botShopVisited = true;
        CloseShop();
    }

    int DistanceToPlayer(Actor a) => Pathfinder.Distance(a.Pos, player.Pos);

    // ───────────────────────── 대각선 모서리 규칙 검사 ─────────────────────────

    int cornerMoveViolations, cornerMeleeViolations, checkedSteps, checkedMelee;

    // 봇 플레이 동안 모든 한 칸 이동과 근접 공격이 DungeonMap.CanStep을 지키는지 센다.
    void StartCornerChecks()
    {
        CornerRuleSelfTest();
        InputSelfTest();
        Actor.Stepped = (actor, from, to) =>
        {
            // 소환·순간이동은 한 걸음이 아니다. (0,0)은 갓 만들어져 아직 자리가 없는 몬스터(그 칸은 항상 벽).
            if (map == null || from == Vector2Int.zero || Pathfinder.Distance(from, to) != 1) return;
            checkedSteps++;
            if (map.CanStep(from, to)) return;
            cornerMoveViolations++;
            Debug.Log($"[CHECK] corner move violation: {actor.DisplayName} {from} -> {to}");
        };
    }

    void CheckMelee(Actor attacker, Actor defender)
    {
        checkedMelee++;
        if (map.CanStep(attacker.Pos, defender.Pos)) return;
        cornerMeleeViolations++;
        Debug.Log($"[CHECK] corner melee violation: {attacker.DisplayName} {attacker.Pos} -> {defender.DisplayName} {defender.Pos}");
    }

    void ReportCornerChecks() =>
        Debug.Log($"[CHECK] corner rule in play: steps {checkedSteps} (violations {cornerMoveViolations}), " +
                  $"melee attacks {checkedMelee} (violations {cornerMeleeViolations})");

    // 작은 지도를 직접 만들어 "벽 모서리 끼고 대각선" 상황을 재현한다. '.'은 바닥, '#'은 벽, 아래 줄이 y=0.
    void CornerRuleSelfTest()
    {
        var failures = new List<string>();
        void Expect(bool ok, string what) { if (!ok) failures.Add(what); }
        Vector2Int a = new(1, 1), b = new(2, 2);
        System.Func<Vector2Int, bool> never = _ => false;

        // ㄱ자로 꺾인 복도: (1,1) → (2,1) → (2,2). (1,2)는 벽이라 (1,1)↔(2,2) 대각선은 모서리를 끼게 된다.
        var bend = TestMap("####", "##.#", "#..#", "####");
        Expect(!bend.CanStep(a, b) && !bend.CanStep(b, a), "bend: diagonal past one wall corner must be blocked");
        Expect(bend.CanStep(a, new Vector2Int(2, 1)), "bend: orthogonal step must be allowed");
        Expect(Pathfinder.FirstStep(bend, a, p => p == b, never) == new Vector2Int(2, 1), "bend: path must go around the corner");
        bend.ComputeFov(a, FovRadius);
        Expect(bend.IsVisible(b), "bend: the cell around the corner stays visible");

        // 대각선으로만 맞닿은 두 칸: 사이가 모두 벽이라 걸어갈 수도, 때릴 수도, 볼 수도 없다.
        var crack = TestMap("####", "##.#", "#.##", "####");
        Expect(!crack.CanStep(a, b), "crack: diagonal between two walls must be blocked");
        Expect(Pathfinder.FirstStep(crack, a, p => p == b, never) == null, "crack: no path through the crack");
        crack.ComputeFov(a, FovRadius);
        Expect(!crack.IsVisible(b), "crack: floor beyond the crack must not be visible");
        Expect(crack.IsVisible(new Vector2Int(2, 1)) && crack.IsVisible(new Vector2Int(0, 0)), "crack: walls next to the viewer stay visible");

        // 뚫린 곳에서는 대각선이 그대로 된다.
        var open = TestMap("####", "#..#", "#..#", "####");
        Expect(open.CanStep(a, b) && open.CanStep(b, a), "open: diagonal must be allowed");
        Expect(Pathfinder.FirstStep(open, a, p => p == b, never) == b, "open: diagonal is the shortest path");
        Expect(!open.CanStep(a, a) && !open.CanStep(a, new Vector2Int(3, 1)) && !open.CanStep(new Vector2Int(1, 1), new Vector2Int(1, 3)),
            "open: same cell, walls and distant cells are not a step");

        Debug.Log(failures.Count == 0
            ? "[CHECK] corner rule self-test: passed (3 maps)"
            : "[CHECK] corner rule self-test: FAILED — " + string.Join("; ", failures));
    }

    // 가짜 키보드와 가짜 시계로 InputReader를 돌려 꾹 누르기 규칙을 확인한다(실제 키보드는 사람이 확인해야 한다).
    void InputSelfTest()
    {
        var failures = new List<string>();
        var down = new HashSet<KeyCode>();
        var pressed = new HashSet<KeyCode>();
        float now = 0f;
        var reader = new InputReader { KeyHeld = k => down.Contains(k), KeyPressed = k => pressed.Contains(k), Now = () => now };

        void Press(KeyCode k) { down.Add(k); pressed.Add(k); }
        // 60fps로 seconds 동안 돌리며 나온 명령을 모은다. 방금 누른 키는 첫 프레임에만 "눌림"으로 보인다.
        List<PlayerCommand> Run(float seconds)
        {
            var got = new List<PlayerCommand>();
            for (int frames = Mathf.Max(1, Mathf.RoundToInt(seconds * 60f)); frames > 0; frames--)
            {
                var cmd = reader.Read();
                if (cmd.Kind != CommandKind.None) got.Add(cmd);
                pressed.Clear();
                now += 1f / 60f;
            }
            return got;
        }
        bool AllMoves(List<PlayerCommand> got, Vector2Int dir, int atLeast) =>
            got.Count >= atLeast && got.All(c => c.Kind == CommandKind.Move && c.Dir == dir);
        void Expect(bool ok, string what) { if (!ok) failures.Add(what); }

        // 1) 누르면 바로 한 걸음, 꾹 누르면 잠깐 뒤부터 계속.
        Press(KeyCode.W);
        Expect(AllMoves(Run(1f / 60f), Vector2Int.up, 1), "tap moves once at once");
        Expect(Run(0.2f).Count == 0, "no repeat before the delay");
        Expect(AllMoves(Run(1f), Vector2Int.up, 8), "holding keeps moving");

        // 2) 누른 채 다른 방향 키를 눌렀다 떼도 끊기지 않는다(예전에는 여기서 멈췄다).
        Press(KeyCode.D);
        Expect(AllMoves(Run(0.5f), Vector2Int.right, 2), "the newer key takes over");
        down.Remove(KeyCode.D);
        Expect(AllMoves(Run(1f), Vector2Int.up, 8), "releasing the newer key resumes the older held key");

        // 3) 일부러 멈춘 뒤에는 계속 누르고 있어도 움직이지 않고, 뗐다가 다시 누르면 움직인다.
        Expect(reader.CancelRepeat(), "cancel reports that a held move was interrupted");
        Expect(!reader.CancelRepeat(), "a second stop in the same turn is not reported again");
        Expect(Run(1f).Count == 0, "no moves after a stop while the key stays down");
        Expect(!reader.ConsumeStopCue(), "no second cue when the stop was already reported");
        down.Remove(KeyCode.W);
        Run(0.1f);
        Press(KeyCode.W);
        Expect(AllMoves(Run(0.6f), Vector2Int.up, 3), "release and press again resumes");
        down.Clear();
        Run(0.1f);

        // 4) 막 누른 첫 걸음에서 멈춘 경우: 계속 누르고 있으면 잠시 뒤 표시를 한 번 낸다.
        Press(KeyCode.A);
        Expect(AllMoves(Run(1f / 60f), Vector2Int.left, 1), "first step of a new press");
        Expect(!reader.CancelRepeat(), "a stop on the first step is not reported at once");
        Expect(!reader.ConsumeStopCue(), "cue waits while the key might just be a tap");
        Expect(Run(0.4f).Count == 0, "no moves after the stop");
        Expect(reader.ConsumeStopCue() && !reader.ConsumeStopCue(), "cue fires once when the key is still held");
        down.Clear();
        Run(0.1f);

        // 5) 톡 누르고 뗀 뒤 멈추면 표시가 없다.
        Press(KeyCode.A);
        Run(1f / 60f);
        down.Clear();
        reader.CancelRepeat();
        Run(0.5f);
        Expect(!reader.ConsumeStopCue(), "no cue after a tap");

        // 6) 다른 명령 키와 같은 프레임에 눌린 이동 키도 놓치지 않는다.
        Press(KeyCode.Space);
        Press(KeyCode.S);
        var both = Run(2f / 60f);
        Expect(both.Count == 2 && both[0].Kind == CommandKind.Wait && both[1].Kind == CommandKind.Move && both[1].Dir == Vector2Int.down,
            "a move key pressed together with another command still moves");

        down.Clear();
        Run(0.1f);

        // 7) 화면이 바뀔 때(직업 선택 → 던전, 새 층): 다른 키와 함께 누르고 있던 방향 키는 뗐다 누르기 전까지 무시한다.
        Press(KeyCode.Return);
        Press(KeyCode.D);
        var start = Run(1f / 60f);
        Expect(start.Count == 1 && start[0].Kind == CommandKind.Descend, "the other command wins the shared frame");
        reader.CancelRepeat(); // BeginRun / TryDescend가 하는 일
        Expect(Run(1f).Count == 0, "a key held across a screen change does not move");
        down.Remove(KeyCode.D);
        Run(0.1f);
        Press(KeyCode.D);
        Expect(AllMoves(Run(1f / 60f), Vector2Int.right, 1), "pressing again after the screen change moves");

        Debug.Log(failures.Count == 0
            ? "[CHECK] input hold self-test: passed (7 cases, simulated keyboard)"
            : "[CHECK] input hold self-test: FAILED — " + string.Join("; ", failures));
    }

    DungeonMap TestMap(params string[] rowsTopDown)
    {
        int h = rowsTopDown.Length, w = rowsTopDown[0].Length;
        var tiles = new TileType[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tiles[x, y] = rowsTopDown[h - 1 - y][x] == '.' ? TileType.Floor : TileType.Wall;
        var root = new GameObject("CornerTestMap");
        root.SetActive(false);
        var testMap = new DungeonMap(tiles, root.transform);
        Destroy(root);
        return testMap;
    }

    // ───────────────────────── 스크린샷 검증 ─────────────────────────

    IEnumerator AutoShot(string basePath)
    {
        autopilotActive = true;
        StartCornerChecks();
        yield return new WaitForSeconds(0.5f);
        yield return Capture(basePath + "_title.png");
        yield return new WaitForSeconds(0.2f);
        showingTitle = false;
        yield return Capture(basePath + "_select.png");
        yield return new WaitForSeconds(0.2f);
        OpenMeta();
        yield return Capture(basePath + "_meta.png");
        yield return new WaitForSeconds(0.2f);
        metaOpen = false;
        // 기록 화면: 실제 저장된 기록과, 열 판을 꽉 채운 예시(저장하지 않음)를 각각 찍는다.
        OpenRecords();
        yield return Capture(basePath + "_records.png");
        recordsPreview = SampleRecords();
        yield return Capture(basePath + "_records_sample.png");
        recordsPreview = null;
        recordsOpen = false;

        BeginDebugRun(debugClass ?? (PlayerClass)Random.Range(0, GameData.Classes.Length));
        yield return new WaitForSeconds(0.5f);

        bool combatShot = false, bossShot = false, shopShot = false, roomShot = false, slamShot = false;
        // 사람이 키를 꾹 누를 때와 비슷한 속도(초당 약 15턴)로 플레이한다.
        int maxSteps = TryGetArg("-steps", out var stepsArg) && int.TryParse(stepsArg, out var s) ? s : 1000;
        var stepTimer = new System.Diagnostics.Stopwatch();
        int startFrame = Time.frameCount;
        float startTime = Time.realtimeSinceStartup;
        for (int i = 0; i < maxSteps && !gameOver; i++)
        {
            int roomsBefore = specialRoomsDiscovered;
            stepTimer.Start();
            AutopilotStep();
            stepTimer.Stop();
            yield return new WaitForSeconds(0.06f);

            if (!slamShot && enemies.Any(e => e.SlamCharging))
            {
                slamShot = true;
                yield return new WaitForSeconds(0.25f);
                yield return Capture(basePath + "_slam.png");
            }

            if (!shopShot && shopOpen)
            {
                shopShot = true;
                yield return Capture(basePath + "_shop.png");
            }
            if (!roomShot && specialRoomsDiscovered > roomsBefore && !shopOpen)
            {
                roomShot = true;
                yield return new WaitForSeconds(0.2f);
                yield return Capture(basePath + "_room.png");
            }

            // 공격 모션이 가장 앞으로 나간 순간(약 0.1초 뒤)을 찍는다.
            if (!combatShot && turn > 3 && floatingTexts.Count > 0 && enemies.Any(e => DistanceToPlayer(e) == 1))
            {
                combatShot = true;
                yield return new WaitForSeconds(0.04f);
                yield return Capture(basePath + "_combat.png");
            }
            if (!bossShot && bossSeen)
            {
                bossShot = true;
                yield return new WaitForSeconds(0.3f);
                yield return Capture(basePath + "_boss.png");
            }
        }

        float elapsed = Time.realtimeSinceStartup - startTime;
        int frames = Time.frameCount - startFrame;
        Debug.Log($"[PERF] turns {turn}, frames {frames}, avg fps {frames / elapsed:F1}, " +
                  $"avg turn logic {stepTimer.Elapsed.TotalMilliseconds / Mathf.Max(1, turn):F2} ms, total {elapsed:F1}s");

        ReportCornerChecks();
        yield return new WaitForSeconds(0.8f);
        yield return Capture(basePath + "_end.png");
        Debug.Log($"[AUTOSHOT] class {playerClass.Name}, depth {depth}, level {level}, kills {kills}, turns {turn}, gameOver {gameOver}, killedBy {killedBy}");
        Application.Quit();
    }

    IEnumerator Capture(string path)
    {
        if (player != null && map != null)
            Debug.Log($"[SHOT] {System.IO.Path.GetFileName(path)}: player {player.Pos} tile {map.Get(player.Pos)} " +
                      $"visible {map.IsVisible(player.Pos)} transform {player.transform.position} cam {cam.transform.position}");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        yield return null;
    }

    // ───────────────────────── 밸런스 시뮬레이션 ─────────────────────────

    IEnumerator SimulateRuns(int runs)
    {
        autopilotActive = true;
        StartCornerChecks();
        var results = new List<int>();
        var byClass = new Dictionary<PlayerClass, List<int>>();
        // 층에 도착했을 때의 능력치 합계: [0]판 수 [1]레벨 [2]공격 [3]방어 [4]최대 체력 [5]무기 공격 [6]누적 처치
        var arrivals = new SortedDictionary<int, long[]>();
        int bossFights = 0, bossWins = 0;
        long bossTurns = 0, bossPotions = 0, bossHpLeft = 0, totalKills = 0, totalTurns = 0;
        string lastBossName = null;
        void RecordArrival()
        {
            if (!arrivals.TryGetValue(depth, out var sum)) arrivals[depth] = sum = new long[12];
            sum[0]++; sum[1] += level; sum[2] += player.Attack; sum[3] += player.Defense; sum[4] += player.MaxHp;
            sum[5] += weapon?.Attack ?? 0; sum[6] += kills;
            sum[7] += Count(ItemKind.HealthPotion); sum[8] += Count(ItemKind.FireScroll); sum[9] += Count(ItemKind.TeleportScroll);
            sum[10] += gold; sum[11] += player.Hp * 100 / player.MaxHp;
        }

        for (int r = 0; r < runs; r++)
        {
            botShopsUsed = 0;
            var cls = debugClass ?? (PlayerClass)(r % GameData.Classes.Length);
            BeginDebugRun(cls);
            int lastDepth = depth;
            RecordArrival();
            bool bossFight = debugBossOnly && boss != null;
            int potionsAtStart = Count(ItemKind.HealthPotion);
            string bossName = boss != null ? boss.DisplayName : null;
            if (bossFight) bossFights++;

            for (int step = 0; step < SimMaxStepsPerRun && !gameOver; step++)
            {
                if (bossFight && !StairsSealed)
                {
                    // 보스를 쓰러뜨렸다. 보스전 결과만 남기고 이 판을 끝낸다.
                    bossWins++;
                    bossTurns += turnsOnFloor;
                    bossPotions += potionsAtStart - Count(ItemKind.HealthPotion);
                    bossHpLeft += player.Hp * 100 / player.MaxHp;
                    break;
                }
                AutopilotStep();
                if (turnsOnFloor > StuckTurnsOnFloor)
                {
                    killedBy = "(막힘)";
                    // 왜 막혔는지 알 수 있게 상황을 남긴다: 보이는 적, 계단까지 길이 있는지.
                    var seen = enemies.Where(e => map.IsVisible(e.Pos))
                        .Select(e => $"{e.Template?.NameEn ?? e.DisplayName}@{e.Pos} range {e.Range} hp {e.Hp}/{e.MaxHp}");
                    bool stairsPath = Pathfinder.FirstStep(map, player.Pos, p => p == stairsPos, p => EnemyAt(p) != null || IsMerchantAt(p)).HasValue;
                    bool stairsPathIgnoringActors = Pathfinder.FirstStep(map, player.Pos, p => p == stairsPos, _ => false).HasValue;
                    bool stairsPathAvoidingTraps = Pathfinder.FirstStep(map, player.Pos, p => p == stairsPos, p => IsRevealedTrap(p)).HasValue;
                    var knownTraps = traps.Where(t => t.Value.Revealed).Select(t => $"{t.Value.Kind}@{t.Key}");
                    Debug.Log($"[SIM] stuck on floor {depth}: player {player.Pos} hp {player.Hp}/{player.MaxHp}, stairs {stairsPos} sealed {StairsSealed}, " +
                              $"path to stairs {stairsPath} (ignoring actors {stairsPathIgnoringActors}, avoiding known traps {stairsPathAvoidingTraps}), " +
                              $"enemies left {enemies.Count}, visible: {string.Join(", ", seen)}; known traps: {string.Join(", ", knownTraps)}; " +
                              $"altar {altarPos}, merchant {(merchant != null ? merchant.Pos.ToString() : "none")} visited {botShopVisited}, " +
                              $"items left {items.Count(it => map.IsExplored(it.Pos))}");
                    break;
                }
                if (depth != lastDepth)
                {
                    lastDepth = depth;
                    RecordArrival();
                    yield return null; // 이전 층 오브젝트가 실제로 파괴되도록 한 프레임 넘긴다.
                }
            }

            if (bossFight) lastBossName = bossName;
            totalKills += kills;
            totalTurns += turn;
            results.Add(depth);
            if (!byClass.ContainsKey(cls)) byClass[cls] = new List<int>();
            byClass[cls].Add(depth);
            Debug.Log($"[SIM] run {r + 1}: {playerClass.Name} depth {depth}, level {level}, atk {player.Attack}, def {player.Defense}, " +
                      $"hp {player.MaxHp}, kills {kills}, turns {turn}, gold {gold}, bought {botShopsUsed}, killedBy {killedBy ?? "(생존)"}");
            yield return null;
        }

        foreach (var (cls, depths) in byClass)
            Debug.Log($"[SIM] class {GameData.ClassOf(cls).Name}: runs {depths.Count}, avg depth {depths.Average():F1}, max {depths.Max()}");
        // 층별 도착 시점 평균(5층 단위와 시작 층). -startdepth 근사가 실제 성장과 맞는지, 깊은 층 수치가 어떤지 볼 때 쓴다.
        foreach (var (d, sum) in arrivals)
        {
            if (d % 5 != 0 && d != debugStartDepth) continue;
            float n = sum[0];
            Debug.Log($"[SIM] arrive floor {d}: runs {sum[0]}, level {sum[1] / n:F1}, atk {sum[2] / n:F1}, def {sum[3] / n:F1}, " +
                      $"hp {sum[4] / n:F0} ({sum[11] / n:F0}%), weapon {sum[5] / n:F1}, kills {sum[6] / n:F0}, " +
                      $"potions {sum[7] / n:F1}, fire {sum[8] / n:F1}, warp {sum[9] / n:F1}, gold {sum[10] / n:F0}, " +
                      $"monster hp {GameData.ReferenceHp(d)} atk {GameData.ReferenceAttack(d)} def {GameData.ReferenceDefense(d)}");
        }
        if (bossFights > 0)
        {
            var spec = GameData.BossFor(debugStartDepth);
            float wins = Mathf.Max(1, bossWins);
            Debug.Log($"[SIM] boss floor {debugStartDepth} {lastBossName} (hp {spec.Hp} atk {spec.Attack} def {spec.Defense}): " +
                      $"wins {bossWins}/{bossFights}, avg turns {bossTurns / wins:F0}, potions used {bossPotions / wins:F1}, hp left {bossHpLeft / wins:F0}%");
        }
        ReportCornerChecks();
        results.Sort();
        Debug.Log($"[SIM] summary: start {debugStartDepth}, floors gained {results.Average() - debugStartDepth:F1}, " +
                  $"kills per run {(float)totalKills / runs:F1}, turns per run {(float)totalTurns / runs:F0}, runs {runs}, avg depth {results.Average():F1}, median {results[results.Count / 2]}, " +
                  $"min {results[0]}, max {results[^1]}, reached boss1 {results.Count(x => x >= 5)}, beat boss1 {results.Count(x => x > 5)}");
        Application.Quit();
    }

    static bool TryGetArg(string name, out string value)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) { value = args[i + 1]; return true; }
        }
        value = null;
        return false;
    }
}
