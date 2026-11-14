using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Combat;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.Combat
{
    /// <summary>
    /// Боевая стойка и атака (PLAN Стадия 4, п. 7). Стойку меняет только сервер: на запрос 0x72 он отвечает тем же пакетом
    /// (Mobile.Warmode → SendSetWarMode; одно и то же значение — без ответа, частые смены придерживает — DelayChangeWarmode),
    /// поэтому клиент ждёт ответа. Запрос атаки 0x05 лишь назначает противника (Mobile.Attack → Combatant), о нём — 0xAA;
    /// стойку при атаке сервер сам не включает, выход из стойки сбрасывает противника (Mobile.Warmode).
    /// </summary>
    public sealed class CombatCommandsTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Rat = new Serial(0x100);

        private EntityStore _store = null!;
        private CombatState _state = null!;
        private RecordingCombatSender _sender = null!;
        private CombatCommands _commands = null!;
        private List<string> _events = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _store.Reset(new PlayerState(Player, 0x190, new Point3D(1, 1, 0), Direction.North, new MapSize(7168, 4096)));
            _state = new CombatState();
            _events = new List<string>();
            _state.WarModeChanged += () => _events.Add("стойка " + _state.WarMode);
            _state.CombatantChanged += () => _events.Add("противник " + _state.Combatant.Value.ToString("X"));
            _sender = new RecordingCombatSender();
            _commands = new CombatCommands(_sender, _state, _store);
        }

        [Test]
        public void SetWarMode_SentButStateWaitsForServer()
        {
            Assert.That(_commands.SetWarMode(true), Is.True);

            Assert.That(_sender.Sent, Is.EqualTo(new[] { "стойка True" }));
            Assert.That(_state.WarMode, Is.False, "стойка — только по ответу сервера");

            _state.OnWarMode(true);
            Assert.That(_state.WarMode, Is.True);
            Assert.That(_events, Is.EqualTo(new[] { "стойка True" }));
        }

        [Test]
        public void SetWarMode_SameAsServer_NothingSent()
        {
            Assert.That(_commands.SetWarMode(false), Is.False);
            Assert.That(_sender.Sent, Is.Empty);
        }

        [Test]
        public void ServerRepeatsSameWarMode_NoEvent()
        {
            _state.OnWarMode(false);

            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void Attack_SendsTarget_CombatantFromServer()
        {
            Assert.That(_commands.Attack(Rat), Is.True);
            Assert.That(_sender.Sent, Is.EqualTo(new[] { "атака 100" }));
            Assert.That(_state.Combatant, Is.EqualTo(Serial.Zero), "противник — только по 0xAA");

            _state.OnCombatant(Rat);
            Assert.That(_state.Combatant, Is.EqualTo(Rat));
            _state.OnCombatant(Serial.Zero);
            Assert.That(_state.HasCombatant, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "противник 100", "противник 0" }));
        }

        [Test]
        public void Attack_SelfItemOrZero_NothingSent()
        {
            Assert.That(_commands.Attack(Player), Is.False);
            Assert.That(_commands.Attack(new Serial(0x40000001)), Is.False);
            Assert.That(_commands.Attack(Serial.Zero), Is.False);
            Assert.That(_sender.Sent, Is.Empty);
        }

        [Test]
        public void Reset_ForgetsWarModeAndCombatant()
        {
            _state.OnWarMode(true);
            _state.OnCombatant(Rat);

            _state.Reset();

            Assert.That((_state.WarMode, _state.HasCombatant), Is.EqualTo((false, false)));
        }

        private sealed class RecordingCombatSender : ICombatSender
        {
            public List<string> Sent { get; } = new List<string>();

            public void SendWarMode(bool warMode) => Sent.Add("стойка " + warMode);

            public void SendAttack(Serial target) => Sent.Add("атака " + target.Value.ToString("X"));
        }
    }
}
