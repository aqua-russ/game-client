using TheVerge.Network.IO;

namespace TheVerge.Network.Session
{
    /// <summary>
    /// Обработчик входящего пакета. Читатель стоит после заголовка: номера, длины (у пакета переменной длины) и
    /// подкоманды (если обработчик зарегистрирован на подкоманду). Читатель — ref struct: ссылку на буфер пакета
    /// из пула сохранить нельзя, буфер возвращается в пул сразу после обработчика.
    /// </summary>
    public delegate void PacketHandler(ref PacketReader reader);
}
