using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 개발·검증용 자동 플레이. 실행 인자로 켠다.
//   -autoshot <경로>    봇이 플레이하면서 전투/보스/마지막 장면을 <경로>_combat.png 등으로 저장하고 종료
//   -simulate <횟수>    화면 없이 여러 판을 빠르게 돌려 도달 층수 통계를 로그에 남기고 종료 (밸런스 확인용)
//   -startdepth <층>    해당 층에서, 그 층에 맞게 성장한 상태로 시작
//   -class <직업>       warrior / rogue / mage 중 하나로 고정 (없으면 시뮬레이션은 직업을 돌아가며, 스크린샷은 무작위)
public partial class GameManager
{
    const int SimMaxStepsPerRun = 8000;
    const int StuckTurnsOnFloor = 900;

    bool autopilotActive;
    int debugStartDepth = 1;
    PlayerClass? debugClass;
    bool botShopVisited;
    int botShopsUsed;

    void StartDebugModes()
    {
        if (TryGetArg("-startdepth", out var d) && int.TryParse(d, out var startDepth)) debugStartDepth = Mathf.Max(1, startDepth);
        if (TryGetArg("-class", out var c) && System.Enum.TryParse<PlayerClass>(c, true, out var cls)) debugClass = cls;

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

    void BeginDebugRun(PlayerClass cls)
    {
        BeginRun(cls);
        if (debugStartDepth <= 1) return;

        for (int d = 1; d < debugStartDepth; d++)
        {
            level++;
            AddBaseStats(maxHp: playerClass.HpPerLevel, attack: 1, defense: d % 2 == 0 ? 1 : 0);
        }
        // 그 층에 걸맞은 장비도 하나씩 쥐여준다.
        foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            SetEquipped(slot, EquipmentFactory.Generate(rng, debugStartDepth - 1, slot: slot));
        player.Hp = player.MaxHp;
        inventory[ItemKind.HealthPotion] = 3;
        gold = debugStartDepth * 25;
        depth = debugStartDepth;
        BuildFloor();
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
                .Where(cell => map.IsWalkable(cell) && !IsOccupied(cell) && Pathfinder.Distance(cell, charging.Pos) > radius)
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
            if (DistanceToPlayer(nearest) == 1) { TryMovePlayer(nearest.Pos - player.Pos); return; }
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
        int adjacent = enemies.Count(e => DistanceToPlayer(e) == 1);
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
                   ?? Pathfinder.FirstStep(map, player.Pos, isGoal, p => EnemyAt(p) != null || IsMerchantAt(p));
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

    // ───────────────────────── 스크린샷 검증 ─────────────────────────

    IEnumerator AutoShot(string basePath)
    {
        autopilotActive = true;
        yield return new WaitForSeconds(0.5f);
        yield return Capture(basePath + "_select.png");
        yield return new WaitForSeconds(0.2f);

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
        var results = new List<int>();
        var byClass = new Dictionary<PlayerClass, List<int>>();

        for (int r = 0; r < runs; r++)
        {
            botShopsUsed = 0;
            var cls = debugClass ?? (PlayerClass)(r % GameData.Classes.Length);
            BeginDebugRun(cls);
            int lastDepth = depth;

            for (int step = 0; step < SimMaxStepsPerRun && !gameOver; step++)
            {
                AutopilotStep();
                if (turnsOnFloor > StuckTurnsOnFloor) { killedBy = "(막힘)"; break; }
                if (depth != lastDepth)
                {
                    lastDepth = depth;
                    yield return null; // 이전 층 오브젝트가 실제로 파괴되도록 한 프레임 넘긴다.
                }
            }

            results.Add(depth);
            if (!byClass.ContainsKey(cls)) byClass[cls] = new List<int>();
            byClass[cls].Add(depth);
            Debug.Log($"[SIM] run {r + 1}: {playerClass.Name} depth {depth}, level {level}, atk {player.Attack}, def {player.Defense}, " +
                      $"hp {player.MaxHp}, kills {kills}, turns {turn}, gold {gold}, bought {botShopsUsed}, killedBy {killedBy ?? "(생존)"}");
            yield return null;
        }

        foreach (var (cls, depths) in byClass)
            Debug.Log($"[SIM] class {GameData.ClassOf(cls).Name}: runs {depths.Count}, avg depth {depths.Average():F1}, max {depths.Max()}");
        results.Sort();
        Debug.Log($"[SIM] summary: runs {runs}, avg depth {results.Average():F1}, median {results[results.Count / 2]}, " +
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
