using System;
using System.Buffers.Binary;

namespace TheVerge.Network
{
    /// <summary>
    /// Таблица длин пакетов протокола Ultima Online / ModernUO (клиент 7.0.95.0).
    /// Позволяет безопасно нарезать непрерывный поток байтов TCP на дискретные пакеты.
    /// </summary>
    public static class PacketLengthTable
    {
        // -1 означает динамическую длину (длина читается из байтов [1..2] в Big-Endian)
        // 0 означает неизвестный или неподдерживаемый пакет
        private static readonly short[] Lengths = new short[256];

        static PacketLengthTable()
        {
            // Фиксированные пакеты сервера ModernUO (клиент 7.0.95.0)
            Lengths[0x0B] = 7;  // Damage
            Lengths[0x15] = 9;  // Follower
            Lengths[0x1B] = 37; // Login Confirm
            Lengths[0x1D] = 5;  // Delete Object
            Lengths[0x20] = 19; // Mobile Update
            Lengths[0x21] = 8;  // Move Reject
            Lengths[0x22] = 3;  // Move Ack
            Lengths[0x23] = 26; // Drag Item Effect
            Lengths[0x24] = 9;  // Open Container Gump
            Lengths[0x25] = 21; // Add Item to Container
            Lengths[0x27] = 2;  // Lift Reject
            Lengths[0x2C] = 2;  // Death Status
            Lengths[0x2D] = 17; // Mobile Attributes
            Lengths[0x2E] = 15; // Equip Item
            Lengths[0x2F] = 10; // Combat
            Lengths[0x38] = 7;  // Pathfinding
            Lengths[0x4E] = 6;  // Personal Light Level
            Lengths[0x4F] = 2;  // Overall Light Level
            Lengths[0x53] = 2;  // Warning Message
            Lengths[0x54] = 12; // Sound Effect
            Lengths[0x55] = 1;  // Login Complete
            Lengths[0x5B] = 4;  // Current Time
            Lengths[0x65] = 4;  // Set Weather
            Lengths[0x6C] = 19; // Target
            Lengths[0x6D] = 3;  // Play Music
            Lengths[0x6E] = 14; // Mobile Animation
            Lengths[0x70] = 28; // Graphical Effect
            Lengths[0x72] = 5;  // War Mode
            Lengths[0x73] = 2;  // Ping Ack
            Lengths[0x76] = 16; // New Subserver
            Lengths[0x77] = 17; // Mobile Moving
            Lengths[0x7B] = 2;  // Sequence
            Lengths[0x82] = 2;  // Login Denied
            Lengths[0x85] = 2;  // Delete Character Result
            Lengths[0x88] = 66; // Open Paperdoll
            Lengths[0x8C] = 11; // Relay to Game Server
            Lengths[0x95] = 9;  // Dye Window
            Lengths[0x97] = 2;  // Move Player
            Lengths[0x98] = 37; // All Names (3D)
            Lengths[0x99] = 30; // Target Multi
            Lengths[0xA1] = 9;  // Hits Update
            Lengths[0xA2] = 9;  // Mana Update
            Lengths[0xA3] = 9;  // Stamina Update
            Lengths[0xAA] = 5;  // Attack OK
            Lengths[0xAF] = 13; // Death Animation
            Lengths[0xB9] = 5;  // Supported Features (Extended: 5 байт)
            Lengths[0xBC] = 3;  // Season Change
            Lengths[0xC0] = 36; // Effect Hued
            Lengths[0xC2] = 21; // Unicode Prompt
            Lengths[0xC6] = 1;  // Invalid Map
            Lengths[0xC7] = 49; // Particle Effect
            Lengths[0xC8] = 2;  // Change Update Range
            Lengths[0xD1] = 2;  // Logout Ack
            Lengths[0xDC] = 9;  // Object Hash / Buff
            Lengths[0xE2] = 10; // Mobile Animation New
            Lengths[0xF2] = 25; // Map / Move
            Lengths[0xF3] = 26; // World Item Object

            // Динамические пакеты сервера (длина в байтах [1..2])
            Lengths[0x11] = -1; // Status info
            Lengths[0x17] = -1; // Health bar status
            Lengths[0x1A] = -1; // World Item
            Lengths[0x1C] = -1; // ASCII Speech
            Lengths[0x3A] = -1; // Skills
            Lengths[0x3B] = -1; // Vendor Buy/Sell
            Lengths[0x3C] = -1; // Container Content
            Lengths[0x66] = -1; // Book Page Content
            Lengths[0x6F] = -1; // Secure Trade
            Lengths[0x71] = -1; // Bulletin Board
            Lengths[0x74] = -1; // Vendor Buy List
            Lengths[0x78] = -1; // Mobile Incoming (включает список экипировки)
            Lengths[0x7C] = -1; // Open Menu
            Lengths[0x81] = -1; // Character List
            Lengths[0x86] = -1; // Resend Character List
            Lengths[0x9E] = -1; // Vendor Sell List
            Lengths[0xA5] = -1; // Open Web Browser
            Lengths[0xA6] = -1; // Tip Window
            Lengths[0xA8] = -1; // Server List
            Lengths[0xA9] = -1; // Character List
            Lengths[0xAE] = -1; // Unicode Speech
            Lengths[0xB0] = -1; // Send Gump Dialog
            Lengths[0xB7] = -1; // Help / Tip
            Lengths[0xB8] = -1; // Quest / Profile
            Lengths[0xBA] = 10; // Quest Arrow
            Lengths[0xBD] = -1; // Client Version Request
            Lengths[0xBF] = -1; // General Information Packet (Command Extensions)
            Lengths[0xC1] = -1; // Cliloc Message
            Lengths[0xCC] = -1; // Cliloc Message Affix
            Lengths[0xD2] = 25; // Extended 0x20 Mobile Update
            Lengths[0xD3] = -1; // Extended 0x78 Mobile Incoming
            Lengths[0xD4] = -1; // Book Header
            Lengths[0xD6] = -1; // MegaCliloc Object Property List
            Lengths[0xDD] = -1; // Compressed Gump Dialog
            Lengths[0xDF] = -1; // Buff / Status
            Lengths[0xE3] = -1; // KR Encryption Response
            Lengths[0xF0] = -1; // Krrios Client Special
        }

