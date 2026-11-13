using TheVerge.Core.Items;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>
    /// 0x3C Container Content: полный видимый список содержимого контейнера. Сервер: Projects/Server/Network/Packets/
    /// OutgoingContainerPackets.cs, SendContainerContent — ushort число, затем по <see cref="ContainerEntry.Length"/> байт на
    /// предмет. Длина, не совпадающая с числом, — ошибка пакета до изменений мира. Книга заклинаний в UOR — тоже 0x3C
    /// (SendOldSpellbookContent: Serial 0x7FFFFFFF − i, количество — номер заклинания).
    /// </summary>
    public sealed class ContainerContentHandler
    {
        public const byte PacketId = 0x3C;

        private readonly ContainerContents _contents;

        public ContainerContentHandler(ContainerContents contents)
        {
            _contents = contents;
        }

        public void Handle(ref PacketReader reader)
        {
            var count = reader.ReadUInt16();
            if (reader.Remaining != count * ContainerEntry.Length)
            {
                throw new PacketOutOfRangeException("0x3C: предметов " + count + ", а байт " + reader.Remaining + ".");
            }

            if (count == 0)
            {
                _contents.BeginContent(Serial.Zero);
                _contents.EndContent();
                return;
            }

            var current = Serial.Zero;
            for (var i = 0; i < count; i++)
            {
                var entry = ContainerEntry.Read(ref reader);
                if (entry.Container != current)
                {
                    EndIfStarted(current);
                    current = entry.Container;
                    _contents.BeginContent(current);
                }

                _contents.AddContent(entry.Serial, entry.Info, entry.Container, entry.GridIndex);
            }

            EndIfStarted(current);
        }

        private void EndIfStarted(Serial current)
        {
            if (current.IsValid)
            {
                _contents.EndContent();
            }
        }
    }
}
