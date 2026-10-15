using UnityEngine;

// 바닥에 떨어져 있는 아이템. 플레이어가 밟으면 자동으로 줍는다.
public class Item : MonoBehaviour
{
    public ItemKind Kind;
    public Vector2Int Pos;
    public int Amount = 1;        // 금화 더미의 금액
    public Equipment Equipment;   // Kind가 Equipment일 때의 장비 정보

    SpriteRenderer sr;

    public static Item Create(ItemKind kind, Vector2Int pos, Transform parent, int amount = 1, Equipment equipment = null)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0);
        var item = go.AddComponent<Item>();
        item.Kind = kind;
        item.Pos = pos;
        item.Amount = amount;
        item.Equipment = equipment;
        item.sr = go.AddComponent<SpriteRenderer>();
        item.sr.sortingOrder = 1;
        item.sr.sprite = kind switch
        {
            ItemKind.Equipment => SpriteFor(equipment.Slot),
            ItemKind.HealthPotion => SpriteFactory.Potion,
            ItemKind.Gold => SpriteFactory.Coins,
            _ => SpriteFactory.Scroll,
        };
        item.sr.color = equipment != null ? equipment.RarityColor : GameData.ItemTint(kind);
        item.sr.enabled = false;
        return item;
    }

    public static Sprite SpriteFor(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => SpriteFactory.Sword,
        EquipSlot.Armor => SpriteFactory.Armor,
        _ => SpriteFactory.Ring,
    };

    public void SetVisible(bool visible) => sr.enabled = visible;
}
