using System.Collections.Generic;
using UnityEngine;

// 상인과 상점. 상인에게 부딪히면 상점 창이 열리고, 숫자 키로 산다.
public partial class GameManager
{
    class ShopOffer
    {
        public ItemKind Kind;
        public Equipment Equipment;
        public int Price;
        public int Stock;

        public string Name => Equipment?.ColoredName ?? GameData.ItemName(Kind);
    }

    readonly List<ShopOffer> shopOffers = new();
    Actor merchant;
    bool shopOpen;
    int gold;

    void SetupShop(RectInt room)
    {
        merchant = Actor.Create("상인", SpriteFactory.Merchant, new Color(1f, 0.92f, 0.75f), 2, floorRoot);
        merchant.IsMerchant = true;
        merchant.SetPos(DungeonGenerator.Center(room), snap: true);
        merchant.SetVisible(false);

        // 물약 + 두루마리 1종 + 장비 2개(하나는 희귀 이상).
        shopOffers.Clear();
        AddOffer(ItemKind.HealthPotion, 3);
        var scrolls = new[] { ItemKind.FireScroll, ItemKind.TeleportScroll, ItemKind.MapScroll };
        AddOffer(scrolls[rng.Next(scrolls.Length)], 2);
        AddEquipmentOffer(EquipmentFactory.Generate(rng, depth + 1, Rarity.Rare));
        AddEquipmentOffer(EquipmentFactory.Generate(rng, depth + 1));
    }

    void AddOffer(ItemKind kind, int stock) =>
        shopOffers.Add(new ShopOffer { Kind = kind, Price = GameData.Price(kind, depth), Stock = stock });

    void AddEquipmentOffer(Equipment eq) =>
        shopOffers.Add(new ShopOffer { Kind = ItemKind.Equipment, Equipment = eq, Price = GameData.Price(eq, depth), Stock = 1 });

    bool IsMerchantAt(Vector2Int p) => merchant != null && merchant.Pos == p;

    void OpenShop()
    {
        shopOpen = true;
        input.CancelRepeat();
        Log("상인: \"어서 오게! 골드만 있다면 뭐든 팔지.\"");
    }

    void CloseShop()
    {
        shopOpen = false;
        Log("상인: \"또 오게나.\"");
    }

    void HandleShopInput(PlayerCommand cmd)
    {
        switch (cmd.Kind)
        {
            case CommandKind.UseItem: Buy(cmd.Slot); break;
            case CommandKind.Cancel:
            case CommandKind.Move:
            case CommandKind.Wait: CloseShop(); break;
        }
    }

    bool Buy(int index)
    {
        if (index >= shopOffers.Count) return false;
        var offer = shopOffers[index];
        if (offer.Stock <= 0) { Log("이미 다 팔린 물건이다."); return false; }
        if (gold < offer.Price) { Log($"골드가 부족하다. ({offer.Name} {offer.Price}G)"); return false; }

        gold -= offer.Price;
        offer.Stock--;
        if (offer.Equipment != null)
        {
            ReceiveEquipment(offer.Equipment, "사서", forceEquip: true);
            Play(Sfx.Pickup);
        }
        else
        {
            GainItem(offer.Kind, 1, "샀다");
        }
        return true;
    }

    void DrawShop(float w, float h)
    {
        const float pw = 620, ph = 330;
        float x = (w - pw) / 2, y = (h - ph) / 2 - 40;
        DrawRect(new Rect(x, y, pw, ph), new Color(0.08f, 0.06f, 0.04f, 0.92f));
        DrawRect(new Rect(x, y, pw, 3), new Color(1f, 0.8f, 0.3f));

        GUI.Label(new Rect(x + 20, y + 14, pw - 40, 30), "<b><color=#ffd966>상인의 가게</color></b>", textStyle);
        GUI.Label(new Rect(x + pw - 160, y + 14, 140, 30), $"<color=#ffd966>보유 {gold}G</color>",
            new GUIStyle(textStyle) { alignment = TextAnchor.UpperRight });

        for (int i = 0; i < shopOffers.Count; i++)
        {
            var offer = shopOffers[i];
            bool soldOut = offer.Stock <= 0;
            bool affordable = gold >= offer.Price;
            string name = soldOut ? $"<color=#666666>{GameData.ItemName(offer.Kind)}</color>" : offer.Name;
            string priceColor = soldOut ? "#666666" : affordable ? "#ffd966" : "#ff6666";
            string stock = soldOut ? "품절" : $"재고 {offer.Stock}";
            float rowY = y + 58 + i * 56;
            GUI.Label(new Rect(x + 24, rowY, 400, 30), $"[{i + 1}]  {name}", textStyle);
            GUI.Label(new Rect(x + pw - 190, rowY, 180, 30),
                $"<color={priceColor}>{offer.Price}G</color>   <color=#999999>{stock}</color>", textStyle);

            // 장비는 현재 착용 장비와 비교해서 보여준다.
            string detail = offer.Equipment != null && !soldOut
                ? $"{offer.Equipment.Describe()}   <color=#888888>(착용 중: {GetEquipped(offer.Equipment.Slot)?.Describe() ?? "없음"})</color>"
                : "";
            GUI.Label(new Rect(x + 56, rowY + 24, pw - 80, 26), $"<color=#aaaaaa>{detail}</color>",
                new GUIStyle(textStyle) { fontSize = 14 });
        }

        GUI.Label(new Rect(x + 20, y + ph - 36, pw - 40, 30),
            "<color=#aaaaaa>숫자 키로 구매 · Esc 또는 이동 키로 닫기</color>", new GUIStyle(textStyle) { fontSize = 16 });
    }
}
