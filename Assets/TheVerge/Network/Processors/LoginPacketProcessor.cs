using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheVerge.Core;

namespace TheVerge.Network.Processors
{
    public sealed class LoginPacketProcessor
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Action<byte[]> _sendRawAction;

        public string AuthStageDescription { get; private set; } = "Ожидание подключения...";

        public event Action<CharacterListDto> OnCharacterListReceived;
        public event Action<string> OnCharacterDeleteFailed;
        public event Action<PlayerLoginConfirmDto> OnPlayerEnteredWorld;
        public event Action<string> OnSessionError;

        public LoginPacketProcessor(IMainThreadDispatcher dispatcher, Action<byte[]> sendRawAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _sendRawAction = sendRawAction ?? throw new ArgumentNullException(nameof(sendRawAction));
        }

        public async Task<(TcpClient client, NetworkStream stream)> RunAuthenticationAsync(
            TcpClient tcpClient,
            NetworkStream stream,
            string host,
            int port,
            string account,
            string password,
            bool isLiveServer,
            CancellationToken token)
        {
            byte[] buffer = new byte[4096];

            AuthStageDescription = "Авторизация: Отправка Seed 0xEF...";
            byte[] seed = new byte[21];
            seed[0] = 0xEF;
            seed[1] = 127; seed[2] = 0; seed[3] = 0; seed[4] = 1;
            NetByteConverter.WriteInt32(seed, 5, 7);
            NetByteConverter.WriteInt32(seed, 9, 0);
            NetByteConverter.WriteInt32(seed, 13, 95);
            NetByteConverter.WriteInt32(seed, 17, 0);
            await stream.WriteAsync(seed, 0, seed.Length, token);

            AuthStageDescription = "Авторизация: Отправка логина 0x80...";
            byte[] login = new byte[62];
            login[0] = 0x80;
            Encoding.ASCII.GetBytes(account.PadRight(30, '\0')).CopyTo(login, 1);
            Encoding.ASCII.GetBytes(password.PadRight(30, '\0')).CopyTo(login, 31);
            login[61] = 0x5D;
            await stream.WriteAsync(login, 0, login.Length, token);

            AuthStageDescription = "Авторизация: Ожидание серверов 0xA8...";
            int read = await stream.ReadAsync(buffer, 0, 3, token);
            if (read < 3 || buffer[0] != 0xA8) throw new InvalidOperationException("Сервер отклонил авторизацию");
            ushort a8Len = NetByteConverter.ReadUInt16(buffer, 1);
            int remainingA8 = a8Len - 3;
            while (remainingA8 > 0)
            {
                int r = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, remainingA8), token);
                if (r <= 0) break;
                remainingA8 -= r;
            }

            AuthStageDescription = "Авторизация: Выбор сервера 0xA0...";
            byte[] selectServer = new byte[3] { 0xA0, 0, 0 };
            await stream.WriteAsync(selectServer, 0, selectServer.Length, token);

            byte[] relayBuf = new byte[11];
            int relayRead = await ReadExactAsync(stream, relayBuf, 11, token);
            if (relayRead < 11 || relayBuf[0] != 0x8C) throw new InvalidOperationException("Ошибка релея сервера");
            uint authKey = NetByteConverter.ReadUInt32(relayBuf, 7);

            if (isLiveServer)
            {
                stream?.Dispose();
                tcpClient?.Dispose();
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(host, port);
                stream = tcpClient.GetStream();
            }

            AuthStageDescription = "Вход в мир: Отправка AuthKey + 0x91...";
            byte[] gameLogin = new byte[69];
            NetByteConverter.WriteUInt32(gameLogin, 0, authKey);
            gameLogin[4] = 0x91;
            NetByteConverter.WriteUInt32(gameLogin, 5, authKey);
            Encoding.ASCII.GetBytes(account.PadRight(30, '\0')).CopyTo(gameLogin, 9);
            Encoding.ASCII.GetBytes(password.PadRight(30, '\0')).CopyTo(gameLogin, 39);
            await stream.WriteAsync(gameLogin, 0, gameLogin.Length, token);

            AuthStageDescription = "Ожидание списка персонажей 0xA9...";
            return (tcpClient, stream);
        }

        public void ParseCharacterList(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 4) return;
            byte count = packet[3];
            var slots = new List<CharacterSlotDto>();
            int offset = 4;
            for (int i = 0; i < count && offset + 30 <= packet.Length; i++)
            {
                int nameLen = 0;
                while (nameLen < 30 && packet[offset + nameLen] != 0) nameLen++;
                string name = nameLen > 0 ? Encoding.UTF8.GetString(packet.Slice(offset, nameLen)).Trim() : string.Empty;
                slots.Add(new CharacterSlotDto(i, name));
                offset += 60;
            }
            var listDto = new CharacterListDto(slots);
            AuthStageDescription = $"Персонажей на аккаунте: {listDto.TotalSlots}";
            _dispatcher.Enqueue(() => OnCharacterListReceived?.Invoke(listDto));
        }

        public void SendSelectCharacter(int slotIndex, string charName = "Hero")
        {
            AuthStageDescription = $"Вход выбранным персонажем (слот {slotIndex}, {charName})...";
            byte[] playChar = new byte[73];
            playChar[0] = 0x5D;
            NetByteConverter.WriteUInt32(playChar, 1, 0xEDEDEDED);
            byte[] nameBytes = Encoding.UTF8.GetBytes(charName ?? "Hero");
            int writeLen = Math.Min(nameBytes.Length, 29);
            Array.Copy(nameBytes, 0, playChar, 5, writeLen);
            NetByteConverter.WriteInt32(playChar, 65, slotIndex);
            _sendRawAction(playChar);

            SendClientVersion();
        }

        public void SendCreateCharacter(CharacterCreationArgsDto args, int slotIndex = 0)
        {
            AuthStageDescription = $"Создание персонажа 0xF8 ({args.CharacterName})...";
            byte[] createPkt = CharacterCreationPacketBuilder.Build(args);
            NetByteConverter.WriteInt32(createPkt, 94, slotIndex);
            _sendRawAction(createPkt);

            SendClientVersion();
        }

        public void SendDeleteCharacter(int slotIndex, string password = "")
        {
            AuthStageDescription = $"Удаление персонажа (слот {slotIndex})...";
            byte[] deletePkt = new byte[39];
            deletePkt[0] = 0x83;
            Encoding.ASCII.GetBytes((password ?? string.Empty).PadRight(30, '\0')).CopyTo(deletePkt, 1);
            NetByteConverter.WriteInt32(deletePkt, 31, slotIndex);
            NetByteConverter.WriteUInt32(deletePkt, 35, 0);
            _sendRawAction(deletePkt);
        }

        public void ParseDeleteResult(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte res = packet[1];
            string error = res switch
            {
                0 => string.Empty,
                1 => "Неверный пароль",
                2 => "Персонаж не существует",
                3 => "Персонаж сейчас в игре",
                4 => "Персонаж создан слишком недавно",
                _ => $"Ошибка удаления (код {res})"
            };
            if (!string.IsNullOrEmpty(error))
            {
                AuthStageDescription = $"Ошибка удаления: {error}";
                _dispatcher.Enqueue(() => OnCharacterDeleteFailed?.Invoke(error));
            }
        }

        public void SendClientVersion()
        {
            byte[] verStr = Encoding.ASCII.GetBytes("7.0.95.0\0");
            byte[] verPkt = new byte[3 + verStr.Length];
            verPkt[0] = 0xBD;
            NetByteConverter.WriteUInt16(verPkt, 1, (ushort)verPkt.Length);
            verStr.CopyTo(verPkt, 3);
            _sendRawAction(verPkt);
        }

        public void ParseLoginConfirm(ReadOnlySpan<byte> packet, out Serial playerSerial, out Point3D startPos, out Direction startDir)
        {
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4));
            ushort body = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(9, 2));
            short x = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(11, 2));
            short y = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(13, 2));
            short z = BinaryPrimitives.ReadInt16BigEndian(packet.Slice(15, 2));
            var dir = (Direction)(packet[17] & 0x07);

            playerSerial = new Serial(serial);
            startPos = new Point3D(x, y, (sbyte)z);
            startDir = dir;

            AuthStageDescription = "🟢 В МИРЕ [0x1B LoginConfirm]";
            var confirmDto = new PlayerLoginConfirmDto(playerSerial, body, startPos, startDir);
            _dispatcher.Enqueue(() => OnPlayerEnteredWorld?.Invoke(confirmDto));
        }

        public void SetStageDescription(string desc) => AuthStageDescription = desc;

        private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int totalBytes, CancellationToken token)
        {
            int read = 0;
            while (read < totalBytes)
            {
                int r = await stream.ReadAsync(buffer, read, totalBytes - read, token);
                if (r <= 0) break;
                read += r;
            }
            return read;
        }
    }
}
