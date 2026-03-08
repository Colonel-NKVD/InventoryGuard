using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
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
        public void Defaults()
        {
            RestrictedItems = new List<RestrictedItem> { 
                new RestrictedItem { ItemId = 17, MaxAmount = 2 },
                new RestrictedItem { ItemId = 132, MaxAmount = 1 }
            };
        }
    }

    public class InventoryGuard : RocketPlugin<InventoryGuardConfiguration>
    {
        protected override void Load()
        {
            UnturnedPlayerEvents.OnInventoryUpdated += OnInventoryUpdated;
            Rocket.Core.Logging.Logger.Log("InventoryGuard для Iron & Mud загружен!");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnInventoryUpdated -= OnInventoryUpdated;
        }

        private void OnInventoryUpdated(UnturnedPlayer player, InventoryGroup group, byte index, ItemJar jar)
        {
            if (player.IsAdmin || player.HasPermission("inventoryguard.ignore.*")) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
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
                    count += player.Inventory.items[page].items.FindAll(x => x.item.id == restriction.ItemId).Count;
                }

                if (count > effectiveLimit && jar.item.id == restriction.ItemId)
                {
                    player.Inventory.askDropItem(player.CSteamID, (byte)group, jar.x, jar.y);
                    UnturnedChat.Say(player, $"[Лимит] Предмет {restriction.ItemId} сброшен под ноги! Лимит: {effectiveLimit}", Color.yellow);
                }
            }
        }
    }
}