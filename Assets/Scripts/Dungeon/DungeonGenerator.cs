using System.Collections.Generic;
using UnityEngine;

public enum TileType { Wall, Floor, Stairs, Altar }

// 방(Room) + 복도(Corridor) 방식의 랜덤 던전 생성기.
public static class DungeonGenerator
{
    public class Result
    {
        public TileType[,] Tiles;
        public readonly List<RectInt> Rooms = new();
        public Vector2Int Start;
        public Vector2Int Stairs;
    }

    public static Result Generate(int width, int height, System.Random rng,
        int maxRooms = 14, int minSize = 4, int maxSize = 10)
    {
        while (true)
        {
            var result = TryGenerate(width, height, rng, maxRooms, minSize, maxSize);
            if (result.Rooms.Count >= 3) return result;
        }
    }

    static Result TryGenerate(int width, int height, System.Random rng, int maxRooms, int minSize, int maxSize)
    {
        var result = new Result { Tiles = new TileType[width, height] }; // 기본값 Wall

        for (int attempt = 0; attempt < maxRooms * 5 && result.Rooms.Count < maxRooms; attempt++)
        {
            int w = rng.Next(minSize, maxSize + 1);
            int h = rng.Next(minSize, Mathf.Max(minSize, maxSize - 2) + 1);
            int x = rng.Next(1, width - w - 1);
            int y = rng.Next(1, height - h - 1);
            var room = new RectInt(x, y, w, h);

            bool overlaps = false;
            foreach (var other in result.Rooms)
            {
                var padded = new RectInt(other.x - 1, other.y - 1, other.width + 2, other.height + 2);
                if (padded.Overlaps(room)) { overlaps = true; break; }
            }
            if (overlaps) continue;

            CarveRoom(result.Tiles, room);
            if (result.Rooms.Count > 0)
                CarveCorridor(result.Tiles, Center(result.Rooms[^1]), Center(room), rng);
            result.Rooms.Add(room);
        }

        if (result.Rooms.Count > 0)
        {
            result.Start = Center(result.Rooms[0]);
            result.Stairs = Center(result.Rooms[^1]);
            result.Tiles[result.Stairs.x, result.Stairs.y] = TileType.Stairs;
        }
        return result;
    }

    public static Vector2Int Center(RectInt room) =>
        new(room.x + room.width / 2, room.y + room.height / 2);

    static void CarveRoom(TileType[,] tiles, RectInt room)
    {
        for (int x = room.xMin; x < room.xMax; x++)
            for (int y = room.yMin; y < room.yMax; y++)
                tiles[x, y] = TileType.Floor;
    }

    // ㄱ자 복도: 가로 먼저 또는 세로 먼저를 랜덤으로 선택.
    static void CarveCorridor(TileType[,] tiles, Vector2Int a, Vector2Int b, System.Random rng)
    {
        var corner = rng.Next(2) == 0 ? new Vector2Int(b.x, a.y) : new Vector2Int(a.x, b.y);
        CarveLine(tiles, a, corner);
        CarveLine(tiles, corner, b);
    }

    static void CarveLine(TileType[,] tiles, Vector2Int from, Vector2Int to)
    {
        var p = from;
        var step = new Vector2Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));
        while (true)
        {
            if (tiles[p.x, p.y] == TileType.Wall) tiles[p.x, p.y] = TileType.Floor;
            if (p == to) break;
            p += step;
        }
    }
}
