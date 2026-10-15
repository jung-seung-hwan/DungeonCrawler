using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 방 종류 배정(상점, 보물 창고, 제단), 방 채우기, 함정.
public partial class GameManager
{
    class Trap
    {
        public TrapKind Kind;
        public bool Revealed;
        public SpriteRenderer Renderer;
    }

    const float TrapSpotChance = 0.2f;   // 함정 옆에 있을 때 매 턴 발견할 확률

    readonly Dictionary<Vector2Int, Trap> traps = new();
    readonly HashSet<int> discoveredRooms = new();
    readonly List<RoomKind> roomKinds = new();
    Vector2Int? altarPos;
    int specialRoomsDiscovered;

    void ResetRoomState()
    {
        traps.Clear();
        slamWarnings.Clear();
        discoveredRooms.Clear();
        roomKinds.Clear();
        altarPos = null;
        merchant = null;
        shopOpen = false;
        shopOffers.Clear();
        botShopVisited = false;
    }

    // 첫 방은 시작 방, 마지막 방은 계단 방. 나머지 중 일부를 특수 방으로 만든다.
    void AssignRoomKinds()
    {
        roomKinds.AddRange(Enumerable.Repeat(RoomKind.Normal, rooms.Count));
        roomKinds[0] = RoomKind.Start;
        roomKinds[^1] = RoomKind.Stairs;

        var middle = Enumerable.Range(1, rooms.Count - 2).OrderBy(_ => rng.Next()).ToList();
        int next = 0;

        // 보스 층 바로 전 층에는 상점이 꼭 나온다.
        bool wantShop = !IsBossFloor && depth >= 2 && (depth % GameData.BossFloorInterval == GameData.BossFloorInterval - 1 || rng.NextDouble() < 0.3);
        if (wantShop && next < middle.Count) roomKinds[middle[next++]] = RoomKind.Shop;
        if (rng.NextDouble() < 0.3 && next < middle.Count) roomKinds[middle[next++]] = RoomKind.Treasure;
        if (rng.NextDouble() < 0.35 && next < middle.Count) roomKinds[middle[next++]] = RoomKind.Shrine;
    }

    void PopulateRooms()
    {
        for (int i = 1; i < rooms.Count; i++)
        {
            var room = rooms[i];
            switch (roomKinds[i])
            {
                case RoomKind.Shop:
                    SetupShop(room);
                    continue; // 상점에는 몬스터가 없다.

                case RoomKind.Treasure:
                    for (int n = 0; n < 3; n++)
                        if (TryRandomFreeCell(room, out var itemCell)) SpawnItem(GameData.RollItem(rng), itemCell);
                    for (int n = 0; n < 2; n++)
                        if (TryRandomFreeCell(room, out var goldCell)) SpawnGold(goldCell, rng.Next(8, 16) + depth * 2);
                    SpawnEnemies(room, 2 + depth / 3); // 보물을 지키는 몬스터
                    continue;

                case RoomKind.Shrine:
                    altarPos = DungeonGenerator.Center(room);
                    map.SetTile(altarPos.Value, TileType.Altar);
                    break;
            }

            int maxEnemies = Mathf.Min(1 + depth / 3, 4);
            SpawnEnemies(room, rng.Next(0, maxEnemies + 1));
            if (rng.NextDouble() < 0.35 && TryRandomFreeCell(room, out var cell)) SpawnItem(GameData.RollItem(rng), cell);
            if (rng.NextDouble() < 0.25 && TryRandomFreeCell(room, out var coinCell)) SpawnGold(coinCell, rng.Next(2, 6) + depth);
        }
    }

    void SpawnEnemies(RectInt room, int count)
    {
        for (int n = 0; n < count; n++)
            if (TryRandomFreeCell(room, out var cell)) SpawnEnemy(cell);
    }

    void SpawnGold(Vector2Int pos, int amount) => items.Add(Item.Create(ItemKind.Gold, pos, floorRoot, amount));

    // 특수 방이 처음 시야에 들어오면 알려준다.
    void CheckRoomDiscovery()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (discoveredRooms.Contains(i) || !map.IsVisible(DungeonGenerator.Center(rooms[i]))) continue;
            discoveredRooms.Add(i);