        /// <summary>
        /// Вычисляет длину пакета в байтах.
        /// Возвращает false, если это динамический пакет, но в буфере пока меньше 3 байт.
        /// </summary>
        public static bool TryGetPacketLength(ReadOnlySpan<byte> buffer, out int packetLength)
        {
            packetLength = 0;
            if (buffer.IsEmpty) return false;

            byte packetId = buffer[0];
            short def = Lengths[packetId];

            if (def > 0)
            {
                packetLength = def;
                return true;
            }

            if (def == -1)
            {
                if (buffer.Length < 3)
                {
                    // Нужно подождать прихода хотя бы 3 байтов для чтения размера
                    return false;
                }

                ushort dynLen = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(1, 2));
                packetLength = dynLen;
                return true;
            }

            // Неизвестный пакет
            Console.WriteLine($"[Network] Unknown packet ID 0x{packetId:X2} encountered in network stream.");
            if (buffer.Length >= 3)
            {
                ushort possibleDyn = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(1, 2));
                if (possibleDyn >= 3 && possibleDyn <= 4096)
                {
                    packetLength = possibleDyn;
                    return true;
                }
            }

            packetLength = 1;
            return true;
        }
    }

    /// <summary>
    /// Прямая сериализация и десериализация чисел в Big-Endian (Network Byte Order)
    /// для массивов byte[] без аллокаций и без использования Span в асинхронных методах (CS4012 guard).
    /// </summary>
    public static class NetByteConverter
    {
        public static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        public static void WriteInt16(byte[] buffer, int offset, short value)
        {
            WriteUInt16(buffer, offset, (ushort)value);
        }

        public static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        public static void WriteInt32(byte[] buffer, int offset, int value)
        {
            WriteUInt32(buffer, offset, (uint)value);
        }

        public static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
        }

        public static short ReadInt16(byte[] buffer, int offset)
        {
            return (short)ReadUInt16(buffer, offset);
        }

        public static uint ReadUInt32(byte[] buffer, int offset)
        {
            return ((uint)buffer[offset] << 24) |
                   ((uint)buffer[offset + 1] << 16) |
                   ((uint)buffer[offset + 2] << 8) |
                   buffer[offset + 3];
        }

        public static int ReadInt32(byte[] buffer, int offset)
        {
            return (int)ReadUInt32(buffer, offset);
        }
    }
}
