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
            // По умолчанию ограничение на ID 17 (магазин), максимум 2 штуки
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
            // Используем глобальное событие инвентаря для ВСЕХ игроков сразу
            ItemManager.onInventoryAdded += OnInventoryAdded;
            
            Rocket.Core.Logging.Logger.Log("InventoryGuard: Глобальная слежка запущена!");
        }

        protected override void Unload()
        {
            ItemManager.onInventoryAdded -= OnInventoryAdded;
        }

        // Это сработает, как только любой предмет попадет в любой рюкзак
        private void OnInventoryAdded(PlayerInventory inventory, byte page, byte index, ItemJar jar)
        {
            UnturnedPlayer player = UnturnedPlayer.FromPlayer(inventory.player);
            
            if (player == null || jar == null || jar.item == null) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                // ВРЕМЕННО УБРАНА ПРОВЕРКА НА АДМИНА ДЛЯ ТЕСТА
                if (R.Permissions.HasPermission(player, "inventoryguard.ignore.*")) continue;

                int count = 0;
                // Проверяем все сумки и карманы
                for (byte p = 0; p < PlayerInventory.PAGES; p++)
                {
                    var itemsPage = inventory.items[p];
                    if (itemsPage == null || itemsPage.items == null) continue;
                    
                    foreach (var item in itemsPage.items)
                    {
                        if (item?.item?.id == restriction.ItemId) count++;
                    }
                }

                if (count > restriction.MaxAmount)
                {
                    // Выкидываем лишнее
                    inventory.askDropItem(player.CSteamID, page, jar.x, jar.y);
                    UnturnedChat.Say(player, $"[Guard] Предмет {restriction.ItemId} ограничен! Лимит: {restriction.MaxAmount}", Color.red);
                    Rocket.Core.Logging.Logger.Log($"[Guard] Выкинули предмет {restriction.ItemId} у игрока {player.CharacterName}");
                }
            }
        }
    }
}
