using System;
using System.Collections.Generic;
using System.Reflection;
using Rocket.API;
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
            RestrictedItems = new List<RestrictedItem> { new RestrictedItem { ItemId = 17, MaxAmount = 2 } };
        }
    }

    public class InventoryGuard : RocketPlugin<InventoryGuardConfiguration>
    {
        static InventoryGuard()
        {
            // Эта часть исправляет ошибку с отсутствующим firstpass
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
            U.Events.OnPlayerConnected += OnPlayerConnected;
            Rocket.Core.Logging.Logger.Log("InventoryGuard: Запущен и готов к тесту!");
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            // Подписываемся на обновление инвентаря
            player.Inventory.onInventoryUpdated += (byte page, byte index, ItemJar jar) =>
            {
                CheckInventory(player, page, index, jar);
            };
        }

        private void CheckInventory(UnturnedPlayer player, byte page, byte index, ItemJar jar)
        {
            if (player == null || jar == null || jar.item == null) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                // ВРЕМЕННО закомментирована проверка на админа для теста
                // if (player.IsAdmin) return; 

                if (Rocket.Core.R.Permissions.HasPermission(player, "inventoryguard.ignore.*")) continue;

                int count = 0;
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
                    // ИСПОЛЬЗУЕМ НОВЫЙ МЕТОД (вместо askDropItem)
                    player.Inventory.sendDropItem(page, jar.x, jar.y);
                    
                    UnturnedChat.Say(player, $"[Guard] Предмет {restriction.ItemId} превысил лимит ({restriction.MaxAmount} шт.)", Color.yellow);
                }
            }
        }
    }
}
