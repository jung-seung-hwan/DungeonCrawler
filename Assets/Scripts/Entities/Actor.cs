using System.Collections;
using UnityEngine;

// 플레이어와 몬스터가 공통으로 쓰는 캐릭터. 격자 좌표(Pos)가 실제 위치이고, 화면 위치는 부드럽게 따라간다.
// 그림은 자식 오브젝트(visual)에 있어서, 공격·피격·이동 모션은 실제 위치를 건드리지 않고 그림만 움직인다.
public class Actor : MonoBehaviour
{
    const float MoveSpeed = 18f;

    // 공격 모션: 뒤로 살짝 당겼다(WindUp) → 앞으로 찌르고(Strike) → 제자리로(Recover)
    const float WindUpTime = 0.05f;
    const float StrikeTime = 0.06f;
    const float RecoverTime = 0.11f;
    const float WindUpDistance = 0.12f;
    const float HitDuration = 0.15f;
    const float HitDistance = 0.16f;
    const float HopDuration = 0.12f;
    const float HopHeight = 0.12f;

    public string DisplayName;
    public Vector2Int Pos;
    public int MaxHp;
    public int Hp;
    public int Attack;
    public int Defense;
    public int XpReward;
    public int Speed = 1;
    public int Range;
    public int Regen;
    public string RangedVerb;
    public bool IsPlayer;
    public bool IsBoss;
    public bool IsMerchant;
    public Vector2Int? LastKnownTarget;

    // 몬스터 행동 특성 (EnemyTemplate에서 복사)
    public EnemyTemplate Template;
    public float FleeBelow;
    public bool Pack, Kites, Erratic, Splits, Enrages, Summons;

    // 몬스터 상태
    public bool Enraged;
    public bool FleeAnnounced;
    public int SummonCooldown = 2;
    public Actor Master;             // 소환된 몬스터면 소환한 주인

    // 보스 패턴 상태
    public bool SlamCharging;
    public int SlamChargedTurn;      // 예고한 턴. 같은 턴 안에서는(광폭화 2회 행동 등) 내려찍지 않는다.
    public int SlamCooldown = 2;
    public int BlinkCooldown = 3;
    public int SummonPhase;
    public bool Frenzied;

    public bool IsDead => Hp <= 0;
    public bool IsShown => sr.enabled;
    public Sprite Sprite => sr.sprite;
    public bool FacingLeft => sr.flipX;
    public Color TintColor => baseColor;

    SpriteRenderer sr;
    Transform visual;
    Color baseColor;
    Vector3 targetWorld;
    float bobPhase;

    float attackT = -1f;
    Vector3 attackDir;
    float attackReach;
    float hitT = -1f;
    Vector3 hitDir;
    float hopT = -1f;

    public static Actor Create(string displayName, Sprite sprite, Color color, int sortingOrder, Transform parent)
    {
        var go = new GameObject(displayName);
        go.transform.SetParent(parent, false);
        var actor = go.AddComponent<Actor>();
        actor.DisplayName = displayName;
        actor.visual = new GameObject("Sprite").transform;
        actor.visual.SetParent(go.transform, false);
        actor.sr = actor.visual.gameObject.AddComponent<SpriteRenderer>();
        actor.sr.sprite = sprite;
        actor.sr.sortingOrder = sortingOrder;
        actor.sr.color = color;
        actor.baseColor = color;
        actor.bobPhase = Random.value * 10f;
        return actor;
    }

    public static Actor FromTemplate(EnemyTemplate t, int depth, Transform parent)
    {
        // 픽셀아트가 있으면 그 그림을 그대로, 없으면 기본 동그라미에 몬스터 색을 입힌다.
        var art = PixelArt.Get(t.Name);
        var actor = Create(t.DisplayName, art != null ? art : SpriteFactory.Creature, art != null ? Color.white : t.Color, 2, parent);
        // 출현 층보다 깊을수록 강해진다. 깊은 층일수록 성장 폭도 커진다.
        int bonus = depth - t.MinDepth;
        actor.MaxHp = actor.Hp = t.Hp + bonus * 4 + bonus * bonus / 5;
        actor.Attack = t.Attack + bonus * 3 / 4;
        actor.Defense = t.Defense + bonus / 3;
        actor.XpReward = t.Xp + bonus;
        actor.Speed = t.Speed;
        actor.Range = t.Range;
        actor.Regen = t.Regen;
        actor.RangedVerb = t.RangedVerb;
        actor.Template = t;
        actor.FleeBelow = t.FleeBelow;
        actor.Pack = t.Pack;
        actor.Kites = t.Kites;
        actor.Erratic = t.Erratic;
        actor.Splits = t.Splits;
        actor.Enrages = t.Enrages;
        actor.Summons = t.Summons;
        actor.transform.localScale = Vector3.one * t.Scale;
        return actor;
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        sr.color = color;
    }

