using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using Rocket.Unturned.Events;
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

        // Исправлено: теперь LoadDefaults вместо Defaults
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
            UnturnedPlayerEvents.OnInventoryUpdated += OnInventoryUpdated;
            Rocket.Core.Logging.Logger.Log("InventoryGuard (LDM) успешно запущен!");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnInventoryUpdated -= OnInventoryUpdated;
        }

        // Исправлено: заменили InventoryGroup на byte для совместимости
        private void OnInventoryUpdated(UnturnedPlayer player, byte group, byte index, ItemJar jar)
        {
            if (player.IsAdmin || player.HasPermission("inventoryguard.ignore.*")) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                int effectiveLimit = restriction.MaxAmount;
                
                foreach (var permission in player.Permissions)
                {
                    string prefix = $"limit.{restriction.ItemId}.";
                    if (permission.Name.StartsWith(prefix))
                    {
                        if (int.TryParse(permission.Name.Substring(prefix.Length), out int customLimit))
                        {
                            effectiveLimit = customLimit;
                        }
                    }
                }

                if (player.HasPermission($"inventoryguard.ignore.{restriction.ItemId}")) continue;

                int count = 0;
                for (byte page = 0; page < PlayerInventory.PAGES - 2; page++)
                {
                    var items = player.Inventory.items[page];
                    for (byte i = 0; i < items.items.Count; i++)
                    {
                        if (items.items[i].item.id == restriction.ItemId) count++;
                    }
                }

                if (count > effectiveLimit)
                {
                    player.Inventory.askDropItem(player.CSteamID, group, jar.x, jar.y);
                    // Используем стандартный метод чата для LDM
                    Rocket.Unturned.Chat.UnturnedChat.Say(player, $"[Лимит] Предмет {restriction.ItemId} сброшен! Лимит: {effectiveLimit}", Color.yellow);
                }
            }
        }
    }
}
