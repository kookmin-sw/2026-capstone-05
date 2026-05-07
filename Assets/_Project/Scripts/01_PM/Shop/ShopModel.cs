using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.Shop
{
    public class ShopModel
    {
        // For demonstration, player's gold is kept here. In a real project, it would be in PlayerStats/Inventory.
        public int PlayerGold { get; private set; }

        public List<ShopItemEntry> ShopItems { get; private set; }

        public Action<int> OnGoldChanged;

        public ShopModel(int initialGold, List<ShopItemEntry> initialItems)
        {
            PlayerGold = initialGold;
            ShopItems = initialItems;
        }

        public void SetShopItems(List<ShopItemEntry> items)
        {
            ShopItems = items;
        }

        public void AddGold(int amount)
        {
            PlayerGold += amount;
            OnGoldChanged?.Invoke(PlayerGold);
        }

        public bool TrySpendGold(int amount)
        {
            if (PlayerGold >= amount)
            {
                PlayerGold -= amount;
                OnGoldChanged?.Invoke(PlayerGold);
                return true;
            }
            return false;
        }
    }

    [Serializable]
    public class ShopItemEntry
    {
        public ItemData ItemData;
        public int BuyPrice;
        public int StockCount = 1;
        // Depending on design, you could also define sell price or use ItemData.price
    }
}
