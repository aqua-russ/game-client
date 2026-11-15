using System;
using System.Collections.Generic;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.World;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Мир одного входа в Unity — как BotClient.CreateWorld у бота: мобы и предметы по пакетам сервера
    /// (<see cref="EntityStore"/>), показ шагов чужих (<see cref="OthersMotion"/>) и движение своего персонажа
    /// (<see cref="Movement"/>; null без файлов данных). <see cref="Dispose"/> отписывает всё от входа и мира.
    /// </summary>
    public sealed class UnityWorld : IDisposable
    {
        private readonly WorldEntry _entry;

        private UnityWorld(WorldEntry entry, EntityStore entities, OthersMotion others, UnityMovement movement)
        {
            _entry = entry;
            Entities = entities;
            Others = others;
            Movement = movement;
            // Сначала мир узнаёт своего персонажа, потом движение ставит его на клетку сервера (так же у бота).
            _entry.PlayerConfirmed += Entities.Reset;
            if (Movement != null)
            {
                _entry.PlayerConfirmed += Movement.OnPlayerConfirmed;
            }
        }

        public EntityStore Entities { get; }

        public OthersMotion Others { get; }

        /// <summary>Движение своего персонажа; null — файлов данных нет, ходьбы нет.</summary>
        public UnityMovement Movement { get; }

        /// <param name="data">Файлы данных; null — без предсказания шага ходьбы нет.</param>
        public static UnityWorld Create(PacketDispatcher dispatcher, NetworkSession session, WorldEntry entry, UnityWorldData data,
            IClock clock, ILog log)
        {
            var entities = new EntityStore();
            var others = new OthersMotion(entities, clock);
            var movement = data == null ? null : UnityMovement.Create(dispatcher, session, entities, data, clock, log);
            WorldHandlers.Register(dispatcher, entities, new WorldEnvironment(), movement?.Replies, others);
            return new UnityWorld(entry, entities, others, movement);
        }

        /// <summary>Таймеры тика в порядке бота: показ чужих, темп шагов, затем показ своих шагов (после темпа — в том же тике).</summary>
        public void AddTimers(List<ITickHandler> timers)
        {
            timers.Add(Others);
            if (Movement != null)
            {
                timers.Add(Movement.Pacer);
                timers.Add(Movement.Show);
            }
        }

        public void Dispose()
        {
            _entry.PlayerConfirmed -= Entities.Reset;
            if (Movement != null)
            {
                _entry.PlayerConfirmed -= Movement.OnPlayerConfirmed;
                Movement.Dispose();
            }

            Others.Dispose();
        }
    }
}
