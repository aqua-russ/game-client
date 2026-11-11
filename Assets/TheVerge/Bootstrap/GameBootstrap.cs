using UnityEngine;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Точка входа сцены Bootstrap.unity: здесь объекты клиента связываются конструкторами (AGENTS.md §2.1).
    /// Старая цепочка входа прототипа удалена в задаче 0.9 (ADR 0011); вход в игру появится в задаче 1.16
    /// (docs/PLAN.md, Стадия 1). Имя класса и файл не меняются: на них ссылается сцена.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[The Verge] Клиент запущен. Окно входа появится в задаче 1.16 (docs/PLAN.md, Стадия 1).");
        }
    }
}
