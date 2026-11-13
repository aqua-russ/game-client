using System;
using System.Collections.Generic;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>Список шардов: кнопка на шард (даже если шард один), «Назад» — отмена входа.</summary>
    public sealed class ShardsPanelView : IDisposable
    {
        private readonly LoginScreens _screens;
        private readonly ButtonList _list;
        private readonly Button _back;
        private IReadOnlyList<ShardEntry> _shown;

        public ShardsPanelView(VisualElement root, LoginScreens screens)
        {
            _screens = screens;
            Panel = UiElements.Require<VisualElement>(root, "shardsPanel");
            _list = new ButtonList(UiElements.Require<ScrollView>(Panel, "shardsList"), screens.ChooseShard);
            _back = UiElements.Require<Button>(Panel, "shardsBack");
            _back.clicked += _screens.Cancel;
        }

        public VisualElement Panel { get; }

        public void Refresh()
        {
            var shards = _screens.Shards;
            if (ReferenceEquals(shards, _shown))
            {
                return;
            }

            _shown = shards;
            _list.Rebuild(shards.Count, i => shards[i].Name + " — заполнен на " + shards[i].FullPercent + "%", _ => false);
        }

        public void Dispose()
        {
            _back.clicked -= _screens.Cancel;
            _list.Dispose();
        }
    }
}
