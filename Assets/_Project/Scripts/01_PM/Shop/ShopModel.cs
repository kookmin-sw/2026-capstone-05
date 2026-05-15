using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.Shop
{
    public class ShopModel
    {
        public List<ShopItemEntry> ShopItems { get; private set; }

        public ShopModel(List<ShopItemEntry> initialItems)
        {
            ShopItems = initialItems;
        }

        public void SetShopItems(List<ShopItemEntry> items)
        {
            ShopItems = items;
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
