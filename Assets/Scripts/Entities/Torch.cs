using UnityEngine;

// 횃불 빛 번짐이 일렁이도록 밝기와 크기를 조금씩 흔든다.
public class Torch : MonoBehaviour
{
    const float BaseAlpha = 0.16f;
    const float BaseScale = 4.5f;

    SpriteRenderer sr;
    float seed;

    public static Torch Create(Vector3 position, Transform parent)
    {
        var go = new GameObject("TorchGlow");
        go.transform.SetParent(parent, false);
        // 바닥 타일과 같은 정렬 순서(0)에서 z를 카메라 쪽으로 당겨 바닥 위·아이템 아래에 그린다.
        go.transform.localPosition = new Vector3(position.x, position.y, -0.1f);
        var torch = go.AddComponent<Torch>();
        torch.sr = go.AddComponent<SpriteRenderer>();
        torch.sr.sprite = SpriteFactory.Glow;
        torch.sr.sortingOrder = 0;
        torch.sr.enabled = false;
        torch.seed = Random.value * 100f;
        return torch;
    }

    public void SetLit(bool lit) => sr.enabled = lit;

    void Update()
    {
        if (!sr.enabled) return;
        float n = Mathf.PerlinNoise(seed, Time.time * 5f);
        sr.color = new Color(1f, 0.65f, 0.3f, BaseAlpha * (0.7f + 0.6f * n));
        float s = BaseScale * (0.95f + 0.1f * n);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
