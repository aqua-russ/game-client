using System;
using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// fight:0xТЕЛО (PLAN Стадия 4, п. 7): боевая стойка (0x72, ждём ответа сервера), атака ближайшего моба с этим телом
    /// (0x05), противник — по 0xAA сервера, смерть — моб исчез и рядом лёг его труп (0x2006, количество = тело, Corpse.cs).
    /// В лог — ход боя: здоровье противника и бота. Бой долгий — свой срок <see cref="BotScript.FightTimeoutMs"/>.
    /// </summary>
    internal sealed class FightStep : IBotStep
    {
        public const ushort CorpseGraphic = 0x2006;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _body;
        private readonly List<Item> _items = new List<Item>();
        private Serial _target;
        private Serial _combatant;
        private Point3D _lastPosition;
        private bool _warRequested;
        private bool _attacked;
        private bool _combatantSeen;
        private bool _gone;
        private Vital _targetHits;
        private Vital _playerHits;
        private Vital _lowestPlayerHits;

        public FightStep(BotWorld world, ILog log, string body)
        {
            _world = world;
            _log = log;
            BotAction.TryParseGraphic(body, out _body);
            Failure = world.Combat == null ? "бой у бота не подключён." : string.Empty;
        }

        public int TimeoutMs => BotScript.FightTimeoutMs;

        public string TimeoutProblem
        {
            get
            {
                var after = " за " + BotScript.FightTimeoutMs / 1000 + " с.";
                return !_attacked ? "сервер не включил боевую стойку" + after
                    : !_combatantSeen ? "сервер не назвал " + _target + " противником (0xAA)" + after
                    : _gone ? "противник пропал из виду, а его трупа (0x2006) рядом нет" + after
                    : "противник не умер" + after + " Здоровье противника " + _targetHits + ".";
            }
        }

        public string Failure { get; private set; }

        public bool Poll()
        {
            if (_world.Combat == null || _world.Entities.Player == null)
            {
                return false;
            }

            if (_target == Serial.Zero && !PickTarget())
            {
                return false;
            }

            if (!_attacked)
            {
                StartAttack(_world.Combat);
                return false;
            }

            Watch(_world.Combat);
            return CheckDeath();
        }

        public void Finish()
        {
        }

        private bool PickTarget()
        {
            var player = _world.Entities.Player!;
            Mobile? nearest = null;
            foreach (var mobile in _world.Entities.Mobiles)
            {
                if (mobile.Serial != player.Serial && mobile.Body == _body &&
                    (nearest == null || Distance(player, mobile) < Distance(player, nearest)))
                {
                    nearest = mobile;
                }
            }

            if (nearest == null)
            {
                Failure = "моба с телом 0x" + _body.ToString("X4") + " рядом нет.";
                return false;
            }

            _target = nearest.Serial;
            _lastPosition = nearest.Position;
            _log.Info("Бой: противник " + _target + " (тело 0x" + _body.ToString("X4") + ") в " + _lastPosition + ".");
            return true;
        }

        private void StartAttack(BotCombat combat)
        {
            if (!combat.State.WarMode)
            {
                if (!_warRequested)
                {
                    _warRequested = combat.Commands.SetWarMode(true);
                    _log.Info("Стойка: прошу боевую.");
                }

                return;
            }

            _log.Info("Стойка: боевая.");
            if (!combat.Commands.Attack(_target))
            {
                Failure = "атаковать " + _target + " нельзя.";
                return;
            }

            _attacked = true;
            _world.Status?.Commands.RequestStatus(_target);
            _log.Info("Атака " + _target + ".");
        }

        private void Watch(BotCombat combat)
        {
            if (combat.State.Combatant != _combatant)
            {
                _combatant = combat.State.Combatant;
                _combatantSeen |= _combatant == _target;
                _log.Info("Противник по серверу: " + (_combatant == Serial.Zero ? "нет."
                    : _combatant == _target ? _combatant + "." : _combatant + " — не тот, кого атаковали."));
            }

            var target = _world.Entities.FindMobile(_target);
            if (target != null)
            {
                _lastPosition = target.Position;
                LogHits("Противник", target.Status.Hits, ref _targetHits);
            }

            var hits = _world.Entities.Player!.Status.Hits;
            LogHits("Бот", hits, ref _playerHits);
            var hurt = hits.Maximum > 0 && hits.Current < hits.Maximum;
            if (hurt && (_lowestPlayerHits.Maximum == 0 || hits.Current < _lowestPlayerHits.Current))
            {
                _lowestPlayerHits = hits;
            }
        }

        private bool CheckDeath()
        {
            if (_world.Entities.FindMobile(_target) != null)
            {
                return false;
            }

            _gone = true;
            var corpse = FindCorpse();
            if (corpse == null)
            {
                return false;
            }

            if (!_combatantSeen)
            {
                Failure = "противник умер, а сервер так и не назвал его противником (0xAA).";
                return false;
            }

            _log.Info("Противник " + _target + " убит: труп " + corpse.Serial + " в " + corpse.Position + ".");
            _log.Info(_lowestPlayerHits.Maximum > 0
                ? "Бот получил урон: здоровье опускалось до " + _lowestPlayerHits + "."
                : "Бот урона не получил.");
            return true;
        }

        private Item? FindCorpse()
        {
            _world.Entities.Spatial.QueryItems(_lastPosition.X, _lastPosition.Y, 1, _items);
            foreach (var item in _items)
            {
                if (item.Graphic == CorpseGraphic && item.Amount == _body)
                {
                    return item;
                }
            }

            return null;
        }

        private void LogHits(string who, Vital hits, ref Vital last)
        {
            if (hits.Maximum == 0 || (hits.Current == last.Current && hits.Maximum == last.Maximum))
            {
                return;
            }

            last = hits;
            _log.Info(who + ": здоровье " + hits + ".");
        }

        private static int Distance(Mobile a, Mobile b) =>
            Math.Max(Math.Abs(a.Position.X - b.Position.X), Math.Abs(a.Position.Y - b.Position.Y));
    }
}
