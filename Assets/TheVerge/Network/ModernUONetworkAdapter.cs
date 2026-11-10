using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TheVerge.Core;
using TheVerge.Network.Processors;

namespace TheVerge.Network
{
    public sealed class ModernUONetworkAdapter :
        ISessionAdapter,
        IMovementAdapter,
        IWorldEntityAdapter,
        IInventoryAdapter,
        IGameplayActionsAdapter,
        IStatsAdapter,
        IDisposable
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly HuffmanCodec _huffman = new HuffmanCodec();

        private readonly LoginPacketProcessor _loginProcessor;
        private readonly MovementPacketProcessor _movementProcessor;
        private readonly WorldEntityPacketProcessor _entityProcessor;
        private readonly CombatPacketProcessor _combatProcessor;
        private readonly StatsPacketProcessor _statsProcessor;
        private readonly InventoryPacketProcessor _inventoryProcessor;
        private readonly GumpAndMessagePacketProcessor _gumpProcessor;

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        private bool _isLiveServer;
        private bool _hasEnteredWorld;
        private Serial _localPlayerSerial = new Serial(0x00010001);
        private string _selectedCharacterName;

        public string ConnectedHost { get; private set; } = "127.0.0.1";
        public int ConnectedPort { get; private set; } = 2593;
        public bool IsLiveServer => _isLiveServer;
        public int SentPacketsCount { get; private set; }
        public int ReceivedPacketsCount { get; private set; }
        public int ReceivedBytesCount { get; private set; }
        public byte LastSequenceKey => _movementProcessor.LastSequenceKey;
        public string AuthStageDescription => _loginProcessor.AuthStageDescription;

        public bool IsConnected => _isConnected;
        public bool HasEnteredWorld => _hasEnteredWorld;
        public Serial LocalPlayerSerial => _localPlayerSerial;
        public event Action<CharacterListDto> OnCharacterListReceived;
        public event Action<string> OnCharacterDeleteFailed;
        public event Action<PlayerLoginConfirmDto> OnPlayerEnteredWorld;
        public event Action<string> OnSessionError;
        public event Action OnDisconnected;

        public Point3D CurrentPosition => _movementProcessor.CurrentPosition;
        public Direction CurrentDirection => _movementProcessor.CurrentDirection;
        public event Action<MoveResultDto> OnMoveAccepted;
        public event Action<MoveResultDto> OnMoveRejected;

        public event Action<MobileStateDto> OnMobileSpawned;
        public event Action<MobileStateDto> OnMobileMoved;
        public event Action<MobileStateDto> OnMobileUpdated;
        public event Action<Serial, string> OnMobileNameUpdated;
        public event Action<ItemStateDto> OnWorldItemSpawned;
        public event Action<Serial> OnEntityDespawned;

        public Serial PlayerBackpackSerial => _inventoryProcessor.PlayerBackpackSerial;
        public event Action<Serial, ushort> OnContainerOpened;
        public event Action<Serial> OnContainerClosed;
        public event Action<Serial, IReadOnlyList<ItemStateDto>> OnContainerContentReceived;
        public event Action<ItemStateDto> OnItemAddedToContainer;
        public event Action<Serial, Serial> OnItemRemovedFromContainer;
        public event Action<Serial, ItemStateDto> OnItemEquipped;
        public event Action<Serial, Layer> OnItemUnequipped;
        public event Action<byte> OnLiftRejected;

        public bool IsWarMode => _combatProcessor.IsWarMode;
        public PlayerStatusDto CurrentPlayerStatus => _statsProcessor.PlayerStatus;
        public event Action<PlayerStatusDto> OnPlayerStatusUpdated;
        public event Action<Serial, ushort, ushort> OnMobileHitsUpdated;
        public event Action<CombatDamageDto> OnDamageDealt;
        public event Action<Serial, Serial> OnCombatSwing;
        public event Action<Serial, int> OnMobileAnimation;
        public event Action<OverheadMessageDto> OnOverheadMessage;
        public event Action<bool> OnWarModeChanged;
        public event Action<TargetCursorRequestDto> OnTargetCursorRequested;
        public event Action<VisualEffectDto> OnVisualEffect;
        public event Action<string> OnGumpWindowOpened;

