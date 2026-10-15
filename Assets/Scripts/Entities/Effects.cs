using UnityEngine;

// 잠깐 나타났다 사라지는 연출용 오브젝트. 움직이고, 돌고, 커지거나 작아지며 서서히 투명해진다.
public class Fx : MonoBehaviour
{
    public float Life = 0.2f;
    public float Spin;
    public float StartScale = 1f;
    public float EndScale = 1f;
    public float Drag;
    public float Gravity;
    public Vector3 Velocity;

    SpriteRenderer sr;
    Color color;
    float age;

    public static Fx Spawn(Sprite sprite, Vector3 position, Color color, float life, int sortingOrder = 6)
    {
        var go = new GameObject("Fx");
        go.transform.position = position;
        var fx = go.AddComponent<Fx>();
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = sprite;
        fx.sr.sortingOrder = sortingOrder;
        fx.sr.color = color;
        fx.color = color;
        fx.Life = life;
        return fx;
    }

    public void SetFlip(bool flipX) => sr.flipX = flipX;

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= Life)
        {
            Destroy(gameObject);
            return;
        }

        float k = age / Life;
        Velocity.y -= Gravity * dt;
        Velocity *= Mathf.Max(0f, 1f - Drag * dt);
        transform.position += Velocity * dt;
        transform.Rotate(0f, 0f, Spin * dt);
        float s = Mathf.Lerp(StartScale, EndScale, k);
        transform.localScale = new Vector3(s, s, 1f);
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
    }
}

// 전투 연출 모음.
public static class Effects
{
    // 근접 공격의 베기 궤적. 공격자 → 대상 방향으로 반달 모양이 휙 지나간다.
    public static void Slash(Vector2Int from, Vector2Int to, Color color, float size = 1f)
    {
        var dir = new Vector2(to.x - from.x, to.y - from.y).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        var fx = Fx.Spawn(SpriteFactory.Slash, new Vector3(to.x - dir.x * 0.2f, to.y - dir.y * 0.2f, 0f), color, 0.18f);
        fx.transform.rotation = Quaternion.Euler(0f, 0f, angle + 40f);
        fx.Spin = -500f;
        fx.StartScale = 0.8f * size;
        fx.EndScale = 1.3f * size;
    }

    // 전사의 회전베기: 큰 반달 두 개가 몸 주위를 한 바퀴 돈다.
    public static void Whirl(Vector3 center, Color color)
    {
        for (int i = 0; i < 2; i++)
        {
            var fx = Fx.Spawn(SpriteFactory.Slash, center, color, 0.3f);
            fx.transform.rotation = Quaternion.Euler(0f, 0f, i * 180f);
            fx.Spin = -1200f;
            fx.StartScale = 2.2f;
            fx.EndScale = 3f;
        }
    }

    // 쓰러진 캐릭터: 그림이 위로 떠오르며 작아지고, 픽셀 조각이 흩어진다.
    public static void Death(Actor actor, Color particleColor)
    {
        float scale = actor.transform.lossyScale.x;
        var ghost = Fx.Spawn(actor.Sprite, actor.transform.position, new Color(1f, 1f, 1f, 0.9f), 0.35f, 3);
        ghost.SetFlip(actor.FacingLeft);
        ghost.Velocity = new Vector3(0f, 1.6f, 0f);
        ghost.StartScale = scale;
        ghost.EndScale = scale * 0.3f;
        Burst(actor.transform.position, particleColor, 10);
    }

    public static void Burst(Vector3 position, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Random.insideUnitCircle.normalized;
            var fx = Fx.Spawn(SpriteFactory.Pixel, position, color, Random.Range(0.3f, 0.5f));
            fx.Velocity = new Vector3(dir.x, dir.y + 0.6f, 0f) * Random.Range(3f, 5.5f);
            fx.Gravity = 14f;
            fx.StartScale = fx.EndScale = Random.Range(0.12f, 0.2f);
        }
    }
}
