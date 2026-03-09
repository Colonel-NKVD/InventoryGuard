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
            // Используем максимально надежное событие подбора предмета
            ItemManager.onTakeItemRequested += OnTakeItemRequested;
            Rocket.Core.Logging.Logger.Log("InventoryGuard (LDM) успешно запущен!");
        }

        protected override void Unload()
        {
            ItemManager.onTakeItemRequested -= OnTakeItemRequested;
        }

        private void OnTakeItemRequested(Player player, byte x, byte y, uint instanceID, byte to_x, byte to_y, byte to_rot, byte to_page, ref bool shouldAllow)
        {
            UnturnedPlayer uPlayer = UnturnedPlayer.FromPlayer(player);
            if (uPlayer == null || uPlayer.IsAdmin) return;

            // Проверка предмета, который игрок пытается поднять
            InteractableItem interactableItem = ItemManager.regions[x, y].items.Find(i => i.instanceID == instanceID);
            if (interactableItem == null) return;

            ushort itemID = interactableItem.asset.id;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (itemID != restriction.ItemId) continue;

                // Проверка иммунитета
                if (R.Permissions.HasPermission(uPlayer, "inventoryguard.ignore.*") || 
                    R.Permissions.HasPermission(uPlayer, $"inventoryguard.ignore.{itemID}")) return;

                int effectiveLimit = restriction.MaxAmount;

                // Считаем текущее количество в инвентаре
                int count = 0;
                for (byte p = 0; p < PlayerInventory.PAGES - 2; p++)
                {
                    var itemsPage = player.inventory.items[p];
                    if (itemsPage == null) continue;
                    foreach (var jar in itemsPage.items)
                    {
                        if (jar.item.id == itemID) count++;
                    }
                }

                if (count >= effectiveLimit)
                {
                    shouldAllow = false; // ЗАПРЕЩАЕМ ПОДБОР
                    UnturnedChat.Say(uPlayer, $"[Лимит] Вы не можете взять предмет {itemID}. Лимит: {effectiveLimit}", Color.yellow);
                    return;
                }
            }
        }
    }
}
