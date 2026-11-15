using System;
using TheVerge.Presentation.UI.Diagnostics;
using TheVerge.Presentation.UI.Movement;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI
{
    /// <summary>
    /// Панели поверх мира из одной разметки (UI/Session/LoginScreens.uxml и вставленные в неё шаблоны): индикатор
    /// «Бег / Шаг», экран отладки движения и панель «Симуляция пинга».
    /// </summary>
    public sealed class WorldPanels : IDisposable
    {
        public WorldPanels(VisualElement root)
        {
            RunIndicator = new RunIndicatorView(root);
            MovementDebug = new MovementDebugView(root);
            LagSimulation = new LagSimulationView(root);
        }

        public RunIndicatorView RunIndicator { get; }

        public MovementDebugView MovementDebug { get; }

        public LagSimulationView LagSimulation { get; }

        public void Dispose()
        {
            MovementDebug.Dispose();
            LagSimulation.Dispose();
        }
    }
}
