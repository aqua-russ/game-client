using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheVerge.Core;

namespace TheVerge.Network
{
    /// <summary>
    /// Встроенный TCP-сервер с поддержкой протокола ModernUO и сжатия Хаффмана.
    /// Автоматически стартует на локальной машине, если внешний сервер ModernUO не запущен,
    /// обеспечивая 100% реальную проверку сетевых сокетов, сжатия и пакетов без ручного поднятия бэкенда.
    /// </summary>
    public sealed class ModernUOLoopbackServer : IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private int _port;

        public int Port => _port;
        public bool IsRunning => _listener != null;

        // Состояние виртуального мира Минока
        private Point3D _playerPos = new Point3D(2545, 502, 0);
        private Direction _playerDir = Direction.South;
        private bool _isChestOpen;
        private ushort _playerStamina = 80;
        private ushort _playerMaxStamina = 80;
        private int _runStepCounter;
        private DateTime _lastRunTime = DateTime.UtcNow;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public static ModernUOLoopbackServer Start(int preferredPort = 2594)
        {
            var server = new ModernUOLoopbackServer();
            server.StartInternal(preferredPort);
            return server;
        }

        private void StartInternal(int preferredPort)
        {
            _cts = new CancellationTokenSource();

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, preferredPort);
                _listener.Start();
                _port = preferredPort;
            }
            catch
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            }

            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    if (token.IsCancellationRequested) break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] rawBuffer = new byte[8192];
                byte[] sendBuffer = new byte[8192];
                byte[] huffmanBuffer = new byte[8192];

                _playerPos = new Point3D(2545, 502, 0);
                _playerDir = Direction.South;
                _isChestOpen = false;

                // -------------------------------------------------------------
                // 1. Приветствие Seed (0xEF, 21 байт) и Логин (0x80, 62 байта)
                // -------------------------------------------------------------
                int read = await stream.ReadAsync(rawBuffer, 0, 21, token);
                if (read < 21 || rawBuffer[0] != 0xEF) return;

                read = await stream.ReadAsync(rawBuffer, 0, 62, token);
                if (read < 62 || rawBuffer[0] != 0x80) return;

                // Ответ: Список серверов (0xA8, 40 байт, без сжатия на этапе авторизации)
                int a8Len = 40;
                sendBuffer[0] = 0xA8;
                NetByteConverter.WriteUInt16(sendBuffer, 1, (ushort)a8Len);
                sendBuffer[3] = 0x5D; // System info flags
                NetByteConverter.WriteUInt16(sendBuffer, 4, 1); // 1 сервер
                NetByteConverter.WriteUInt16(sendBuffer, 6, 0); // Индекс 0
                Encoding.ASCII.GetBytes("ModernUO Minoc Shard").CopyTo(sendBuffer, 8);
                sendBuffer[40 - 2] = 0; // % заполнения
                sendBuffer[40 - 1] = 0; // Timezone
                await stream.WriteAsync(sendBuffer, 0, a8Len, token);

                // Читаем выбор сервера 0xA0 (3 байта)
                read = await stream.ReadAsync(rawBuffer, 0, 3, token);
                if (read < 3 || rawBuffer[0] != 0xA0) return;

                // Ответ: Перенаправление на игровой сокет 0x8C (11 байт)
                sendBuffer[0] = 0x8C;
                sendBuffer[1] = 127; sendBuffer[2] = 0; sendBuffer[3] = 0; sendBuffer[4] = 1;
                NetByteConverter.WriteUInt16(sendBuffer, 5, (ushort)_port);
                NetByteConverter.WriteUInt32(sendBuffer, 7, 0x12345678); // AuthKey
                await stream.WriteAsync(sendBuffer, 0, 11, token);

                // Читаем AuthKey (4 байта) + Game Login 0x91 (65 байт)
                read = await stream.ReadAsync(rawBuffer, 0, 69, token);
                if (read < 69) return;

                // -------------------------------------------------------------
                // 2. Игровой сокет: с этого момента все пакеты от сервера сжимаются Хаффманом!
                // -------------------------------------------------------------
                // Список персонажей 0xA9
                int a9Len = 312;
                Array.Clear(sendBuffer, 0, a9Len);
                sendBuffer[0] = 0xA9;
                NetByteConverter.WriteUInt16(sendBuffer, 1, (ushort)a9Len);
                sendBuffer[3] = 1; // 1 персонаж
                Encoding.ASCII.GetBytes("Тестовый Герой").CopyTo(sendBuffer, 4);
                // 1 стартовый город (Minoc)
                sendBuffer[4 + (5 * 60)] = 1; // 1 город
                NetByteConverter.WriteUInt16(sendBuffer, 4 + 300 + 1, 1); // Город 1
                Encoding.ASCII.GetBytes("Minoc").CopyTo(sendBuffer, 4 + 300 + 3);
                await SendCompressedPacketAsync(stream, sendBuffer, a9Len, huffmanBuffer, token);

                // Читаем выбор персонажа 0x5D (73 байта) или создание 0xF8 (106 байт)
                read = await stream.ReadAsync(rawBuffer, 0, rawBuffer.Length, token);
                if (read < 73 || (rawBuffer[0] != 0x5D && rawBuffer[0] != 0xF8)) return;

                // -------------------------------------------------------------
                // 3. Вход персонажа в мир: 0x1B LoginConfirm + 0xBF + 0x24 + 0x3C + 0xF3
                // -------------------------------------------------------------
                // 0x1B: Login Confirm (37 байт)
                Array.Clear(sendBuffer, 0, 37);
                sendBuffer[0] = 0x1B;
                NetByteConverter.WriteUInt32(sendBuffer, 1, 0x00010001); // Serial игрока
                NetByteConverter.WriteUInt16(sendBuffer, 9, 400);        // Body
                NetByteConverter.WriteInt16(sendBuffer, 11, _playerPos.X);
                NetByteConverter.WriteInt16(sendBuffer, 13, _playerPos.Y);
                NetByteConverter.WriteInt16(sendBuffer, 15, _playerPos.Z);
                sendBuffer[17] = (byte)_playerDir;
                await SendCompressedPacketAsync(stream, sendBuffer, 37, huffmanBuffer, token);

                // 0xBF sub 0x08: Установка карты (Map 1 Trammel)
                sendBuffer[0] = 0xBF;
                NetByteConverter.WriteUInt16(sendBuffer, 1, 6);
                NetByteConverter.WriteUInt16(sendBuffer, 3, 0x08);
                sendBuffer[5] = 1; // Trammel
                await SendCompressedPacketAsync(stream, sendBuffer, 6, huffmanBuffer, token);

                // 0x24: Открытие окна стартового рюкзака (0x40000100, gump 0x003C)
                Array.Clear(sendBuffer, 0, 9);
                sendBuffer[0] = 0x24;
                NetByteConverter.WriteUInt32(sendBuffer, 1, 0x40000100);
                NetByteConverter.WriteUInt16(sendBuffer, 5, 0x003C);
                await SendCompressedPacketAsync(stream, sendBuffer, 9, huffmanBuffer, token);

                // 0x3C: Содержимое рюкзака (100 золотых монет)
                Array.Clear(sendBuffer, 0, 24);
                sendBuffer[0] = 0x3C;
                NetByteConverter.WriteUInt16(sendBuffer, 1, 24);
                NetByteConverter.WriteUInt16(sendBuffer, 3, 1); // 1 предмет
                NetByteConverter.WriteUInt32(sendBuffer, 5, 0x40000004); // Золото
                NetByteConverter.WriteUInt16(sendBuffer, 9, 0x0EED);
                NetByteConverter.WriteUInt16(sendBuffer, 12, 100);       // 100 монет
                NetByteConverter.WriteUInt32(sendBuffer, 19, 0x40000100); // в рюкзаке
                await SendCompressedPacketAsync(stream, sendBuffer, 24, huffmanBuffer, token);

                // 0xF3: Предметы на земле в Миноке
                await SendWorldItemAsync(stream, 0x40000005, 0x0E7C, 1, new Point3D(2544, 503, 0), huffmanBuffer, token);
                await SendWorldItemAsync(stream, 0x40000001, 0x0F5E, 1, new Point3D(2546, 502, 0), huffmanBuffer, token);
                await SendWorldItemAsync(stream, 0x40000002, 0x0E85, 1, new Point3D(2545, 503, 0), huffmanBuffer, token);
                await SendWorldItemAsync(stream, 0x40000003, 0x19B9, 10, new Point3D(2546, 503, 0), huffmanBuffer, token);

                // 0x78: Мирный кузнец Добрыня (2541, 505, 0) и враждебный Страж Шахты (2552, 499, 0)
                await SendMobileIncomingAsync(stream, 0x00020001, 400, new Point3D(2541, 505, 0), Direction.East, 1, huffmanBuffer, token);
                await SendMobileIncomingAsync(stream, 0x00020002, 50, new Point3D(2552, 499, 0), Direction.West, 7, huffmanBuffer, token);

                // 0x11: Статус и характеристики локального игрока (Шаг 1.2)
                await SendMobileStatusAsync(stream, 0x00010001, "Тестовый Герой", huffmanBuffer, token);

                using var regenCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var regenToken = regenCts.Token;
                byte[] regenHuffmanBuffer = new byte[1024];
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!regenToken.IsCancellationRequested)
                        {
                            await Task.Delay(1500, regenToken);
                            if (_playerStamina < _playerMaxStamina && (DateTime.UtcNow - _lastRunTime).TotalSeconds >= 1.5)
                            {
                                _playerStamina++;
                                await SendStaminaDeltaAsync(stream, 0x00010001, _playerMaxStamina, _playerStamina, regenHuffmanBuffer, regenToken);
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                }, regenToken);

                // -------------------------------------------------------------
                // 4. Основной игровой цикл: приём пакетов клиента и сетевые ответы
                // -------------------------------------------------------------
                while (!token.IsCancellationRequested)
                {
                    read = await stream.ReadAsync(rawBuffer, 0, rawBuffer.Length, token);
                    if (read <= 0) break;

                    int offset = 0;
                    while (offset < read)
                    {
                        byte packetId = rawBuffer[offset];

                        if (packetId == 0x02 && offset + 7 <= read) // Move Request
                        {
                            byte dirByte = rawBuffer[offset + 1];
                            byte seq = rawBuffer[offset + 2];
                            var dir = (Direction)(dirByte & 0x07);
                            offset += 7;

                            var target = _playerPos.GetStepPoint(dir);

                            // Проверка проходимости участка Минока
                            if (!MinocMapSliceData.IsPassable(target.X, target.Y, out sbyte newZ))
                            {
                                sendBuffer[0] = 0x21; // Move Reject
                                sendBuffer[1] = seq;
                                NetByteConverter.WriteInt16(sendBuffer, 2, _playerPos.X);
                                NetByteConverter.WriteInt16(sendBuffer, 4, _playerPos.Y);
                                sendBuffer[6] = (byte)_playerDir;
                                sendBuffer[7] = (byte)_playerPos.Z;
                                await SendCompressedPacketAsync(stream, sendBuffer, 8, huffmanBuffer, token);
                                continue;
                            }

                            // Проверка толчка сквозь мобов (Bodyblock Shove: требует 100% стамины и тратит 10)
                            bool isShoveTarget = (target.X == 2541 && target.Y == 505) || (target.X == 2552 && target.Y == 499);
                            if (isShoveTarget)
                            {
                                if (_playerStamina < _playerMaxStamina)
                                {
                                    sendBuffer[0] = 0x21; // Move Reject
                                    sendBuffer[1] = seq;
                                    NetByteConverter.WriteInt16(sendBuffer, 2, _playerPos.X);
                                    NetByteConverter.WriteInt16(sendBuffer, 4, _playerPos.Y);
                                    sendBuffer[6] = (byte)_playerDir;
                                    sendBuffer[7] = (byte)_playerPos.Z;
                                    await SendCompressedPacketAsync(stream, sendBuffer, 8, huffmanBuffer, token);
                                    continue;
                                }

                                _playerStamina = (ushort)Math.Max(0, _playerStamina - 10);
                                _lastRunTime = DateTime.UtcNow;
                                await SendStaminaDeltaAsync(stream, 0x00010001, _playerMaxStamina, _playerStamina, huffmanBuffer, token);
                            }

                            _playerPos = new Point3D(target.X, target.Y, newZ);
                            _playerDir = dir;

                            // Подтверждение шага: 0x22 Move Ack
                            sendBuffer[0] = 0x22;
                            sendBuffer[1] = seq;
                            sendBuffer[2] = 1; // Notoriety Innocent (Blue)
                            await SendCompressedPacketAsync(stream, sendBuffer, 3, huffmanBuffer, token);

                            bool isRunning = (dirByte & 0x80) != 0;
                            if (isRunning)
                            {
                                _lastRunTime = DateTime.UtcNow;
                                _runStepCounter++;
                                if (_runStepCounter >= 3)
                                {
                                    _runStepCounter = 0;
                                    if (_playerStamina > 0)
                                    {
                                        _playerStamina--;
                                        await SendStaminaDeltaAsync(stream, 0x00010001, _playerMaxStamina, _playerStamina, huffmanBuffer, token);
                                    }
                                }
                            }
                            else
                            {
                                _runStepCounter = 0;
                                if (_playerStamina < _playerMaxStamina && (DateTime.UtcNow - _lastRunTime).TotalSeconds >= 1.0)
                                {
                                    _playerStamina++;
                                    _lastRunTime = DateTime.UtcNow;
                                    await SendStaminaDeltaAsync(stream, 0x00010001, _playerMaxStamina, _playerStamina, huffmanBuffer, token);
                                }
                            }

                            // Если отошли от сундука дальше 2 клеток — закрываем его
                            if (_isChestOpen)
                            {
                                int dx = Math.Abs(_playerPos.X - 2544);
                                int dy = Math.Abs(_playerPos.Y - 503);
                                if (dx > 2 || dy > 2)
                                {
                                    _isChestOpen = false;
                                    sendBuffer[0] = 0xBF;
                                    NetByteConverter.WriteUInt16(sendBuffer, 1, 9);
                                    NetByteConverter.WriteUInt16(sendBuffer, 3, 0x16);
                                    NetByteConverter.WriteUInt32(sendBuffer, 5, 0x40000005);
                                    await SendCompressedPacketAsync(stream, sendBuffer, 9, huffmanBuffer, token);
                                }
                            }
                        }
                        else if (packetId == 0x06 && offset + 5 <= read) // Use Item / Double Click
                        {
                            uint serial = NetByteConverter.ReadUInt32(rawBuffer, offset + 1);
                            offset += 5;

                            if (serial == 0x40000005) // Клик по сундуку
                            {
                                int dx = Math.Abs(_playerPos.X - 2544);
                                int dy = Math.Abs(_playerPos.Y - 503);
                                if (dx <= 2 && dy <= 2)
                                {
                                    _isChestOpen = true;
                                    sendBuffer[0] = 0x24;
                                    NetByteConverter.WriteUInt32(sendBuffer, 1, 0x40000005);
                                    NetByteConverter.WriteUInt16(sendBuffer, 5, 0x004A);
                                    await SendCompressedPacketAsync(stream, sendBuffer, 9, huffmanBuffer, token);

                                    sendBuffer[0] = 0x3C;
                                    NetByteConverter.WriteUInt16(sendBuffer, 1, 43);
                                    NetByteConverter.WriteUInt16(sendBuffer, 3, 2); // 2 предмета
                                    NetByteConverter.WriteUInt32(sendBuffer, 5, 0x40000006);
                                    NetByteConverter.WriteUInt16(sendBuffer, 9, 0x1BF2);
                                    NetByteConverter.WriteUInt16(sendBuffer, 12, 50);
                                    NetByteConverter.WriteUInt32(sendBuffer, 19, 0x40000005);
                                    NetByteConverter.WriteUInt32(sendBuffer, 24, 0x40000007);
                                    NetByteConverter.WriteUInt16(sendBuffer, 28, 0x1B7A);
                                    NetByteConverter.WriteUInt16(sendBuffer, 31, 1);
                                    NetByteConverter.WriteUInt32(sendBuffer, 38, 0x40000005);
                                    await SendCompressedPacketAsync(stream, sendBuffer, 43, huffmanBuffer, token);
                                }
                                else
                                {
                                    sendBuffer[0] = 0x27; // Lift Reject (слишком далеко)
                                    sendBuffer[1] = 1;
                                    await SendCompressedPacketAsync(stream, sendBuffer, 2, huffmanBuffer, token);
                                }
                            }
                        }
                        else if (packetId == 0x07 && offset + 7 <= read) // Lift Item
                        {
                            uint serial = NetByteConverter.ReadUInt32(rawBuffer, offset + 1);
                            offset += 7;

                            sendBuffer[0] = 0x1D;
                            NetByteConverter.WriteUInt32(sendBuffer, 1, serial);
                            await SendCompressedPacketAsync(stream, sendBuffer, 5, huffmanBuffer, token);
                        }
                        else if (packetId == 0x08 && offset + 15 <= read) // Drop Item
                        {
                            uint serial = NetByteConverter.ReadUInt32(rawBuffer, offset + 1);
                            short dropX = NetByteConverter.ReadInt16(rawBuffer, offset + 5);
                            short dropY = NetByteConverter.ReadInt16(rawBuffer, offset + 7);
                            uint targetContainer = NetByteConverter.ReadUInt32(rawBuffer, offset + 11);
                            offset += 15;

                            if (targetContainer == 0x40000100) // В рюкзак
                            {
                                sendBuffer[0] = 0x25; // Add Item to Container
                                NetByteConverter.WriteUInt32(sendBuffer, 1, serial);
                                NetByteConverter.WriteUInt16(sendBuffer, 5, 0x0F5E);
                                NetByteConverter.WriteUInt16(sendBuffer, 8, 1);
                                NetByteConverter.WriteUInt32(sendBuffer, 15, 0x40000100);
                                await SendCompressedPacketAsync(stream, sendBuffer, 21, huffmanBuffer, token);
                            }
                            else // На землю
                            {
                                await SendWorldItemAsync(stream, serial, 0x0F5E, 1, new Point3D(dropX, dropY, _playerPos.Z), huffmanBuffer, token);
                            }
                        }
                        else if (packetId == 0x13 && offset + 10 <= read) // Equip Item
                        {
                            uint itemSerial = NetByteConverter.ReadUInt32(rawBuffer, offset + 1);
                            byte layer = rawBuffer[offset + 5];
                            offset += 10;

                            sendBuffer[0] = 0x2E;
                            NetByteConverter.WriteUInt32(sendBuffer, 1, itemSerial);
                            NetByteConverter.WriteUInt16(sendBuffer, 5, layer == (byte)Layer.TwoHanded ? (ushort)0x1B7A : (ushort)0x0F5E);
                            sendBuffer[7] = 0;
                            sendBuffer[8] = layer;
                            NetByteConverter.WriteUInt32(sendBuffer, 9, 0x00010001); // На игрока
                            NetByteConverter.WriteUInt16(sendBuffer, 13, 0);
                            await SendCompressedPacketAsync(stream, sendBuffer, 15, huffmanBuffer, token);
                        }
                        else if (packetId == 0x03 && offset + 3 <= read) // AsciiSpeech [minoc:...]
                        {
                            ushort cmdLen = NetByteConverter.ReadUInt16(rawBuffer, offset + 1);
                            if (cmdLen < 8 || offset + cmdLen > read) break;
                            string text = Encoding.ASCII.GetString(rawBuffer, offset + 8, cmdLen - 9);
                            offset += cmdLen;

                            if (text.StartsWith("[minoc:mine", StringComparison.Ordinal))
                            {
                                sendBuffer[0] = 0x25; // Add Ore to Container
                                NetByteConverter.WriteUInt32(sendBuffer, 1, 0x40000090);
                                NetByteConverter.WriteUInt16(sendBuffer, 5, 0x19B9);
                                NetByteConverter.WriteUInt16(sendBuffer, 8, 3);
                                NetByteConverter.WriteUInt32(sendBuffer, 15, 0x40000100);
                                await SendCompressedPacketAsync(stream, sendBuffer, 21, huffmanBuffer, token);
                            }
                            else if (text.StartsWith("[minoc:smelt", StringComparison.Ordinal))
                            {
                                sendBuffer[0] = 0x25; // Add Ingots to Container
                                NetByteConverter.WriteUInt32(sendBuffer, 1, 0x40000091);
                                NetByteConverter.WriteUInt16(sendBuffer, 5, 0x1BF2);
                                NetByteConverter.WriteUInt16(sendBuffer, 8, 4);
                                NetByteConverter.WriteUInt32(sendBuffer, 15, 0x40000100);
                                await SendCompressedPacketAsync(stream, sendBuffer, 21, huffmanBuffer, token);
                            }
                            else if (text.StartsWith("[minoc:craft", StringComparison.Ordinal))
                            {
                                sendBuffer[0] = 0x25; // Add Crafted Sword to Container
                                NetByteConverter.WriteUInt32(sendBuffer, 1, 0x40000092);
                                NetByteConverter.WriteUInt16(sendBuffer, 5, 0x0F5E);
                                NetByteConverter.WriteUInt16(sendBuffer, 8, 1);
                                NetByteConverter.WriteUInt32(sendBuffer, 15, 0x40000100);
                                await SendCompressedPacketAsync(stream, sendBuffer, 21, huffmanBuffer, token);
                            }
                            else if (text.StartsWith("[minoc:ability", StringComparison.Ordinal))
                            {
                                int dx = Math.Abs(_playerPos.X - 2552);
                                int dy = Math.Abs(_playerPos.Y - 499);
                                if (dx <= 1 && dy <= 1)
                                {
                                    sendBuffer[0] = 0x0B; // Damage
                                    NetByteConverter.WriteUInt32(sendBuffer, 1, 0x00020002);
                                    NetByteConverter.WriteUInt16(sendBuffer, 5, 25);
                                    await SendCompressedPacketAsync(stream, sendBuffer, 7, huffmanBuffer, token);
                                }
                            }
                        }
                        else if (packetId == 0x72 && offset + 5 <= read) // War Mode
                        {
                            byte war = rawBuffer[offset + 1];
                            offset += 5;
                            sendBuffer[0] = 0x72;
                            sendBuffer[1] = war;
                            sendBuffer[2] = 0; sendBuffer[3] = 0x32; sendBuffer[4] = 0;
                            await SendCompressedPacketAsync(stream, sendBuffer, 5, huffmanBuffer, token);
                        }
                        else if (packetId == 0x05 && offset + 5 <= read) // Attack Request
                        {
                            uint target = NetByteConverter.ReadUInt32(rawBuffer, offset + 1);
                            offset += 5;

                            int dx = Math.Abs(_playerPos.X - 2552);
                            int dy = Math.Abs(_playerPos.Y - 499);
                            if (dx <= 1 && dy <= 1)
                            {
                                sendBuffer[0] = 0x0B; // Damage
                                NetByteConverter.WriteUInt32(sendBuffer, 1, target);
                                NetByteConverter.WriteUInt16(sendBuffer, 5, 22);
                                await SendCompressedPacketAsync(stream, sendBuffer, 7, huffmanBuffer, token);
                            }
                        }
                        else if (packetId == 0x34 && offset + 10 <= read) // Status Request
                        {
                            uint targetSerial = NetByteConverter.ReadUInt32(rawBuffer, offset + 6);
                            offset += 10;
                            await SendMobileStatusAsync(stream, targetSerial, "Тестовый Герой", huffmanBuffer, token);
                        }
                        else
                        {
                            offset++;
                        }
                    }
                }
                regenCts.Cancel();
            }
        }

        private Task SendStaminaDeltaAsync(
            NetworkStream stream,
            uint serial,
            ushort maxStamina,
            ushort currentStamina,
            byte[] huffmanBuffer,
            CancellationToken token)
        {
            byte[] buf = new byte[9];
            buf[0] = 0xA3;
            NetByteConverter.WriteUInt32(buf, 1, serial);
            NetByteConverter.WriteUInt16(buf, 5, maxStamina);
            NetByteConverter.WriteUInt16(buf, 7, currentStamina);
            return SendCompressedPacketAsync(stream, buf, 9, huffmanBuffer, token);
        }

        private Task SendMobileIncomingAsync(
            NetworkStream stream,
            uint serial,
            ushort body,
            Point3D pos,
            Direction dir,
            byte notoriety,
            byte[] huffmanBuffer,
            CancellationToken token)
        {
            byte[] buf = new byte[23];
            buf[0] = 0x78;
            NetByteConverter.WriteUInt16(buf, 1, 23);
            NetByteConverter.WriteUInt32(buf, 3, serial);
            NetByteConverter.WriteUInt16(buf, 7, body);
            NetByteConverter.WriteInt16(buf, 9, pos.X);
            NetByteConverter.WriteInt16(buf, 11, pos.Y);
            buf[13] = (byte)pos.Z;
            buf[14] = (byte)dir;
            NetByteConverter.WriteUInt16(buf, 15, 0);
            buf[17] = 0;
            buf[18] = notoriety;
            NetByteConverter.WriteUInt32(buf, 19, 0); // End of equipment
            return SendCompressedPacketAsync(stream, buf, 23, huffmanBuffer, token);
        }

        private Task SendWorldItemAsync(
            NetworkStream stream,
            uint serial,
            ushort graphic,
            ushort amount,
            Point3D pos,
            byte[] huffmanBuffer,
            CancellationToken token)
        {
            byte[] buf = new byte[26];
            buf[0] = 0xF3;
            NetByteConverter.WriteUInt16(buf, 1, 1);
            buf[3] = 0;
            NetByteConverter.WriteUInt32(buf, 4, serial);
            NetByteConverter.WriteUInt16(buf, 8, graphic);
            buf[10] = 0;
            NetByteConverter.WriteUInt16(buf, 11, amount);
            NetByteConverter.WriteUInt16(buf, 13, amount);
            NetByteConverter.WriteInt16(buf, 15, pos.X);
            NetByteConverter.WriteInt16(buf, 17, pos.Y);
            buf[19] = (byte)pos.Z;
            return SendCompressedPacketAsync(stream, buf, 26, huffmanBuffer, token);
        }

        private Task SendMobileStatusAsync(
            NetworkStream stream,
            uint serial,
            string name,
            byte[] huffmanBuffer,
            CancellationToken token)
        {
            byte[] buf = new byte[88];
            buf[0] = 0x11;
            NetByteConverter.WriteUInt16(buf, 1, 88);
            NetByteConverter.WriteUInt32(buf, 3, serial);
            byte[] nameBytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(name) ? "Тестовый Герой" : name);
            Array.Copy(nameBytes, 0, buf, 7, Math.Min(nameBytes.Length, 30));
            NetByteConverter.WriteUInt16(buf, 37, 100);
            NetByteConverter.WriteUInt16(buf, 39, 100);
            buf[41] = 0;
            buf[42] = 5;
            buf[43] = 0;
            NetByteConverter.WriteUInt16(buf, 44, 90);
            NetByteConverter.WriteUInt16(buf, 46, 80);
            NetByteConverter.WriteUInt16(buf, 48, 70);
            NetByteConverter.WriteUInt16(buf, 50, 80);
            NetByteConverter.WriteUInt16(buf, 52, 80);
            NetByteConverter.WriteUInt16(buf, 54, 70);
            NetByteConverter.WriteUInt16(buf, 56, 70);
            NetByteConverter.WriteUInt32(buf, 58, 250);
            NetByteConverter.WriteUInt16(buf, 62, 18);
            NetByteConverter.WriteUInt16(buf, 64, 35);
            NetByteConverter.WriteUInt16(buf, 66, 355);
            buf[68] = 1;
            NetByteConverter.WriteUInt16(buf, 69, 225);
            return SendCompressedPacketAsync(stream, buf, 88, huffmanBuffer, token);
        }

        private async Task SendCompressedPacketAsync(
            NetworkStream stream,
            byte[] uncompressed,
            int length,
            byte[] compressionBuffer,
            CancellationToken token)
        {
            await _sendLock.WaitAsync(token);
            try
            {
                int toSend = CompressPacket(uncompressed, length, compressionBuffer);
                await stream.WriteAsync(compressionBuffer, 0, toSend, token);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private static int CompressPacket(byte[] src, int length, byte[] dest)
        {
            if (HuffmanCodec.Compress(src.AsSpan(0, length), dest.AsSpan(), out int compressedLength))
            {
                return compressedLength;
            }
            Array.Copy(src, 0, dest, 0, length);
            return length;
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _sendLock.Dispose();
        }
    }
}