        public ModernUONetworkAdapter(IMainThreadDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _loginProcessor = new LoginPacketProcessor(_dispatcher, SendRaw);
            _movementProcessor = new MovementPacketProcessor(_dispatcher, SendRaw);
            _entityProcessor = new WorldEntityPacketProcessor(_dispatcher);
            _combatProcessor = new CombatPacketProcessor(_dispatcher, SendRaw);
            _statsProcessor = new StatsPacketProcessor(_dispatcher, SendRaw);
            _inventoryProcessor = new InventoryPacketProcessor(_dispatcher, SendRaw);
            _gumpProcessor = new GumpAndMessagePacketProcessor(_dispatcher, SendRaw);

            WireProcessorEvents();
        }

        private void WireProcessorEvents()
        {
            _loginProcessor.OnCharacterListReceived += dto => OnCharacterListReceived?.Invoke(dto);
            _loginProcessor.OnCharacterDeleteFailed += err => OnCharacterDeleteFailed?.Invoke(err);
            _loginProcessor.OnPlayerEnteredWorld += dto => OnPlayerEnteredWorld?.Invoke(dto);
            _loginProcessor.OnSessionError += err => OnSessionError?.Invoke(err);

            _movementProcessor.OnMoveAccepted += dto => OnMoveAccepted?.Invoke(dto);
            _movementProcessor.OnMoveRejected += dto => OnMoveRejected?.Invoke(dto);

            _entityProcessor.OnMobileSpawned += dto => OnMobileSpawned?.Invoke(dto);
            _entityProcessor.OnMobileMoved += dto => OnMobileMoved?.Invoke(dto);
            _entityProcessor.OnMobileUpdated += dto => OnMobileUpdated?.Invoke(dto);
            _entityProcessor.OnMobileNameUpdated += (ser, name) => OnMobileNameUpdated?.Invoke(ser, name);
            _entityProcessor.OnWorldItemSpawned += dto => OnWorldItemSpawned?.Invoke(dto);
            _entityProcessor.OnEntityDespawned += ser => OnEntityDespawned?.Invoke(ser);
            _entityProcessor.OnItemEquipped += (ser, item) => OnItemEquipped?.Invoke(ser, item);
            _entityProcessor.OnBackpackSerialDiscovered += ser =>
            {
                _inventoryProcessor.PlayerBackpackSerial = ser;
                _inventoryProcessor.UseItem(ser);
            };
            _entityProcessor.OnLocalPlayerUpdatedFromNetwork += (dir, pos, war) =>
            {
                _movementProcessor.CurrentPosition = pos;
                _movementProcessor.CurrentDirection = dir;
                _movementProcessor.PendingPosition = pos;
                _movementProcessor.PendingDirection = dir;
                if (_combatProcessor.IsWarMode != war)
                {
                    _combatProcessor.IsWarMode = war;
                    OnWarModeChanged?.Invoke(war);
                }
            };

            _inventoryProcessor.OnContainerOpened += (ser, gid) => OnContainerOpened?.Invoke(ser, gid);
            _inventoryProcessor.OnContainerClosed += ser => OnContainerClosed?.Invoke(ser);
            _inventoryProcessor.OnContainerContentReceived += (ser, items) => OnContainerContentReceived?.Invoke(ser, items);
            _inventoryProcessor.OnItemAddedToContainer += item => OnItemAddedToContainer?.Invoke(item);
            _inventoryProcessor.OnItemRemovedFromContainer += (c, item) => OnItemRemovedFromContainer?.Invoke(c, item);
            _inventoryProcessor.OnItemEquipped += (ser, item) => OnItemEquipped?.Invoke(ser, item);
            _inventoryProcessor.OnItemUnequipped += (ser, layer) => OnItemUnequipped?.Invoke(ser, layer);
            _inventoryProcessor.OnLiftRejected += r => OnLiftRejected?.Invoke(r);

            _statsProcessor.OnPlayerStatusUpdated += dto => OnPlayerStatusUpdated?.Invoke(dto);
            _statsProcessor.OnMobileHitsUpdated += (ser, h, mh) => OnMobileHitsUpdated?.Invoke(ser, h, mh);

            _combatProcessor.OnDamageDealt += dmg => OnDamageDealt?.Invoke(dmg);
            _combatProcessor.OnCombatSwing += (a, d) => OnCombatSwing?.Invoke(a, d);
            _combatProcessor.OnMobileAnimation += (ser, a) => OnMobileAnimation?.Invoke(ser, a);
            _combatProcessor.OnWarModeChanged += war => OnWarModeChanged?.Invoke(war);
            _combatProcessor.OnTargetCursorRequested += req => OnTargetCursorRequested?.Invoke(req);

            _gumpProcessor.OnOverheadMessage += msg => OnOverheadMessage?.Invoke(msg);
            _gumpProcessor.OnGumpWindowOpened += gump => OnGumpWindowOpened?.Invoke(gump);
            _gumpProcessor.OnVisualEffect += fx => OnVisualEffect?.Invoke(fx);
        }