    // 도적이 숨었을 때처럼 반투명하게 보이게 한다.
    public void SetAlpha(float alpha)
    {
        baseColor.a = alpha;
        sr.color = baseColor;
    }

    public void SetSprite(Sprite sprite) => sr.sprite = sprite;

    public void SetPos(Vector2Int p, bool snap = false)
    {
        if (!snap)
        {
            Face(p - Pos);
            hopT = 0f; // 한 칸 이동할 때 깡총 뛴다.
        }
        Pos = p;
        targetWorld = new Vector3(p.x, p.y, 0);
        if (snap) transform.position = targetWorld;
    }

    public void SetVisible(bool visible) => sr.enabled = visible;

    // 그림은 오른쪽을 보고 있으므로, 왼쪽으로 갈 때만 뒤집는다. 위아래 이동은 방향을 유지한다.
    public void Face(Vector2Int dir)
    {
        if (dir.x != 0) sr.flipX = dir.x < 0;
    }

    // 공격 모션. reach는 앞으로 튀어나가는 거리(원거리 공격은 짧게).
    public void PlayAttack(Vector2Int target, float reach = 0.45f)
    {
        var dir = new Vector3(target.x - Pos.x, target.y - Pos.y, 0);
        if (dir == Vector3.zero) return;
        Face(target - Pos);
        attackDir = dir.normalized;
        attackReach = reach;
        attackT = 0f;
    }

    // 피격 모션: 맞은 방향 반대로 살짝 밀려나며 붉게 번쩍인다. from이 없으면 무작위 방향.
    public void PlayHit(Vector2Int? from)
    {
        hitDir = from.HasValue && from.Value != Pos
            ? new Vector3(Pos.x - from.Value.x, Pos.y - from.Value.y, 0).normalized
            : (Vector3)Random.insideUnitCircle.normalized;
        hitT = 0f;
        Flash();
    }

    void Flash()
    {
        if (!isActiveAndEnabled) return;
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        sr.color = new Color(1f, 0.25f, 0.25f, baseColor.a);
        yield return new WaitForSeconds(0.12f);
        sr.color = baseColor;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        // 멀리 뒤처지면(빠른 연속 이동 등) 더 빨리 따라잡아 그림이 실제 위치에서 벗어나지 않게 한다.
        float lag = Vector3.Distance(transform.position, targetWorld);
        transform.position = Vector3.MoveTowards(transform.position, targetWorld, Mathf.Max(MoveSpeed, lag * 15f) * dt);

        var offset = Vector3.zero;
        float sx = 1f, sy = 1f;

        if (attackT >= 0f)
        {
            attackT += dt;
            float t = attackT;
            if (t < WindUpTime)
            {
                offset = -attackDir * WindUpDistance * (t / WindUpTime);
                sx = 0.92f; sy = 1.08f;
            }
            else if (t < WindUpTime + StrikeTime)
            {
                float k = (t - WindUpTime) / StrikeTime;
                offset = Vector3.Lerp(-attackDir * WindUpDistance, attackDir * attackReach, k);
                sx = 1.12f; sy = 0.9f;
            }
            else if (t < WindUpTime + StrikeTime + RecoverTime)
            {
                float k = (t - WindUpTime - StrikeTime) / RecoverTime;
                offset = attackDir * attackReach * (1f - k) * (1f - k);
            }
            else attackT = -1f;
        }

        if (hitT >= 0f)
        {
            hitT += dt;
            float k = hitT / HitDuration;
            if (k >= 1f) hitT = -1f;
            else
            {
                offset += hitDir * HitDistance * (1f - k);
                sx *= 1f + 0.1f * (1f - k);
                sy *= 1f - 0.1f * (1f - k);
            }
        }

        if (hopT >= 0f)
        {
            hopT += dt;
            float k = hopT / HopDuration;
            if (k >= 1f) hopT = -1f;
            else offset.y += Mathf.Sin(k * Mathf.PI) * HopHeight;
        }

        // 숨쉬듯 살짝 늘었다 줄었다 한다.
        float breath = Mathf.Sin(Time.time * 3f + bobPhase);
        sy *= 1f + breath * 0.03f;
        sx *= 1f - breath * 0.02f;

        // 그림의 중심이 피벗이라, 세로로 찌그러질 때 발이 바닥에 붙어 있도록 위치를 보정한다.
        offset.y += (sy - 1f) * 0.5f;
        visual.localPosition = offset;
        visual.localScale = new Vector3(sx, sy, 1f);
    }
}
