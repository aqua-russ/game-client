using System;
using NUnit.Framework;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Diagnostics
{
    /// <summary>
    /// Настройки режима «симуляция пинга» (PLAN Стадия 3, п. 9; ADR 0020 п. 9): задержка и разброс отдельно для входящих и
    /// исходящих пакетов, по умолчанию выключено. Значения по умолчанию и пределы — ClientLimits (TARGET.md §3.1): 50 мс
    /// ±15 в каждую сторону, задержка до 1000 мс, разброс до 500 мс. Задержка пакета — постоянная часть плюс равномерно
    /// от −разброса до +разброса, не меньше нуля.
    /// </summary>
    public sealed class LagSimulationTests
    {
        [Test]
        public void ByDefault_Off_WithDefaultValues()
        {
            var lag = new LagSimulation();

            Assert.That(lag.Enabled, Is.False);
            Assert.That(lag.Incoming.IsNone, Is.True);
            Assert.That(lag.Outgoing.IsNone, Is.True);
            Assert.That(ClientLimits.LagSimulationDefaultDelayMs, Is.EqualTo(50));
            Assert.That(ClientLimits.LagSimulationDefaultJitterMs, Is.EqualTo(15));
            Assert.That(lag.IncomingDelayMs, Is.EqualTo(50));
            Assert.That(lag.IncomingJitterMs, Is.EqualTo(15));
            Assert.That(lag.OutgoingDelayMs, Is.EqualTo(50));
            Assert.That(lag.OutgoingJitterMs, Is.EqualTo(15));
        }

        [Test]
        public void Enabled_EachDirectionItsOwnValues()
        {
            var lag = new LagSimulation
            {
                Enabled = true,
                IncomingDelayMs = 200,
                IncomingJitterMs = 30,
                OutgoingDelayMs = 80,
                OutgoingJitterMs = 0
            };

            Assert.That(lag.Incoming.DelayMs, Is.EqualTo(200));
            Assert.That(lag.Incoming.JitterMs, Is.EqualTo(30));
            Assert.That(lag.Outgoing.DelayMs, Is.EqualTo(80));
            Assert.That(lag.Outgoing.JitterMs, Is.EqualTo(0));
            Assert.That(lag.Outgoing.IsNone, Is.False);

            lag.Enabled = false;

            Assert.That(lag.Incoming.IsNone, Is.True, "выключили — значения остались, но не действуют");
            Assert.That(lag.IncomingDelayMs, Is.EqualTo(200));
        }

        [Test]
        public void Values_ClampedToLimits()
        {
            Assert.That(ClientLimits.LagSimulationMaxDelayMs, Is.EqualTo(1000));
            Assert.That(ClientLimits.LagSimulationMaxJitterMs, Is.EqualTo(500));
            var lag = new LagSimulation
            {
                IncomingDelayMs = -5,
                IncomingJitterMs = -1,
                OutgoingDelayMs = 5000,
                OutgoingJitterMs = 900
            };

            Assert.That(lag.IncomingDelayMs, Is.EqualTo(0));
            Assert.That(lag.IncomingJitterMs, Is.EqualTo(0));
            Assert.That(lag.OutgoingDelayMs, Is.EqualTo(1000));
            Assert.That(lag.OutgoingJitterMs, Is.EqualTo(500));
        }

        [Test]
        public void Sample_NoJitter_ExactDelay()
        {
            var settings = new LagSettings(120, 0);

            Assert.That(settings.SampleMs(new StepRandom(999)), Is.EqualTo(120), "без разброса случайное число не берётся");
        }

        [Test]
        public void Sample_Jitter_FromMinusToPlus()
        {
            var settings = new LagSettings(100, 30);
            var random = new StepRandom(-30, 0, 30);

            Assert.That(settings.SampleMs(random), Is.EqualTo(70));
            Assert.That(settings.SampleMs(random), Is.EqualTo(100));
            Assert.That(settings.SampleMs(random), Is.EqualTo(130));
        }

        [Test]
        public void Sample_NeverBelowZero()
        {
            var settings = new LagSettings(10, 30);

            Assert.That(settings.SampleMs(new StepRandom(-30)), Is.EqualTo(0));
        }

        [Test]
        public void Sample_RealRandom_StaysInRange_ReachesBothEnds()
        {
            var settings = new LagSettings(100, 30);
            var random = new Random(12345);
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int i = 0; i < 10000; i++)
            {
                int sample = settings.SampleMs(random);
                min = Math.Min(min, sample);
                max = Math.Max(max, sample);
            }

            Assert.That(min, Is.EqualTo(70));
            Assert.That(max, Is.EqualTo(130));
        }

        [Test]
        public void IncomingSpike_TriggeredAndTakenOnce_ClampedToLimits()
        {
            var lag = new LagSimulation();
            Assert.That(lag.TakeIncomingSpikeMs(), Is.EqualTo(0));

            lag.TriggerIncomingSpike(500);
            Assert.That(lag.TakeIncomingSpikeMs(), Is.EqualTo(500));
            Assert.That(lag.TakeIncomingSpikeMs(), Is.EqualTo(0), "всплеск забирается один раз");

            lag.TriggerIncomingSpike(-10);
            Assert.That(lag.TakeIncomingSpikeMs(), Is.EqualTo(0));

            lag.TriggerIncomingSpike(5000);
            Assert.That(lag.TakeIncomingSpikeMs(), Is.EqualTo(ClientLimits.LagSimulationMaxDelayMs));
        }

        [Test]
        public void IncomingSpike_MultipleBeforeTake_KeepsLargest()
        {
            var lag = new LagSimulation();
            lag.TriggerIncomingSpike(200);
            lag.TriggerIncomingSpike(500);
            lag.TriggerIncomingSpike(150);

            Assert.That(lag.TakeIncomingSpikeMs(), Is.EqualTo(500));
        }
    }
}
