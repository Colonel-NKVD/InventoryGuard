using System;
using System.Collections.Generic;
using System.Reflection; // Это нужно добавить для работы Assembly
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
        // --- ВСТАВЛЯТЬ СЮДА (НАЧАЛО) ---
        static InventoryGuard()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                // Если система ищет несуществующий firstpass, мы говорим ей использовать основной файл игры
                if (args.Name.Contains("Assembly-CSharp-firstpass"))
                {
                    return Assembly.GetAssembly(typeof(ItemJar));
                }
                return null;
            };
        }
        // --- ВСТАВЛЯТЬ СЮДА (КОНЕЦ) ---

        protected override void Load()
        {
            U.Events.OnPlayerConnected += OnPlayerConnected;
            Rocket.Core.Logging.Logger.Log("InventoryGuard (Direct Mode) загружен!");
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            player.Inventory.onInventoryUpdated += (byte page, byte index, ItemJar jar) => 
            {
                OnInternalInventoryUpdated(player, page, index, jar);
            };
        }

        private void OnInternalInventoryUpdated(UnturnedPlayer player, byte page, byte index, ItemJar jar)
        {
            // ВАЖНО: Если ты АДМИН, плагин ничего не сделает. Для теста сними админку!
            if (player == null || player.IsAdmin || jar == null || jar.item == null) return;

            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                if (jar.item.id != restriction.ItemId) continue;

                if (R.Permissions.HasPermission(player, "inventoryguard.ignore.*") || 
                    R.Permissions.HasPermission(player, $"inventoryguard.ignore.{jar.item.id}")) continue;

                int count = 0;
                for (byte p = 0; p < PlayerInventory.PAGES - 2; p++)
                {
                    var itemsPage = player.Inventory.items[p];
                    if (itemsPage == null) continue;
                    foreach (var item in itemsPage.items)
                    {
                        if (item != null && item.item != null && item.item.id == restriction.ItemId) count++;
                    }
                }

                if (count > restriction.MaxAmount)
                {
                    player.Inventory.askDropItem(player.CSteamID, page, jar.x, jar.y);
                    UnturnedChat.Say(player, $"[Лимит] Предмет {restriction.ItemId} превышен! Максимум: {restriction.MaxAmount}", Color.yellow);
                }
            }
        }
    }
}