            string message = roomKinds[i] switch
            {
                RoomKind.Shop => "<color=#ffd966>상인을 발견했다! 부딪히면 거래할 수 있다.</color>",
                RoomKind.Treasure => "<color=#ffd966>보물 창고다! 하지만 지키는 몬스터가 많다...</color>",
                RoomKind.Shrine => "<color=#99ddff>신비한 제단이 빛나고 있다. 올라서면 무슨 일이 일어날까?</color>",
                _ => null,
            };
            if (message == null) continue;
            specialRoomsDiscovered++;
            Log(message);
            input.CancelRepeat();
        }
    }

    // ───────────────────────── 제단 ─────────────────────────

    void UseAltar()
    {
        map.SetTile(player.Pos, TileType.Floor);
        altarPos = null;
        Play(Sfx.Magic);

        double roll = rng.NextDouble();
        if (roll < 0.15)
        {
            Log("<color=#ff7777>제단이 분노했다! 어둠 속에서 몬스터가 솟아난다!</color>");
            Shake(0.3f, 0.2f);
            int spawned = 0;
            foreach (var dir in Pathfinder.Directions8)
            {
                var cell = player.Pos + dir;
                if (spawned >= 2 || map.Get(cell) != TileType.Floor || IsOccupied(cell)) continue;
                var enemy = SpawnEnemy(cell);
                enemy.LastKnownTarget = player.Pos;
                spawned++;
            }
        }
        else if (roll < 0.4)
        {
            player.Hp = player.MaxHp;
            AddFloatingText(player.transform.position, "체력 회복", new Color(0.4f, 1f, 0.4f));
            Log("따뜻한 빛이 온몸을 감싼다. 체력이 모두 회복됐다!");
        }
        else if (roll < 0.6)
        {
            AddBaseStats(maxHp: 8);
            AddFloatingText(player.transform.position, "최대 체력 +8", new Color(0.4f, 1f, 0.4f));
            Log("생명력이 강해졌다! 최대 체력 +8");
        }
        else if (roll < 0.8)
        {
            AddBaseStats(attack: 2);
            AddFloatingText(player.transform.position, "공격 +2", new Color(1f, 0.8f, 0.4f));
            Log("힘이 솟구친다! 공격력 +2");
        }
        else
        {
            AddBaseStats(defense: 1);
            AddFloatingText(player.transform.position, "방어 +1", new Color(0.5f, 0.7f, 1f));
            Log("피부가 바위처럼 단단해졌다! 방어력 +1");
        }
    }

    // ───────────────────────── 함정 ─────────────────────────

    void PlaceTraps()
    {
        int count = Mathf.Min(1 + depth / 2, 6);
        for (int i = 0; i < count; i++)
        {
            int roomIndex = rng.Next(1, rooms.Count);
            if (roomKinds[roomIndex] == RoomKind.Shop) continue;
            if (!TryRandomFreeCell(rooms[roomIndex], out var cell)) continue;

            double roll = rng.NextDouble();
            var kind = roll < 0.5 ? TrapKind.Spike : roll < 0.75 ? TrapKind.Alarm : TrapKind.Teleport;

            var go = new GameObject($"Trap {kind}");
            go.transform.SetParent(floorRoot, false);
            go.transform.localPosition = new Vector3(cell.x, cell.y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Trap;
            sr.color = GameData.TrapTint(kind);
            sr.sortingOrder = 1;
            sr.enabled = false;
            traps[cell] = new Trap { Kind = kind, Renderer = sr };
        }
    }

    bool IsRevealedTrap(Vector2Int pos) => traps.TryGetValue(pos, out var trap) && trap.Revealed;

    void TriggerTrapAt(Vector2Int pos)
    {
        if (!traps.TryGetValue(pos, out var trap)) return;
        trap.Revealed = true;
        string name = GameData.TrapName(trap.Kind);

        switch (trap.Kind)
        {
            case TrapKind.Spike:
                Log($"<color=#ff7777>{name}{Josa(name, "을", "를")} 밟았다!</color>");
                DealDamage(player, 3 + depth, name);
                break;

            case TrapKind.Alarm:
                Log("<color=#ffcc44>경보 함정! 요란한 종소리가 던전에 울려 퍼진다!</color>");
                foreach (var enemy in enemies) enemy.LastKnownTarget = player.Pos;
                Play(Sfx.Shoot);
                Shake(0.25f, 0.15f);
                break;

            case TrapKind.Teleport:
                Log("<color=#bb99ff>발밑의 마법진이 빛난다! 순간이동 함정이다!</color>");
                Teleport();
                break;
        }
    }

    // 함정 바로 옆에 서 있으면 일정 확률로 함정을 알아챈다.
    void SpotNearbyTraps()
    {
        foreach (var (pos, trap) in traps)
        {
            float chance = playerClass.Id == PlayerClass.Rogue ? TrapSpotChance * 2 : TrapSpotChance;
            if (trap.Revealed || Pathfinder.Distance(pos, player.Pos) != 1 || rng.NextDouble() >= chance) continue;
            trap.Revealed = true;
            string name = GameData.TrapName(trap.Kind);
            Log($"{name}{Josa(name, "을", "를")} 발견했다!");
            input.CancelRepeat();
        }
    }

    void RevealAllTraps()
    {
        foreach (var trap in traps.Values) trap.Revealed = true;
    }

    void RefreshTraps()
    {
        foreach (var (pos, trap) in traps)
            trap.Renderer.enabled = trap.Revealed && map.IsExplored(pos);
    }
}