        public void Login(string host, int port, string account, string password)
        {
            Disconnect();
            ConnectedHost = host;
            ConnectedPort = port;
            _cts = new CancellationTokenSource();
            _movementProcessor.Reset();
            _huffman.Reset();
            _loginProcessor.SetStageDescription($"Подключение к {host}:{port}...");

            Task.Run(async () =>
            {
                try
                {
                    _tcpClient = new TcpClient();
                    await _tcpClient.ConnectAsync(host, port);
                    _stream = _tcpClient.GetStream();
                    _isConnected = true;
                    _isLiveServer = (port == 2593);

                    var (gameClient, gameStream) = await _loginProcessor.RunAuthenticationAsync(_tcpClient, _stream, host, port, account, password, _isLiveServer, _cts.Token);
                    _tcpClient = gameClient;
                    _stream = gameStream;
                    _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
                }
                catch (Exception ex)
                {
                    _dispatcher.Enqueue(() =>
                    {
                        _loginProcessor.SetStageDescription($"Ошибка: {ex.Message}");
                        OnSessionError?.Invoke(ex.Message);
                    });
                }
            });
        }

        public void SelectCharacter(int slotIndex, string characterName = null)
        {
            if (!string.IsNullOrEmpty(characterName))
            {
                _selectedCharacterName = characterName;
                _statsProcessor.SetPlayerName(characterName);
            }
            _loginProcessor.SendSelectCharacter(slotIndex, _selectedCharacterName);
        }

        public void CreateCharacter(CharacterCreationArgsDto args, int slotIndex = 0)
        {
            if (!string.IsNullOrEmpty(args.CharacterName))
            {
                _selectedCharacterName = args.CharacterName;
                _statsProcessor.SetPlayerName(args.CharacterName);
            }
            _loginProcessor.SendCreateCharacter(args, slotIndex);
        }

        public void DeleteCharacter(int slotIndex)
        {
            _loginProcessor.SendDeleteCharacter(slotIndex);
        }

        public void Connect(string host, int port, string account, string password, int characterSlot = 0)
        {
            Action<CharacterListDto> onList = null;
            onList = list =>
            {
                OnCharacterListReceived -= onList;
                SelectCharacter(characterSlot);
            };
            OnCharacterListReceived += onList;
            Login(host, port, account, password);
        }

        public void ConnectWithCharacterCreation(string host, int port, string account, string password, CharacterCreationArgsDto creationArgs)
        {
            Action<CharacterListDto> onList = null;
            onList = list =>
            {
                OnCharacterListReceived -= onList;
                CreateCharacter(creationArgs);
            };
            OnCharacterListReceived += onList;
            Login(host, port, account, password);
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] rawBuffer = new byte[8192];
            byte[] decompBuffer = new byte[65536];
            int decompCount = 0;

