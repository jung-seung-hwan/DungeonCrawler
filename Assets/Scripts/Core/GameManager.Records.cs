using System;
using System.Collections.Generic;
using UnityEngine;

// 기록 화면: 직업별 최고 도달 층, 상위 10판, 누적 통계. 직업 선택 화면에서 T 키로 연다.
// 서버·계정 없이 PlayerPrefs의 JSON 문자열 하나("Records")에 저장한다.
// 직업과 사인은 언어와 무관한 키로 저장하고, 그릴 때 현재 언어로 바꾼다.
// 전체 최고 도달 층은 예전부터 쓰던 "BestDepth" 값을 그대로 쓴다(이 파일이 고치지 않는다).
// 자동 플레이(봇·시뮬레이션)는 기록을 남기지 않는다.
[Serializable]
public class RunRecord
{
    public int depth;
    public string cls;      // PlayerClass 이름 ("Warrior" 등)
    public int level;
    public int kills;
    public string killer;   // "enemy:<몬스터 한국어 키>" 또는 "trap:<TrapKind>"
    public long time;       // 끝난 시각 (UTC, 유닉스 초)
}

[Serializable]
public class ClassBest
{
    public string cls;
    public int depth;
}

[Serializable]
public class RecordData
{
    public int runs;
    public int totalKills;
    public int bossKills;
    public List<ClassBest> classBest = new();
    public List<RunRecord> top = new();

    public int BestOf(PlayerClass id)
    {
        var entry = classBest.Find(b => b.cls == id.ToString());
        return entry != null ? entry.depth : 0;
    }

    // 한 판을 더한다. 상위 목록은 층 → 레벨 → 처치 수 순, 같으면 최근 판이 위.
    public void Add(RunRecord run, int runBossKills, int keep)
    {
        runs++;
        totalKills += run.kills;
        bossKills += runBossKills;

        var best = classBest.Find(b => b.cls == run.cls);
        if (best == null) classBest.Add(new ClassBest { cls = run.cls, depth = run.depth });
        else if (run.depth > best.depth) best.depth = run.depth;

        int index = top.FindIndex(r => Compare(run, r) <= 0);
        top.Insert(index < 0 ? top.Count : index, run);
        if (top.Count > keep) top.RemoveRange(keep, top.Count - keep);
    }

    static int Compare(RunRecord a, RunRecord b)
    {
        if (a.depth != b.depth) return b.depth.CompareTo(a.depth);
        if (a.level != b.level) return b.level.CompareTo(a.level);
        return b.kills.CompareTo(a.kills);
    }
}

public partial class GameManager
{
    const string RecordsKey = "Records";
    const int TopRunCount = 10;

    bool recordsOpen;
    string killerKey;           // 이번 런의 사인 키 (DealDamage가 채운다)
    RecordData recordsCache;
    RecordData recordsPreview;  // 스크린샷 검증용 예시 자료. 저장하지 않는다.

    RecordData Records => recordsPreview ?? (recordsCache ??= LoadRecords());

    static RecordData LoadRecords()
    {
        string json = PlayerPrefs.GetString(RecordsKey, "");
        if (string.IsNullOrEmpty(json)) return new RecordData();
        try
        {
            var data = JsonUtility.FromJson<RecordData>(json) ?? new RecordData();
            data.classBest ??= new List<ClassBest>();
            data.top ??= new List<RunRecord>();
            return data;
        }
        catch (Exception)
        {
            return new RecordData(); // 저장값이 깨졌으면 빈 기록으로 시작한다.
        }
    }

    // 런이 끝났을 때 한 번 부른다.
    void SaveRunRecord()
    {
        if (autopilotActive) return;
        var data = recordsCache ??= LoadRecords();
        data.Add(new RunRecord
        {
            depth = depth,
            cls = playerClass.Id.ToString(),
            level = level,
            kills = kills,
            killer = killerKey ?? "",
            time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        }, bossKills, TopRunCount);
        PlayerPrefs.SetString(RecordsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // 저장된 사인 키를 현재 언어의 이름으로 바꾼다.
    static string KillerName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "?";
        int colon = key.IndexOf(':');
        if (colon < 0) return key;
        string kind = key.Substring(0, colon), id = key.Substring(colon + 1);
        if (kind == "enemy")
        {
            var template = GameData.EnemyNamed(id) ?? GameData.BossNamed(id);
            return template != null ? template.DisplayName : id;
        }
        if (kind == "trap" && Enum.TryParse<TrapKind>(id, out var trap)) return Cap(GameData.TrapName(trap));
        return id;
    }

    static string RecordDate(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString("yyyy-MM-dd");

    void OpenRecords()
    {
        recordsOpen = true;
        Play(Sfx.UiOpen);
    }

    void HandleRecordsInput(PlayerCommand cmd)
    {
        if (cmd.Kind != CommandKind.Cancel && cmd.Kind != CommandKind.Records) return;
        recordsOpen = false;
        Play(Sfx.UiClose);
    }

    void DrawRecords(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.8f));
        const float pw = 820, rowH = 27;
        float ph = 232 + TopRunCount * rowH;
        float x = (w - pw) / 2, y = (h - ph) / 2;
        var data = Records;

        DrawRect(new Rect(x, y, pw, ph), new Color(0.08f, 0.08f, 0.12f, 0.98f));
        DrawRect(new Rect(x, y, pw, 4), new Color(1f, 0.85f, 0.4f));
        GUI.Label(new Rect(x + 24, y + 16, 300, 34), Ko ? "<b>기록</b>" : "<b>Records</b>", new GUIStyle(textStyle) { fontSize = 26 });

        var right = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleRight };
        int best = PlayerPrefs.GetInt(BestDepthKey, 0);
        if (recordsPreview != null && recordsPreview.top.Count > 0) best = Mathf.Max(best, recordsPreview.top[0].depth);
        FitLabel(new Rect(x + pw - 344, y + 20, 320, 30),
            $"<color=#ffd966>{(Ko ? "최고 기록" : "Best")}: {(best > 0 ? FloorName(best) : "—")}</color>", right);

