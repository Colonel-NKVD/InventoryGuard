using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
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
            // Исправлено: подписка на событие в LDM
            UnturnedPlayerEvents.OnPlayerInventoryUpdated += OnInventoryUpdated;
            Logger.Log("InventoryGuard (LDM 2026) загружен!");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerInventoryUpdated -= OnInventoryUpdated;
        }

        private void OnInventoryUpdated(UnturnedPlayer player, InventoryGroup group, byte index, ItemJar jar)
        {
            if (player == null || player.IsAdmin || player.HasPermission("inventoryguard.ignore.*")) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                int effectiveLimit = restriction.MaxAmount;
                
                // Исправлено получение разрешений
                var permissions = player.GetPermissions();
                foreach (var permission in permissions)
                {
                    string prefix = $"limit.{restriction.ItemId}.";
                    if (permission.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
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
                    player.Inventory.askDropItem(player.CSteamID, (byte)group, jar.x, jar.y);
                    Rocket.Unturned.Chat.UnturnedChat.Say(player, $"[Лимит] Предмет {restriction.ItemId} превышен ({effectiveLimit}) и сброшен!", Color.yellow);
                }
            }
        }
    }
}
