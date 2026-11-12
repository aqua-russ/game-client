using System;
using TheVerge.Network.Framing;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Поля строки пакета после времени (<see cref="PacketLogFormat"/>): направление, номер, подкоманда, имя, длина,
    /// пометки, байты; пароль — звёздочками (<see cref="PacketLogSecrets"/>).
    /// </summary>
    internal static class PacketLogPacketLine
    {
        public static void Append(PacketLogLine line, PacketDirection direction, ReadOnlySpan<byte> packet, bool sent)
        {
            line.AppendField(PacketLogFormat.DirectionText(direction));
            line.AppendHexNumber(packet[0], 1);
            line.Append(PacketLogFormat.Separator);
            AppendSubCommand(line, direction, packet);
            line.AppendField(PacketLogFormat.Name(direction, packet));
            line.AppendNumber(packet.Length);
            line.Append(PacketLogFormat.Separator);
            var masked = PacketLogSecrets.TryGetMasked(direction, packet, out var maskStart, out var maskLength);
            AppendFlags(line, masked, sent);
            line.AppendBytes(packet, maskStart, maskLength);
        }

        private static void AppendSubCommand(PacketLogLine line, PacketDirection direction, ReadOnlySpan<byte> packet)
        {
            if (!PacketLogFormat.TryReadSubCommand(direction, packet, out var subCommand, out var size))
            {
                line.AppendField(PacketLogFormat.Empty);
                return;
            }

            line.AppendHexNumber(subCommand, size);
            line.Append(PacketLogFormat.Separator);
        }

        private static void AppendFlags(PacketLogLine line, bool masked, bool sent)
        {
            if (!masked && sent)
            {
                line.AppendField(PacketLogFormat.Empty);
                return;
            }

            if (masked)
            {
                line.Append(PacketLogFormat.Masked);
            }

            if (!sent)
            {
                if (masked)
                {
                    line.Append(PacketLogFormat.FlagSeparator);
                }

                line.Append(PacketLogFormat.Unsent);
            }

            line.Append(PacketLogFormat.Separator);
        }
    }
}
