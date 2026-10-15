using System;
using System.Collections.Generic;
using UnityEngine;

public static class Pathfinder
{
    public static readonly Vector2Int[] Directions8 =
    {
        new(0, 1), new(0, -1), new(-1, 0), new(1, 0),
        new(-1, 1), new(1, 1), new(-1, -1), new(1, -1),
    };

    // BFS로 isGoal을 만족하는 가장 가까운 칸을 찾아 그쪽으로 가는 첫 걸음을 돌려준다.
    // 목표 칸 자체는 막혀 있어도 도달한 것으로 친다(예: 플레이어가 서 있는 칸).
    public static Vector2Int? FirstStep(DungeonMap map, Vector2Int start,
        Func<Vector2Int, bool> isGoal, Func<Vector2Int, bool> isBlocked)
    {
        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var dir in Directions8)
            {
                var next = current + dir;
                if (cameFrom.ContainsKey(next) || !map.IsWalkable(next)) continue;

                bool goal = isGoal(next);
                if (!goal && isBlocked(next)) continue;
                cameFrom[next] = current;
                if (goal) return Backtrack(cameFrom, start, next);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    static Vector2Int Backtrack(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int end)
    {
        var step = end;
        while (cameFrom[step] != start) step = cameFrom[step];
        return step;
    }

    public static int Distance(Vector2Int a, Vector2Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
