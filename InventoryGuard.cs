using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;

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
            RestrictedItems = new List<RestrictedItem> 
            { 
                new RestrictedItem { ItemId = 17, MaxAmount = 2 } 
            };
        }
    }

    public class InventoryGuard : RocketPlugin<InventoryGuardConfiguration>
    {
        protected override void Load()
        {
            ItemManager.onTakeItemRequested += OnTakeItemRequested;
            Rocket.Core.Logging.Logger.Log("InventoryGuard (LDM) успешно запущен!");
        }

        protected override void Unload()
        {
            ItemManager.onTakeItemRequested -= OnTakeItemRequested;
        }

        // Исправленная сигнатура: убраны лишние аргументы, мешавшие компиляции
        private void OnTakeItemRequested(Player player, byte x, byte y, uint instanceID, ref bool shouldAllow)
        {
            UnturnedPlayer uPlayer = UnturnedPlayer.FromPlayer(player);
            if (uPlayer == null || uPlayer.IsAdmin) return;

            // Исправлен поиск предмета: в регионах хранятся ItemData, а не InteractableItem
            var region = ItemManager.regions[x, y];
            ItemData itemData = region.items.Find(i => i.instanceID == instanceID);
            
            if (itemData == null || itemData.item == null) return;

            ushort itemID = itemData.item.id;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (itemID != restriction.ItemId) continue;

                if (R.Permissions.HasPermission(uPlayer, "inventoryguard.ignore.*") || 
                    R.Permissions.HasPermission(uPlayer, $"inventoryguard.ignore.{itemID}")) return;

                int effectiveLimit = restriction.MaxAmount;

                int count = 0;
                for (byte p = 0; p < PlayerInventory.PAGES - 2; p++)
                {
                    var itemsPage = player.inventory.items[p];
                    if (itemsPage == null) continue;
                    foreach (var jar in itemsPage.items)
                    {
                        if (jar.item != null && jar.item.id == itemID) count++;
                    }
                }

                if (count >= effectiveLimit)
                {
                    shouldAllow = false; 
                    UnturnedChat.Say(uPlayer, $"[Лимит] Вы не можете взять предмет {itemID}. Лимит: {effectiveLimit}", Color.yellow);
                    return;
                }
            }
        }
    }
}
