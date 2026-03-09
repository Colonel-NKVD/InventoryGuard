using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
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
            // Используем прямой доступ к игровым событиям Unturned через Rocket
            ItemManager.onInventoryUpdated += OnInventoryUpdated;
            Logger.Log("InventoryGuard (LDM 2026) загружен корректно!");
        }

        protected override void Unload()
        {
            ItemManager.onInventoryUpdated -= OnInventoryUpdated;
        }

        private void OnInventoryUpdated(PlayerInventory inventory, byte page, byte index, ItemJar jar)
        {
            UnturnedPlayer player = UnturnedPlayer.FromPlayer(inventory.player);
            if (player == null || player.IsAdmin) return;

            // Проверка общего иммунитета
            if (R.Permissions.HasPermission(player, "inventoryguard.ignore.*")) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                int effectiveLimit = restriction.MaxAmount;
                
                // Проверка персональных лимитов через систему разрешений Rocket
                if (R.Permissions.HasPermission(player, $"inventoryguard.ignore.{restriction.ItemId}")) continue;

                // Поиск кастомного лимита в разрешениях
                var pGroup = R.Permissions.GetGroups(player, false);
                foreach (var group in pGroup)
                {
                    foreach (var perm in group.Permissions)
                    {
                        string prefix = $"limit.{restriction.ItemId}.";
                        if (perm.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(perm.Name.Substring(prefix.Length), out int customLimit))
                            {
                                effectiveLimit = Math.Max(effectiveLimit, customLimit);
                            }
                        }
                    }
                }

                int count = 0;
                for (byte p = 0; p < PlayerInventory.PAGES - 2; p++)
                {
                    var itemsPage = inventory.items[p];
                    if (itemsPage == null) continue;
                    
                    for (int i = 0; i < itemsPage.items.Count; i++)
                    {
                        if (itemsPage.items[i].item.id == restriction.ItemId) count++;
                    }
                }

                if (count > effectiveLimit)
                {
                    inventory.askDropItem(player.CSteamID, page, jar.x, jar.y);
                    Rocket.Unturned.Chat.UnturnedChat.Say(player, $"[Лимит] {restriction.ItemId} сброшен! Лимит: {effectiveLimit}", Color.yellow);
                }
            }
        }
    }
}
