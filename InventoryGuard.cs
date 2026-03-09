using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq; // Нужно для работы со списками
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
            // Ограничение: ID 17 (магазин), не больше 2 штук
            RestrictedItems = new List<RestrictedItem> { new RestrictedItem { ItemId = 17, MaxAmount = 2 } };
        }
    }

    public class InventoryGuard : RocketPlugin<InventoryGuardConfiguration>
    {
        private DateTime lastCheck = DateTime.Now;

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
            Rocket.Core.Logging.Logger.Log("InventoryGuard: ЗАПУЩЕН. Режим постоянной проверки активен.");
        }

        // FixedUpdate работает постоянно, пока включен сервер
        void FixedUpdate()
        {
            // Проверяем инвентари раз в 3 секунды, чтобы не нагружать сервер
            if ((DateTime.Now - lastCheck).TotalSeconds < 3) return;
            lastCheck = DateTime.Now;

            foreach (var player in Provider.clients.Select(c => UnturnedPlayer.FromSteamPlayer(c)))
            {
                if (player == null) continue;
                
                // Пропускаем админов, чтобы они могли строить/тестить
                if (player.IsAdmin) continue;

                ExecuteGuard(player);
            }
        }

        private void ExecuteGuard(UnturnedPlayer player)
        {
            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                int count = 0;
                // Список для удаления лишних предметов
                List<ItemAddress> toDrop = new List<ItemAddress>();

                for (byte p = 0; p < PlayerInventory.PAGES; p++)
                {
                    var pageItems = player.Inventory.items[p];
                    if (pageItems == null) continue;

                    for (byte i = 0; i < pageItems.getItemCount(); i++)
                    {
                        var jar = pageItems.getItem(i);
                        if (jar?.item?.id == restriction.ItemId)
                        {
                            count++;
                            // Если предметов больше лимита - запоминаем этот адрес
                            if (count > restriction.MaxAmount)
                            {
                                toDrop.Add(new ItemAddress(p, jar.x, jar.y));
                            }
                        }
                    }
                }

                // Выбрасываем все "лишние" найденные предметы
                foreach (var addr in toDrop)
                {
                    player.Inventory.sendDropItem(addr.page, addr.x, addr.y);
                    UnturnedChat.Say(player, $"[Guard] Лимит ID {restriction.ItemId} превышен! Лишнее выброшено.", Color.yellow);
                }
            }
        }

        // Вспомогательная структура для адреса предмета
        private struct ItemAddress
        {
            public byte page;
            public byte x;
            public byte y;
            public ItemAddress(byte p, byte x, byte y) { page = p; this.x = x; this.y = y; }
        }
    }
}
