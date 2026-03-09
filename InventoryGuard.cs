using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using SDG.Unturned;
using UnityEngine;
using Rocket.Core.Logging;

namespace InventoryGuard
{
    public class RestrictedItem
    {
        public ushort ItemId;
        public int MaxAmount;
    }

    public class InventoryGuardConfiguration : IRocketPluginConfiguration
    {
        public List<RestrictedItem> RestrictedItems;
        public void LoadDefaults()
        {
            RestrictedItems = new List<RestrictedItem> { new RestrictedItem { ItemId = 17, MaxAmount = 2 } };
        }
    }

    public class InventoryGuard : RocketPlugin<InventoryGuardConfiguration>
    {
        protected override void Load()
        {
            UnturnedPlayerEvents.OnPlayerInventoryUpdated += OnInventoryUpdated;
            Rocket.Core.Logging.Logger.Log("InventoryGuard (LDM) успешно запущен!");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerInventoryUpdated -= OnInventoryUpdated;
        }

        // Заменили InventoryGroup на byte для максимальной совместимости
        private void OnInventoryUpdated(UnturnedPlayer player, byte group, byte index, ItemJar jar)
        {
            if (player == null || player.IsAdmin || jar == null || jar.item == null) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                if (R.Permissions.HasPermission(player, "inventoryguard.ignore.*") || 
                    R.Permissions.HasPermission(player, $"inventoryguard.ignore.{restriction.ItemId}")) continue;

                int effectiveLimit = restriction.MaxAmount;
                int count = 0;

                for (byte p = 0; p < PlayerInventory.PAGES - 2; p++)
                {
                    var itemsPage = player.Inventory.items[p];
                    if (itemsPage == null || itemsPage.items == null) continue;
                    
                    for (int i = 0; i < itemsPage.items.Count; i++)
                    {
                        var itemJar = itemsPage.items[i];
                        if (itemJar != null && itemJar.item != null && itemJar.item.id == restriction.ItemId)
                        {
                            count++;
                        }
                    }
                }

                if (count > effectiveLimit)
                {
                    // Используем базовый метод сброса предмета
                    player.Inventory.askDropItem(player.CSteamID, group, jar.x, jar.y);
                    UnturnedChat.Say(player, $"[Лимит] Предмет {restriction.ItemId} превышен! Максимум: {effectiveLimit}", Color.yellow);
                }
            }
        }
    }
}
