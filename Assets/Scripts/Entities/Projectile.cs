using UnityEngine;

// 화살, 화염구 같은 투사체 연출. 피해는 턴 처리에서 즉시 계산되고, 이건 보여주기만 한다.
public class Projectile : MonoBehaviour
{
    const float Speed = 30f;

    Vector3 target;

    public static void Spawn(Vector2Int from, Vector2Int to, Color color, float scale = 1f)
    {
        var go = new GameObject("Projectile");
        go.transform.position = new Vector3(from.x, from.y, 0);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Bolt;
        sr.color = color;
        sr.sortingOrder = 5;
        go.AddComponent<Projectile>().target = new Vector3(to.x, to.y, 0);
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, Speed * Time.deltaTime);
        if (transform.position == target) Destroy(gameObject);
    }
}
