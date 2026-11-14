using System;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.Status
{
    /// <summary>
    /// Состояние мобов по пакетам сервера (PLAN Стадия 4, п. 4): статус (краткий и полный), быстрые обновления, полоски,
    /// привязанный питомец, замки статов. Моб, которого нет в мире, — пропуск и счётчик <see cref="UnknownMobile"/> (сервер
    /// шлёт состояние только видимых мобов; заглушку тут не заводим: без тела и места её нечем показать). О каждом
    /// изменении — событие <see cref="Changed"/>.
    /// </summary>
    public sealed class MobileStatusUpdates
    {
        private const int PoisonBar = 1;
        private const int YellowBar = 2;

        private readonly EntityStore _store;

        public MobileStatusUpdates(EntityStore store)
        {
            _store = store;
        }

        public event Action<Mobile>? Changed;

        public long UnknownMobile { get; private set; }

        public void OnCompactStatus(Serial serial, string name, Vital hits, bool canBeRenamed)
        {
            var mobile = Find(serial);
            if (mobile != null)
            {
                SetHead(mobile.Status, name, hits, canBeRenamed, 0);
                Changed?.Invoke(mobile);
            }
        }

        public void OnFullStatus(Serial serial, string name, Vital hits, bool canBeRenamed, int version, in StatusSheet sheet)
        {
            var mobile = Find(serial);
            if (mobile == null)
            {
                return;
            }

            var status = mobile.Status;
            SetHead(status, name, hits, canBeRenamed, version);
            status.Female = sheet.Female;
            status.Stam = sheet.Stam;
            status.Mana = sheet.Mana;
            status.Attributes = sheet.Attributes;
            status.Followers = sheet.Followers;
            status.FollowersMax = sheet.FollowersMax;
            Changed?.Invoke(mobile);
        }

        public void OnVital(Serial serial, VitalKind kind, Vital value)
        {
            var mobile = Find(serial);
            if (mobile == null)
            {
                return;
            }

            if (kind == VitalKind.Hits)
            {
                mobile.Status.Hits = value;
            }
            else if (kind == VitalKind.Mana)
            {
                mobile.Status.Mana = value;
            }
            else
            {
                mobile.Status.Stam = value;
            }

            Changed?.Invoke(mobile);
        }

        public void OnAttributes(Serial serial, Vital hits, Vital mana, Vital stam)
        {
            var mobile = Find(serial);
            if (mobile != null)
            {
                mobile.Status.Hits = hits;
                mobile.Status.Mana = mana;
                mobile.Status.Stam = stam;
                Changed?.Invoke(mobile);
            }
        }

        /// <summary>0x17: вид 1 — яд (уровень яда + 1, 0 — нет), 2 — жёлтая полоска (1 — есть); другие виды пропускаются.</summary>
        public void OnHealthbar(Serial serial, int kind, int level)
        {
            var mobile = kind == PoisonBar || kind == YellowBar ? Find(serial) : null;
            if (mobile == null)
            {
                return;
            }

            if (kind == PoisonBar)
            {
                mobile.Status.PoisonLevel = level;
            }
            else
            {
                mobile.Status.YellowHealthbar = level != 0;
            }

            Changed?.Invoke(mobile);
        }

        public void OnBonded(Serial serial, bool dead)
        {
            var mobile = Find(serial);
            if (mobile != null)
            {
                mobile.Status.IsDeadBonded = dead;
                Changed?.Invoke(mobile);
            }
        }

        public void OnStatLocks(Serial serial, StatLocks locks)
        {
            var mobile = Find(serial);
            if (mobile != null)
            {
                mobile.Status.Locks = locks;
                Changed?.Invoke(mobile);
            }
        }

        private static void SetHead(MobileStatus status, string name, Vital hits, bool canBeRenamed, int version)
        {
            status.Name = name;
            status.Hits = hits;
            status.CanBeRenamed = canBeRenamed;
            status.Version = version;
        }

        private Mobile? Find(Serial serial)
        {
            var mobile = _store.FindMobile(serial);
            if (mobile == null)
            {
                UnknownMobile++;
            }

            return mobile;
        }
    }
}