            while (!token.IsCancellationRequested && _isConnected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(rawBuffer, 0, rawBuffer.Length, token);
                    if (bytesRead <= 0) break;

                    ReceivedBytesCount += bytesRead;
                    if (_huffman.Decompress(rawBuffer.AsSpan(0, bytesRead), decompBuffer.AsSpan(decompCount), out int written))
                    {
                        decompCount += written;
                    }
                    ProcessPackets(decompBuffer, ref decompCount);
                }
                catch
                {
                    if (token.IsCancellationRequested) break;
                }
            }

            _dispatcher.Enqueue(() =>
            {
                _isConnected = false;
                _loginProcessor.SetStageDescription("Отключено от сервера");
                OnDisconnected?.Invoke();
            });
        }

        private void ProcessPackets(byte[] buffer, ref int count)
        {
            int offset = 0;
            while (offset < count)
            {
                var slice = buffer.AsSpan(offset, count - offset);
                if (!PacketLengthTable.TryGetPacketLength(slice, out int len) || len <= 0 || slice.Length < len) break;
                ParsePacket(slice.Slice(0, len));
                offset += len;
            }
            if (offset > 0)
            {
                int rem = count - offset;
                if (rem > 0) Array.Copy(buffer, offset, buffer, 0, rem);
                count = rem;
            }
        }

        private void ParsePacket(ReadOnlySpan<byte> packet)
        {
            ReceivedPacketsCount++;
            byte op = packet[0];
            switch (op)
            {
                case 0xBD:
                    _loginProcessor.SendClientVersion();
                    break;
                case 0x86:
                case 0xA9: _loginProcessor.ParseCharacterList(packet); break;
                case 0x85: _loginProcessor.ParseDeleteResult(packet); break;
                case 0x1B:
                    _hasEnteredWorld = true;
                    _loginProcessor.ParseLoginConfirm(packet, out _localPlayerSerial, out var p, out var d);
                    _movementProcessor.CurrentPosition = p;
                    _movementProcessor.CurrentDirection = d;
                    _movementProcessor.PendingPosition = p;
                    _movementProcessor.PendingDirection = d;
                    if (!string.IsNullOrEmpty(_selectedCharacterName))
                    {
                        _statsProcessor.SetPlayerName(_selectedCharacterName);
                    }
                    _statsProcessor.RequestStats(_localPlayerSerial);
                    break;
                case 0x22: _movementProcessor.ParseMoveAck(packet); break;
                case 0x21: _movementProcessor.ParseMoveReject(packet); break;
                case 0x78: _entityProcessor.ParseMobileIncoming(packet, _localPlayerSerial); break;
                case 0x77: _entityProcessor.ParseMobileMoving(packet, _localPlayerSerial); break;
                case 0x20: _entityProcessor.ParseMobileUpdate(packet, _localPlayerSerial); break;
                case 0x1A: _entityProcessor.ParseWorldItem0x1A(packet); break;
                case 0xF3: _entityProcessor.ParseWorldItem(packet); break;
                case 0x1D:
                    _entityProcessor.ParseDeleteObject(packet);
                    _inventoryProcessor.NotifyObjectDeleted(new Serial(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(1, 4))));
                    break;
                case 0x98: _entityProcessor.ParseMobileName(packet); break;
                case 0x11: _statsProcessor.ParseMobileStatus(packet, _localPlayerSerial); break;
                case 0x2D: _statsProcessor.ParseMobileAttributes(packet, _localPlayerSerial); break;
                case 0xA1: _statsProcessor.ParseHitsDelta(packet, _localPlayerSerial); break;
                case 0xA2: _statsProcessor.ParseManaDelta(packet, _localPlayerSerial); break;
                case 0xA3: _statsProcessor.ParseStaminaDelta(packet, _localPlayerSerial); break;
                case 0x0B: _combatProcessor.ParseCombatDamage(packet); break;
                case 0x2F: _combatProcessor.ParseCombatSwing(packet); break;
                case 0x6E: _combatProcessor.ParseMobileAnimation(packet); break;
                case 0x72: _combatProcessor.ParseWarMode(packet); break;
                case 0x6C: _combatProcessor.ParseTargetCursor(packet); break;
                case 0x24: _inventoryProcessor.ParseOpenContainer(packet); break;
                case 0x3C: _inventoryProcessor.ParseContainerContents(packet); break;
                case 0x25: _inventoryProcessor.ParseAddItem(packet); break;
                case 0x2E: _inventoryProcessor.ParseEquipItem(packet, _localPlayerSerial); break;
                case 0x27: _inventoryProcessor.ParseLiftReject(packet); break;
                case 0xBF: _inventoryProcessor.ParseGeneralInfo(packet); break;
                case 0x1C: _gumpProcessor.ParseAsciiSpeech(packet); break;
                case 0xAE: _gumpProcessor.ParseUnicodeSpeech(packet); break;
                case 0x70:
                case 0xC0: _gumpProcessor.ParseGraphicalEffect(packet); break;
                case 0xB0:
                case 0xDD: _gumpProcessor.ParseGump(packet); break;
            }
        }

        public bool TryRequestMove(Direction direction, bool isRunning) =>
            _hasEnteredWorld && _movementProcessor.TryRequestMove(direction, isRunning, _isConnected, _isLiveServer);
        public void RequestTurn(Direction direction)
        {
            if (_hasEnteredWorld) _movementProcessor.RequestTurn(direction, _isConnected, _isLiveServer);
        }
        public void RequestWarMode(bool warMode)
        {
            if (_hasEnteredWorld) _combatProcessor.RequestWarMode(warMode);
        }
        public void RequestAttack(Serial targetMobile)
        {
            if (_hasEnteredWorld) _combatProcessor.RequestAttack(targetMobile);
        }
        public void RequestStats(Serial mobileSerial)
        {
            if (_hasEnteredWorld) _statsProcessor.RequestStats(mobileSerial);
        }
        public void SendTargetResponseGround(uint id, Point3D pos, ushort gr = 0)
        {
            if (_hasEnteredWorld) _combatProcessor.SendTargetResponseGround(id, pos, gr);
        }
        public void SendTargetResponseEntity(uint id, Serial ser, Point3D pos, ushort gr = 0)
        {
            if (_hasEnteredWorld) _combatProcessor.SendTargetResponseEntity(id, ser, pos, gr);
        }
        public void UseItem(Serial itemSerial)
        {
            if (_hasEnteredWorld) _inventoryProcessor.UseItem(itemSerial);
        }
        public void LiftItem(Serial itemSerial, ushort amount = 1)
        {
            if (_hasEnteredWorld) _inventoryProcessor.LiftItem(itemSerial, amount);
        }
        public void DropToWorld(Serial itemSerial, Point3D pos)
        {
            if (_hasEnteredWorld) _inventoryProcessor.DropToWorld(itemSerial, pos);
        }
        public void DropToContainer(Serial itemSerial, Serial containerSerial)
        {
            if (_hasEnteredWorld) _inventoryProcessor.DropToContainer(itemSerial, containerSerial);
        }
        public void EquipItem(Serial itemSerial, Layer targetLayer)
        {
            if (_hasEnteredWorld) _inventoryProcessor.EquipItem(itemSerial, targetLayer, _localPlayerSerial);
        }
        public void CloseContainer(Serial containerSerial)
        {
            if (_hasEnteredWorld) _inventoryProcessor.CloseContainer(containerSerial);
        }

        public void RequestMineAt(Point3D veinPosition, Serial veinSerial)
        {
            if (!_hasEnteredWorld) return;
            if (veinSerial.IsValid) UseItem(veinSerial);
            _gumpProcessor.SendAsciiCommand($"[minoc:mine:{veinPosition.X}:{veinPosition.Y}]");
        }

        public void RequestSmeltOre(Serial forgeSerial)
        {
            if (!_hasEnteredWorld) return;
            if (forgeSerial.IsValid) UseItem(forgeSerial);
            _gumpProcessor.SendAsciiCommand("[minoc:smelt]");
        }

        public void RequestOpenAnvil(Serial anvilSerial)
        {
            if (_hasEnteredWorld) _dispatcher.Enqueue(() => OnGumpWindowOpened?.Invoke("craft"));
        }
        public void RequestCraftItem(int recipeId)
        {
            if (_hasEnteredWorld) _gumpProcessor.SendAsciiCommand($"[minoc:craft:{recipeId}]");
        }
        public void RequestOpenVendor(Serial vendorSerial)
        {
            if (_hasEnteredWorld) _dispatcher.Enqueue(() => OnGumpWindowOpened?.Invoke("vendor"));
        }
        public void RequestVendorTransaction(int offerId)
        {
            if (_hasEnteredWorld) _gumpProcessor.SendAsciiCommand($"[minoc:vendor:{offerId}]");
        }
        public void RequestHotbarAbility(int slotIndex, Serial targetMobile)
        {
            if (_hasEnteredWorld) _gumpProcessor.SendAsciiCommand($"[minoc:ability:{slotIndex}:{targetMobile.Value}]");
        }
        public void SimulateMoveReject() => _movementProcessor.SimulateMoveReject();

        private void SendRaw(byte[] data)
        {
            if (_stream == null || !_isConnected) return;
            try
            {
                SentPacketsCount++;
                _stream.Write(data, 0, data.Length);
            }
            catch { }
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            _isConnected = false;
            _hasEnteredWorld = false;
            try { _stream?.Close(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            _stream = null;
            _tcpClient = null;
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
        }
    }
}
