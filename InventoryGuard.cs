using System;
using System.Collections.Generic;
using System.Reflection;
using Rocket.API;
using Rocket.Core.Plugins;
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
        // Кэш конфига для поиска за O(1)
        private Dictionary<ushort, int> limitsCache;
        
        // Глобальный переиспользуемый словарь (Zero Allocation)
        private Dictionary<ushort, int> tempCounts;

        // Индекс для амортизации нагрузки
        private int playerCheckIndex = 0;

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
            // 1. Инициализируем кэш лимитов при загрузке
            limitsCache = new Dictionary<ushort, int>();
            foreach (var item in Configuration.Instance.RestrictedItems)
            {
                limitsCache[item.ItemId] = item.MaxAmount;
            }

            // 2. Создаем словарь для подсчета один раз
            tempCounts = new Dictionary<ushort, int>();

            Rocket.Core.Logging.Logger.Log("InventoryGuard: Режим максимальной оптимизации (Frame Slicing) активен.");
        }
        
        void FixedUpdate()
        {
            int playerCount = Provider.clients.Count;
            if (playerCount == 0) return;

            // Сдвигаем индекс. Проверяем строго 1 игрока за 1 физический тик (50 раз в секунду).
            playerCheckIndex++;
            if (playerCheckIndex >= playerCount) 
            {
                playerCheckIndex = 0; // Сброс, если дошли до конца списка или кто-то вышел
            }

            SteamPlayer client = Provider.clients[playerCheckIndex];
            Player player = client?.player;

            // ИСПРАВЛЕНИЕ: Используем правильный путь к статусу администратора
            if (player == null || player.channel.owner.isAdmin) return;

            ExecuteGuard(player, client);
        }

        private void ExecuteGuard(Player player, SteamPlayer client)
        {
            // Очищаем глобальный словарь вместо создания нового (спасает Garbage Collector от лишней работы)
            tempCounts.Clear();

            // Проходим только по личным вещам игрока, игнорируя ящики (p < PlayerInventory.STORAGE)
            for (byte p = 0; p < PlayerInventory.STORAGE; p++)
            {
                var page = player.inventory.items[p];
                if (page == null) continue;

                // Идем с конца, чтобы безопасно удалять предметы
                for (int i = page.getItemCount() - 1; i >= 0; i--)
                {
                    var jar = page.getItem((byte)i);
                    ushort id = jar.item.id;

                    // Если предмет числится в кэше лимитов
                    if (limitsCache.TryGetValue(id, out int maxAmount))
                    {
                        // Считаем количество в инвентаре
                        tempCounts.TryGetValue(id, out int currentCount);
                        currentCount++;
                        tempCounts[id] = currentCount;

                        if (currentCount > maxAmount)
                        {
                            // 1. Выбрасываем ПЕРЕД игроком на землю
                            ItemManager.dropItem(jar.item, player.transform.position, false, true, true);
                            
                            // 2. Удаляем из инвентаря
                            player.inventory.removeItem(p, (byte)i);

                            // Отправляем сообщение конкретному игроку через его SteamID
                            UnturnedChat.Say(client.playerID.steamID, $"[Guard] Лимит ID {id} превышен! Лишнее выпало на землю.", Color.yellow);
                            Rocket.Core.Logging.Logger.Log($"[Guard] Предмет {id} выброшен у {player.channel.owner.playerID.characterName}");
                        }
                    }
                }
            }
        }
    }
}
