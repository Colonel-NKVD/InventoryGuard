using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;

namespace InventoryGuard
{
    public class RestrictedItem { public ushort ItemId; public int MaxAmount; }

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
        private DateTime lastCheck = DateTime.Now;

        static InventoryGuard()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.Contains("Assembly-CSharp-firstpass"))
                    return Assembly.GetAssembly(typeof(ItemJar));
                return null;
            };
        }

        protected override void Load()
        {
            Rocket.Core.Logging.Logger.Log("InventoryGuard: Режим ВЫБРОСА предметов активен. Сканирование ящиков отключено.");
        }

        void FixedUpdate()
        {
            // Проверка каждые 2 секунды
            if ((DateTime.Now - lastCheck).TotalSeconds < 2) return;
            lastCheck = DateTime.Now;

            foreach (var steamPlayer in Provider.clients)
            {
                UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(steamPlayer);
                if (player == null || player.IsAdmin) continue;
                ExecuteGuard(player);
            }
        }

        private void ExecuteGuard(UnturnedPlayer player)
        {
            foreach (var restriction in Configuration.Instance.RestrictedItems)
            {
                int count = 0;
                
                // ИЗМЕНЕНИЕ ЗДЕСЬ: 
                // Проходим только по личным вещам игрока, останавливаясь перед открытыми ящиками (STORAGE)
                for (byte p = 0; p < PlayerInventory.STORAGE; p++)
                {
                    var pageItems = player.Inventory.items[p];
                    if (pageItems == null) continue;

                    // Идем с конца списка, чтобы не сбить индексы при удалении
                    for (int i = pageItems.getItemCount() - 1; i >= 0; i--)
                    {
                        var jar = pageItems.getItem((byte)i);
                        if (jar?.item?.id == restriction.ItemId)
                        {
                            count++;
                            
                            // Если лимит превышен
                            if (count > restriction.MaxAmount)
                            {
                                // 1. Выбрасываем ПЕРЕД игроком на землю
                                ItemManager.dropItem(jar.item, player.Position, false, true, true);
                                
                                // 2. Удаляем из инвентаря
                                player.Inventory.removeItem(p, (byte)i);

                                UnturnedChat.Say(player, $"[Guard] Лимит ID {restriction.ItemId} превышен! Лишнее выпало на землю.", Color.yellow);
                                Rocket.Core.Logging.Logger.Log($"[Guard] Предмет {restriction.ItemId} выброшен у {player.CharacterName}");
                            }
                        }
                    }
                }
            }
        }
    }
}