        // 누적 통계
        FitLabel(new Rect(x + 24, y + 62, pw - 48, 26), Ko
            ? $"<color=#aaaaaa>플레이</color> {data.runs}판     <color=#aaaaaa>총 처치</color> {data.totalKills}     <color=#aaaaaa>쓰러뜨린 보스</color> {data.bossKills}"
            : $"<color=#aaaaaa>Runs</color> {data.runs}     <color=#aaaaaa>Total kills</color> {data.totalKills}     <color=#aaaaaa>Bosses slain</color> {data.bossKills}", textStyle);

        // 직업별 최고 도달 층
        var classes = GameData.Classes;
        float colW = (pw - 48) / classes.Length;
        for (int i = 0; i < classes.Length; i++)
        {
            var c = classes[i];
            int classBest = data.BestOf(c.Id);
            var cell = new Rect(x + 24 + i * colW, y + 98, colW - 8, 34);
            DrawRect(cell, new Color(1f, 1f, 1f, 0.05f));
            FitLabel(new Rect(cell.x + 10, cell.y + 4, cell.width - 20, 26),
                $"<color=#{ColorUtility.ToHtmlStringRGB(c.Color)}>{c.Name}</color>  {(classBest > 0 ? FloorName(classBest) : "<color=#666666>—</color>")}", textStyle);
        }

        // 상위 10판. 열: 순위, 층, 직업, 레벨, 처치, 사인, 날짜
        float[] colX = { 24, 70, 190, 320, 400, 490, 680 };
        float[] colWidth = { 40, 112, 122, 72, 82, 182, 116 };
        string[] heads = Ko ? new[] { "#", "층", "직업", "레벨", "처치", "사인", "날짜" }
                            : new[] { "#", "Floor", "Class", "Level", "Kills", "Slain by", "Date" };
        var small = new GUIStyle(textStyle) { fontSize = 16 };
        float ty = y + 148;
        for (int c = 0; c < heads.Length; c++)
            FitLabel(new Rect(x + colX[c], ty, colWidth[c], 24), $"<color=#888888>{heads[c]}</color>", small);
        DrawRect(new Rect(x + 24, ty + 26, pw - 48, 1), new Color(1f, 1f, 1f, 0.15f));

        if (data.top.Count == 0)
        {
            GUI.Label(new Rect(x, ty + 30, pw, TopRunCount * rowH),
                Ko ? "<color=#888888>아직 기록이 없습니다. 첫 모험을 떠나 보세요.</color>"
                   : "<color=#888888>No records yet. Set out on your first run.</color>",
                new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter });
        }
        for (int i = 0; i < data.top.Count && i < TopRunCount; i++)
        {
            var run = data.top[i];
            float ry = ty + 32 + i * rowH;
            if (i % 2 == 0) DrawRect(new Rect(x + 12, ry - 1, pw - 24, rowH - 1), new Color(1f, 1f, 1f, 0.03f));

            string className = run.cls;
            string classHex = "ffffff";
            if (Enum.TryParse<PlayerClass>(run.cls, out var classId) && GameData.ClassOf(classId) is { } def)
            {
                className = def.Name;
                classHex = ColorUtility.ToHtmlStringRGB(def.Color);
            }
            string[] cells =
            {
                $"<color=#888888>{i + 1}</color>",
                $"<color=#ffd966>{FloorName(run.depth)}</color>",
                $"<color=#{classHex}>{className}</color>",
                $"Lv.{run.level}",
                run.kills.ToString(),
                KillerName(run.killer),
                $"<color=#aaaaaa>{RecordDate(run.time)}</color>",
            };
            for (int c = 0; c < cells.Length; c++)
                FitLabel(new Rect(x + colX[c], ry, colWidth[c], 24), cells[c], small);
        }

        FitLabel(new Rect(x + 24, y + ph - 36, pw - 48, 24),
            Ko ? "<color=#888888>도달 층이 높은 순서로 10판까지 남습니다.     Esc · T  닫기</color>"
               : "<color=#888888>The 10 deepest runs are kept.     Esc · T  Close</color>", small);
    }

    // 스크린샷 검증용: 가장 긴 이름들로 열 판을 채운 예시 자료. 저장 형식(JSON)을 한 번 거쳐서 만든다.
    static RecordData SampleRecords()
    {
        var data = new RecordData();
        string[] killers = { "enemy:심연의 용", "enemy:고블린 궁수", "trap:Spike", "enemy:암흑 마법사", "enemy:오우거 족장", "enemy:강령술사" };
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int i = 0; i < 14; i++)
        {
            data.Add(new RunRecord
            {
                depth = 3 + (i * 7) % 23,
                cls = ((PlayerClass)(i % 3)).ToString(),
                level = 2 + (i * 5) % 17,
                kills = 12 + (i * 37) % 240,
                killer = killers[i % killers.Length],
                time = now - i * 86400L,
            }, i % 4, TopRunCount);
        }
        var loaded = JsonUtility.FromJson<RecordData>(JsonUtility.ToJson(data));
        Debug.Log($"[RECORDS] sample round trip: runs {loaded.runs}, top {loaded.top.Count}, first depth {loaded.top[0].depth}, " +
                  $"last depth {loaded.top[^1].depth}, kills {loaded.totalKills}, bosses {loaded.bossKills}");
        return loaded;
    }
}
