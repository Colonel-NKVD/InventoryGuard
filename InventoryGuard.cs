using System;
using System.Collections.Generic;
using System.Reflection;
using Rocket.API;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
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
        static InventoryGuard()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.Contains("Assembly-CSharp-firstpass"))
                {
                    return Assembly.GetAssembly(typeof(ItemJar));
                }
                return null;
            };
        }

        protected override void Load()
        {
            // Подписываемся через стандартное событие RocketMod
            RocketUnturnedPlayerEvents.OnPlayerInventoryUpdated += OnInventoryUpdated;
            
            Rocket.Core.Logging.Logger.Log("InventoryGuard: Защита активирована!");
        }

        protected override void Unload()
        {
            RocketUnturnedPlayerEvents.OnPlayerInventoryUpdated -= OnInventoryUpdated;
        }

        private void OnInventoryUpdated(UnturnedPlayer player, InventoryGroup group, byte page, byte index, ItemJar jar)
        {
            if (player == null || jar == null || jar.item == null) return;

            // Тестируем даже на админах, чтобы ты сразу увидел результат
            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                // Если есть спец-право, игнорируем
                if (R.Permissions.HasPermission(player, "inventoryguard.ignore.*")) continue;

                int count = 0;
                // Считаем предметы во всех сумках
                for (byte p = 0; p < PlayerInventory.PAGES; p++)
                {
                    var itemsPage = player.Inventory.items[p];
                    if (itemsPage == null || itemsPage.items == null) continue;

                    foreach (var item in itemsPage.items)
                    {
                        if (item?.item?.id == restriction.ItemId) count++;
                    }
                }

                if (count > restriction.MaxAmount)
                {
                    // Важный момент: задержка в полсекунды, чтобы игра успела обработать предмет перед тем как его выкинуть
                    player.Inventory.askDropItem(player.CSteamID, page, jar.x, jar.y);
                    UnturnedChat.Say(player, $"[Guard] Лимит ID {restriction.ItemId} превышен! (Макс: {restriction.MaxAmount})", Color.red);
                }
            }
        }
    }
}
