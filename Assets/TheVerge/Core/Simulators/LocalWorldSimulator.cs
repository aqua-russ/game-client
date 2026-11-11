#nullable disable // долг прототипа: файл заменяется по docs/TECH-DEBT.md, разметку nullable не делаем
using System;
using System.Collections.Generic;

namespace TheVerge.Core
{
    /// <summary>
    /// Автономный локальный симулятор сервера в памяти.
    /// Реализует все 5 интерфейсов адаптеров (Session, Movement, WorldEntity, Inventory, GameplayActions),
    /// проверяет дистанцию взаимодействия (до 2 клеток), хранит рюкзак, сундук, слои экипировки,
    /// горное дело, кузнечное ремесло, торговлю и бой.
    /// </summary>
    public sealed class LocalWorldSimulator :
        ISessionAdapter,
        IMovementAdapter,
        IWorldEntityAdapter,
        IInventoryAdapter,
        IGameplayActionsAdapter
    {
        public static readonly Point3D MinocStartPoint = new Point3D(2545, 502, 0);
        public static readonly Serial ChestSerial = new Serial(0x40000005);
        public static readonly Serial BlacksmithSerial = new Serial(0x00020001);
        public static readonly Serial GuardianSerial = new Serial(0x00020002);

        private readonly MovementSequenceQueue _sequenceQueue = new MovementSequenceQueue();
        private readonly Dictionary<Serial, ItemStateDto> _worldItems = new Dictionary<Serial, ItemStateDto>();
        private readonly Dictionary<Serial, ItemStateDto> _backpackItems = new Dictionary<Serial, ItemStateDto>();
        private readonly Dictionary<Serial, ItemStateDto> _chestItems = new Dictionary<Serial, ItemStateDto>();
        private readonly Dictionary<Layer, ItemStateDto> _equippedItems = new Dictionary<Layer, ItemStateDto>();

        private readonly Serial _playerSerial = new Serial(0x00010001);
        private readonly Serial _backpackSerial = new Serial(0x40000100);
        private Point3D _currentPosition = MinocStartPoint;
        private Direction _currentDirection = Direction.South;
        private bool _isConnected;
        private bool _hasEnteredWorld;
        private bool _isChestOpen;
        private bool _isWarMode;
        private ushort _guardianHits = 80;
        private uint _nextItemSerial = 0x40000200;
        private ItemStateDto? _heldItem;
        private PlayerStatusDto _playerStatus;
        private string _selectedCharacterName;
        private int _runStepCounter;

        // ISessionAdapter
        public bool IsConnected => _isConnected;
        public bool HasEnteredWorld => _hasEnteredWorld;
        public Serial LocalPlayerSerial => _playerSerial;
        public event Action<CharacterListDto> OnCharacterListReceived;
        public event Action<string> OnCharacterDeleteFailed;
        public event Action<PlayerLoginConfirmDto> OnPlayerEnteredWorld;
        public event Action<string> OnSessionError;
        public event Action OnDisconnected;

        // IMovementAdapter
        public Point3D CurrentPosition => _currentPosition;
        public Direction CurrentDirection => _currentDirection;
        public event Action<MoveResultDto> OnMoveAccepted;
        public event Action<MoveResultDto> OnMoveRejected;

        // IWorldEntityAdapter
        public event Action<MobileStateDto> OnMobileSpawned;
        public event Action<MobileStateDto> OnMobileMoved;
        public event Action<MobileStateDto> OnMobileUpdated;
        public event Action<Serial, string> OnMobileNameUpdated;
        public event Action<ItemStateDto> OnWorldItemSpawned;
        public event Action<Serial> OnEntityDespawned;

        // IInventoryAdapter
        public Serial PlayerBackpackSerial => _backpackSerial;
        public event Action<Serial, ushort> OnContainerOpened;
        public event Action<Serial> OnContainerClosed;
        public event Action<Serial, IReadOnlyList<ItemStateDto>> OnContainerContentReceived;
        public event Action<ItemStateDto> OnItemAddedToContainer;
        public event Action<Serial, Serial> OnItemRemovedFromContainer;
        public event Action<Serial, ItemStateDto> OnItemEquipped;
        public event Action<Serial, Layer> OnItemUnequipped;
        public event Action<byte> OnLiftRejected;

        // IGameplayActionsAdapter
        public bool IsWarMode => _isWarMode;
        public PlayerStatusDto CurrentPlayerStatus => _playerStatus;
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

        public void Connect(string host, int port, string account, string password, int characterSlot = 0)
        {
            _isConnected = true;
            _currentPosition = MinocStartPoint;
            _currentDirection = Direction.South;
            _sequenceQueue.Reset();
            _playerStatus = !string.IsNullOrEmpty(_selectedCharacterName)
                ? new PlayerStatusDto(_playerSerial, _selectedCharacterName, 100, 100, 70, 70, 80, 80, 90, 80, 70, 250, 18, 35, 400)
                : PlayerStatusDto.CreateDefault(_playerSerial);

            // 1. Подтверждаем вход персонажа
            _hasEnteredWorld = true;
            OnPlayerEnteredWorld?.Invoke(new PlayerLoginConfirmDto(_playerSerial, 400, _currentPosition, _currentDirection));
            OnPlayerStatusUpdated?.Invoke(_playerStatus);

            // 2. Создаем тестовое окружение на участке Минока
            SpawnDefaultWorld();

            // 3. Открываем стартовый рюкзак игрока
            OnContainerOpened?.Invoke(_backpackSerial, 0x003C);
            OnContainerContentReceived?.Invoke(_backpackSerial, new List<ItemStateDto>(_backpackItems.Values));
        }

        public void ConnectWithCharacterCreation(string host, int port, string account, string password, CharacterCreationArgsDto creationArgs)
        {
            Connect(host, port, account, password, 0);
        }

        private readonly List<CharacterSlotDto> _mockSlots = new List<CharacterSlotDto>
        {
            new CharacterSlotDto(0, "Витязь"),
            new CharacterSlotDto(1, string.Empty),
            new CharacterSlotDto(2, string.Empty),
            new CharacterSlotDto(3, string.Empty),
            new CharacterSlotDto(4, string.Empty)
        };

        public void Login(string host, int port, string account, string password)
        {
            _isConnected = true;
            OnCharacterListReceived?.Invoke(new CharacterListDto(new List<CharacterSlotDto>(_mockSlots)));
        }

        public void SelectCharacter(int slotIndex, string characterName = null)
        {
            if (string.IsNullOrEmpty(characterName) && slotIndex >= 0 && slotIndex < _mockSlots.Count)
            {
                characterName = _mockSlots[slotIndex].Name;
            }
            _selectedCharacterName = characterName;
            Connect("127.0.0.1", 2593, "local", "local", slotIndex);
        }

        public void CreateCharacter(CharacterCreationArgsDto args, int slotIndex = 0)
        {
            _selectedCharacterName = args.CharacterName;
            if (slotIndex >= 0 && slotIndex < _mockSlots.Count)
            {
                _mockSlots[slotIndex] = new CharacterSlotDto(slotIndex, args.CharacterName);
            }
            Connect("127.0.0.1", 2593, "local", "local", slotIndex);
        }

        public void DeleteCharacter(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < _mockSlots.Count)
            {
                _mockSlots[slotIndex] = new CharacterSlotDto(slotIndex, string.Empty);
                OnCharacterListReceived?.Invoke(new CharacterListDto(new List<CharacterSlotDto>(_mockSlots)));
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _hasEnteredWorld = false;
            OnDisconnected?.Invoke();
        }

        private void SpawnDefaultWorld()
        {
            _worldItems.Clear();
            _backpackItems.Clear();
            _chestItems.Clear();
            _equippedItems.Clear();
            _guardianHits = 80;

            // Мирный кузнец Добрыня у лагеря
            var blacksmith = new MobileStateDto(
                BlacksmithSerial,
                400,
                0x03EA,
                new Point3D(2541, 505, 0),
                Direction.East,
                1,
                "Добрыня (Кузнец)",
                100,
                100,
                false
            );
            OnMobileSpawned?.Invoke(blacksmith);

            // Враждебный Страж Шахты у входа в пещеру
            var guardian = new MobileStateDto(
                GuardianSerial,
                50,
                0x0455,
                new Point3D(2552, 499, 0),
                Direction.West,
                7,
                "Страж Шахты",
                _guardianHits,
                80,
                true
            );
            OnMobileSpawned?.Invoke(guardian);

            // Стартовый набор в рюкзаке витязя для мгновенного тестирования экипировки и сброса
            var starterSword = new ItemStateDto(new Serial(0x40000051), 0x0F5E, 0, 1, Point3D.Zero, _backpackSerial, Layer.Invalid);
            var starterShield = new ItemStateDto(new Serial(0x40000052), 0x1B7A, 0, 1, Point3D.Zero, _backpackSerial, Layer.Invalid);
            var starterHelm = new ItemStateDto(new Serial(0x40000053), 0x1412, 0, 1, Point3D.Zero, _backpackSerial, Layer.Invalid);
            var starterPotion = new ItemStateDto(new Serial(0x40000054), 0x0F0C, 0, 5, Point3D.Zero, _backpackSerial, Layer.Invalid);
            var starterGold = new ItemStateDto(new Serial(0x40000055), 0x0EED, 0, 100, Point3D.Zero, _backpackSerial, Layer.Invalid);
            _backpackItems[starterSword.Serial] = starterSword;
            _backpackItems[starterShield.Serial] = starterShield;
            _backpackItems[starterHelm.Serial] = starterHelm;
            _backpackItems[starterPotion.Serial] = starterPotion;
            _backpackItems[starterGold.Serial] = starterGold;

            // Тестовые предметы на земле вокруг стартовой точки
            AddWorldItem(new Serial(0x40000001), 0x0F5E, 0, 1, new Point3D(2546, 502, 0));   // Длинный меч
            AddWorldItem(new Serial(0x40000002), 0x0E85, 0, 1, new Point3D(2545, 503, 0));   // Кирка шахтёра
            AddWorldItem(new Serial(0x40000003), 0x19B9, 0, 10, new Point3D(2546, 503, 0));  // Железная руда (10 шт.)
            AddWorldItem(new Serial(0x40000004), 0x0EED, 0, 100, new Point3D(2544, 502, 0)); // Золотые монеты (100 шт.)
            AddWorldItem(ChestSerial, 0x0E7C, 0, 1, new Point3D(2544, 503, 0));              // Деревянный сундук

            // Содержимое деревянного сундука
            var ingots = new ItemStateDto(new Serial(0x40000006), 0x1BF2, 0, 50, Point3D.Zero, ChestSerial, Layer.Invalid);
            var shield = new ItemStateDto(new Serial(0x40000007), 0x1B7A, 0, 1, Point3D.Zero, ChestSerial, Layer.Invalid);
            var potion = new ItemStateDto(new Serial(0x40000008), 0x0F0C, 0, 5, Point3D.Zero, ChestSerial, Layer.Invalid);
            _chestItems[ingots.Serial] = ingots;
            _chestItems[shield.Serial] = shield;
            _chestItems[potion.Serial] = potion;
        }

        private void AddWorldItem(Serial serial, ushort graphic, ushort hue, ushort amount, Point3D pos)
        {
            var item = new ItemStateDto(serial, graphic, hue, amount, pos, Serial.Zero, Layer.Invalid);
            _worldItems[serial] = item;
            OnWorldItemSpawned?.Invoke(item);
        }

        private bool IsWithinReach(Point3D targetPos, int maxTileDistance = 2)
        {
            int dx = Math.Abs(_currentPosition.X - targetPos.X);
            int dy = Math.Abs(_currentPosition.Y - targetPos.Y);
            return dx <= maxTileDistance && dy <= maxTileDistance;
        }

        public bool TryRequestMove(Direction direction, bool isRunning)
        {
            if (!_isConnected) return false;

            var nextFacing = direction & Direction.FacingMask;
            var baseTarget = _currentPosition.GetStepPoint(direction);

            if (!MinocMapSliceData.IsPassable(baseTarget.X, baseTarget.Y, out sbyte targetZ))
            {
                _sequenceQueue.Reset();
                OnMoveRejected?.Invoke(new MoveResultDto(0, _currentPosition, _currentDirection));
                return false;
            }

            var targetPos = new Point3D(baseTarget.X, baseTarget.Y, targetZ);

            bool isShoveTarget = (targetPos.X == 2541 && targetPos.Y == 505) || (targetPos.X == 2552 && targetPos.Y == 499);
            if (isShoveTarget)
            {
                if (_playerStatus.Stamina < _playerStatus.MaxStamina)
                {
                    _sequenceQueue.Reset();
                    OnMoveRejected?.Invoke(new MoveResultDto(0, _currentPosition, _currentDirection));
                    return false;
                }

                ushort shoveStam = (ushort)Math.Max(0, _playerStatus.Stamina - 10);
                _playerStatus = new PlayerStatusDto(
                    _playerStatus.Serial, _playerStatus.Name, _playerStatus.Hits, _playerStatus.MaxHits,
                    _playerStatus.Mana, _playerStatus.MaxMana, shoveStam, _playerStatus.MaxStamina,
                    _playerStatus.Str, _playerStatus.Dex, _playerStatus.Int, _playerStatus.Gold,
                    _playerStatus.ArmorRating, _playerStatus.Weight, _playerStatus.MaxWeight);
                OnPlayerStatusUpdated?.Invoke(_playerStatus);
            }

            if (!_sequenceQueue.TryEnqueueStep(direction, targetPos, out byte seq))
            {
                return false;
            }

            _currentPosition = targetPos;
            _currentDirection = nextFacing;
            _sequenceQueue.ConfirmStep(seq, out _);

            if (isRunning)
            {
                _runStepCounter++;
                if (_runStepCounter >= 3)
                {
                    _runStepCounter = 0;
                    if (_playerStatus.Stamina > 0)
                    {
                        ushort newStam = (ushort)(_playerStatus.Stamina - 1);
                        _playerStatus = new PlayerStatusDto(
                            _playerStatus.Serial, _playerStatus.Name, _playerStatus.Hits, _playerStatus.MaxHits,
                            _playerStatus.Mana, _playerStatus.MaxMana, newStam, _playerStatus.MaxStamina,
                            _playerStatus.Str, _playerStatus.Dex, _playerStatus.Int, _playerStatus.Gold,
                            _playerStatus.ArmorRating, _playerStatus.Weight, _playerStatus.MaxWeight);
                        OnPlayerStatusUpdated?.Invoke(_playerStatus);
                    }
                }
            }
            else
            {
                _runStepCounter = 0;
                if (_playerStatus.Stamina < _playerStatus.MaxStamina)
                {
                    ushort newStam = (ushort)Math.Min((int)_playerStatus.MaxStamina, _playerStatus.Stamina + 1);
                    _playerStatus = new PlayerStatusDto(
                        _playerStatus.Serial, _playerStatus.Name, _playerStatus.Hits, _playerStatus.MaxHits,
                        _playerStatus.Mana, _playerStatus.MaxMana, newStam, _playerStatus.MaxStamina,
                        _playerStatus.Str, _playerStatus.Dex, _playerStatus.Int, _playerStatus.Gold,
                        _playerStatus.ArmorRating, _playerStatus.Weight, _playerStatus.MaxWeight);
                    OnPlayerStatusUpdated?.Invoke(_playerStatus);
                }
            }

            if (_isChestOpen && _worldItems.TryGetValue(ChestSerial, out var chestItem))
            {
                if (!IsWithinReach(chestItem.WorldPosition, 2))
                {
                    _isChestOpen = false;
                    OnContainerClosed?.Invoke(ChestSerial);
                }
            }

            OnMoveAccepted?.Invoke(new MoveResultDto(seq, _currentPosition, _currentDirection));
            return true;
        }

        public void RequestTurn(Direction direction)
        {
            _currentDirection = direction & Direction.FacingMask;
            OnMoveAccepted?.Invoke(new MoveResultDto(0, _currentPosition, _currentDirection));
        }

        public void UseItem(Serial itemSerial)
        {
            if (itemSerial == ChestSerial && _worldItems.TryGetValue(ChestSerial, out var chest))
            {
                if (!IsWithinReach(chest.WorldPosition, 2))
                {
                    OnLiftRejected?.Invoke(1);
                    return;
                }

                _isChestOpen = true;
                OnContainerOpened?.Invoke(ChestSerial, 0x004A);
                OnContainerContentReceived?.Invoke(ChestSerial, new List<ItemStateDto>(_chestItems.Values));
            }
        }

        public void CloseContainer(Serial containerSerial)
        {
            if (containerSerial == ChestSerial && _isChestOpen)
            {
                _isChestOpen = false;
                OnContainerClosed?.Invoke(ChestSerial);
            }
        }

        public void LiftItem(Serial itemSerial, ushort amount = 1)
        {
            if (itemSerial == ChestSerial)
            {
                UseItem(ChestSerial);
                return;
            }

            if (_worldItems.TryGetValue(itemSerial, out var worldItem))
            {
                if (!IsWithinReach(worldItem.WorldPosition, 2))
                {
                    OnLiftRejected?.Invoke(1);
                    return;
                }

                _heldItem = worldItem;
                _worldItems.Remove(itemSerial);
                OnEntityDespawned?.Invoke(itemSerial);
                return;
            }

            if (_backpackItems.TryGetValue(itemSerial, out var backpackItem))
            {
                _heldItem = backpackItem;
                _backpackItems.Remove(itemSerial);
                OnItemRemovedFromContainer?.Invoke(_backpackSerial, itemSerial);
                return;
            }

            if (_chestItems.TryGetValue(itemSerial, out var chestItem))
            {
                if (_worldItems.TryGetValue(ChestSerial, out var chest) && !IsWithinReach(chest.WorldPosition, 2))
                {
                    OnLiftRejected?.Invoke(1);
                    return;
                }

                _heldItem = chestItem;
                _chestItems.Remove(itemSerial);
                OnItemRemovedFromContainer?.Invoke(ChestSerial, itemSerial);
                return;
            }

            foreach (var kvp in _equippedItems)
            {
                if (kvp.Value.Serial == itemSerial)
                {
                    _heldItem = kvp.Value;
                    _equippedItems.Remove(kvp.Key);
                    OnItemUnequipped?.Invoke(_playerSerial, kvp.Key);
                    return;
                }
            }

            OnLiftRejected?.Invoke(0);
        }

        public void DropToWorld(Serial itemSerial, Point3D targetPosition)
        {
            if (_heldItem.HasValue && _heldItem.Value.Serial == itemSerial)
            {
                sbyte z = MinocMapSliceData.IsPassable(targetPosition.X, targetPosition.Y, out sbyte tileZ) ? tileZ : (sbyte)0;
                var posWithZ = new Point3D(targetPosition.X, targetPosition.Y, z);

                var dropped = new ItemStateDto(
                    itemSerial,
                    _heldItem.Value.GraphicId,
                    _heldItem.Value.Hue,
                    _heldItem.Value.Amount,
                    posWithZ,
                    Serial.Zero,
                    Layer.Invalid
                );
                _worldItems[itemSerial] = dropped;
                _heldItem = null;
                OnWorldItemSpawned?.Invoke(dropped);
            }
        }

        public void DropToContainer(Serial itemSerial, Serial targetContainerSerial)
        {
            if (_heldItem.HasValue && _heldItem.Value.Serial == itemSerial)
            {
                var stored = new ItemStateDto(
                    itemSerial,
                    _heldItem.Value.GraphicId,
                    _heldItem.Value.Hue,
                    _heldItem.Value.Amount,
                    Point3D.Zero,
                    targetContainerSerial,
                    Layer.Invalid
                );

                if (targetContainerSerial == ChestSerial)
                {
                    _chestItems[itemSerial] = stored;
                }
                else
                {
                    _backpackItems[itemSerial] = stored;
                }

                _heldItem = null;
                OnItemAddedToContainer?.Invoke(stored);
            }
        }

        public void EquipItem(Serial itemSerial, Layer targetLayer)
        {
            if (_heldItem.HasValue && _heldItem.Value.Serial == itemSerial)
            {
                if (_equippedItems.TryGetValue(targetLayer, out var previousEquipped))
                {
                    _equippedItems.Remove(targetLayer);
                    OnItemUnequipped?.Invoke(_playerSerial, targetLayer);

                    var returnedToBag = new ItemStateDto(
                        previousEquipped.Serial,
                        previousEquipped.GraphicId,
                        previousEquipped.Hue,
                        previousEquipped.Amount,
                        Point3D.Zero,
                        _backpackSerial,
                        Layer.Invalid
                    );
                    _backpackItems[returnedToBag.Serial] = returnedToBag;
                    OnItemAddedToContainer?.Invoke(returnedToBag);
                }

                var equipped = new ItemStateDto(
                    itemSerial,
                    _heldItem.Value.GraphicId,
                    _heldItem.Value.Hue,
                    _heldItem.Value.Amount,
                    Point3D.Zero,
                    _playerSerial,
                    targetLayer
                );
                _equippedItems[targetLayer] = equipped;
                _heldItem = null;
                OnItemEquipped?.Invoke(_playerSerial, equipped);
            }
        }

        // -------------------------------------------------------------------
        // IGameplayActionsAdapter (Локальная симуляция для оффлайн-режима и тестов)
        // -------------------------------------------------------------------
        public void RequestWarMode(bool warMode)
        {
            _isWarMode = warMode;
            OnWarModeChanged?.Invoke(warMode);
        }

        public void RequestAttack(Serial targetMobile)
        {
            RequestHotbarAbility(1, targetMobile);
        }

        public void RequestStats(Serial mobileSerial)
        {
            OnPlayerStatusUpdated?.Invoke(_playerStatus);
        }

        public void SendTargetResponseGround(uint targetId, Point3D position, ushort staticGraphic = 0)
        {
            RequestMineAt(position, Serial.Zero);
        }

        public void SendTargetResponseEntity(uint targetId, Serial entitySerial, Point3D position, ushort graphicId = 0)
        {
            RequestAttack(entitySerial);
        }

        public void RequestMineAt(Point3D veinPosition, Serial veinSerial)
        {
            // Проверка дистанции: персонаж должен быть не дальше 3 клеток
            int dx = Math.Abs(_currentPosition.X - veinPosition.X);
            int dy = Math.Abs(_currentPosition.Y - veinPosition.Y);
            if (dx > 3 || dy > 3)
            {
                OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "Слишком далеко! Подойдите ближе (до 3 клеток)", 0x0022));
                return;
            }

            // Проверка тайла: копать можно только в пещере или у скалы
            if (!MinocMapSliceData.IsMinableTile(veinPosition.X, veinPosition.Y))
            {
                OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "Здесь нет руды! Копать можно только в пещере или у скалы.", 0x0022));
                return;
            }

            var oreSerial = new Serial(_nextItemSerial++);
            var oreItem = new ItemStateDto(oreSerial, 0x19B9, 0, 3, Point3D.Zero, _backpackSerial, Layer.Invalid);
            _backpackItems[oreSerial] = oreItem;
            OnMobileAnimation?.Invoke(_playerSerial, 11);
            OnItemAddedToContainer?.Invoke(oreItem);
            OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "+3 Железная руда", 0x0044));
        }

        public void RequestSmeltOre(Serial forgeSerial)
        {
            // Горн находится в пещере на (2558, 496)
            int dx = Math.Abs(_currentPosition.X - 2558);
            int dy = Math.Abs(_currentPosition.Y - 496);
            if (dx > 3 || dy > 3)
            {
                OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "Слишком далеко! Подойдите к кузнечному горну в пещере.", 0x0022));
                return;
            }
            var ingotSerial = new Serial(_nextItemSerial++);
            var ingotItem = new ItemStateDto(ingotSerial, 0x1BF2, 0, 4, Point3D.Zero, _backpackSerial, Layer.Invalid);
            _backpackItems[ingotSerial] = ingotItem;
            OnMobileAnimation?.Invoke(_playerSerial, 9);
            OnItemAddedToContainer?.Invoke(ingotItem);
            OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "+4 Слитка железа", 0x0035));
        }

        public void RequestOpenAnvil(Serial anvilSerial)
        {
            OnGumpWindowOpened?.Invoke("craft");
        }

        public void RequestCraftItem(int recipeId)
        {
            ushort graphicId = recipeId switch
            {
                1 => 0x0F5E,
                2 => 0x1B7A,
                3 => 0x0E85,
                _ => 0x1412
            };

            var craftedSerial = new Serial(_nextItemSerial++);
            var craftedItem = new ItemStateDto(craftedSerial, graphicId, 0, 1, Point3D.Zero, _backpackSerial, Layer.Invalid);
            _backpackItems[craftedSerial] = craftedItem;
            OnMobileAnimation?.Invoke(_playerSerial, 9);
            OnItemAddedToContainer?.Invoke(craftedItem);
            OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "Выковано изделие!", 0x0044));
        }

        public void RequestOpenVendor(Serial vendorSerial)
        {
            OnGumpWindowOpened?.Invoke("vendor");
        }

        public void RequestVendorTransaction(int offerId)
        {
            uint nextGold = offerId <= 2 ? _playerStatus.Gold + 50 : (_playerStatus.Gold >= 30 ? _playerStatus.Gold - 30 : _playerStatus.Gold);
            _playerStatus = new PlayerStatusDto(
                _playerSerial, _playerStatus.Name,
                _playerStatus.Hits, _playerStatus.MaxHits,
                _playerStatus.Mana, _playerStatus.MaxMana,
                _playerStatus.Stamina, _playerStatus.MaxStamina,
                _playerStatus.Str, _playerStatus.Dex, _playerStatus.Int,
                nextGold, _playerStatus.ArmorRating,
                _playerStatus.Weight, _playerStatus.MaxWeight);
            OnPlayerStatusUpdated?.Invoke(_playerStatus);
            OnOverheadMessage?.Invoke(new OverheadMessageDto(BlacksmithSerial, "Добротно! Обращайся ещё, витязь!", 0x0059));
        }

        public void RequestHotbarAbility(int slotIndex, Serial targetMobile)
        {
            Serial target = targetMobile.IsValid ? targetMobile : GuardianSerial;

            if (slotIndex == 1 || slotIndex == 2)
            {
                int dx = Math.Abs(_currentPosition.X - 2552);
                int dy = Math.Abs(_currentPosition.Y - 499);
                if (dx > 1 || dy > 1)
                {
                    OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "Слишком далеко для удара! Подойдите вплотную (1 клетка).", 0x0022));
                    return;
                }
            }

            switch (slotIndex)
            {
                case 1:
                case 2:
                case 4:
                    ushort dmg = slotIndex == 4 ? (ushort)28 : (ushort)22;
                    _guardianHits = _guardianHits > dmg ? (ushort)(_guardianHits - dmg) : (ushort)80;
                    OnCombatSwing?.Invoke(_playerSerial, target);
                    OnDamageDealt?.Invoke(new CombatDamageDto(target, dmg));
                    OnMobileHitsUpdated?.Invoke(target, _guardianHits, 80);
                    if (slotIndex == 4)
                    {
                        OnVisualEffect?.Invoke(new VisualEffectDto(0, _playerSerial, target, 0x36D4, _currentPosition, new Point3D(2552, 499, 0)));
                    }
                    break;

                case 3:
                    RequestMineAt(_currentPosition, Serial.Zero);
                    break;

                case 5:
                case 6:
                    _playerStatus = new PlayerStatusDto(
                        _playerSerial, _playerStatus.Name,
                        _playerStatus.MaxHits, _playerStatus.MaxHits,
                        _playerStatus.MaxMana, _playerStatus.MaxMana,
                        _playerStatus.MaxStamina, _playerStatus.MaxStamina,
                        _playerStatus.Str, _playerStatus.Dex, _playerStatus.Int,
                        _playerStatus.Gold, _playerStatus.ArmorRating,
                        _playerStatus.Weight, _playerStatus.MaxWeight);
                    OnPlayerStatusUpdated?.Invoke(_playerStatus);
                    OnVisualEffect?.Invoke(new VisualEffectDto(3, _playerSerial, _playerSerial, 0x376A, _currentPosition, _currentPosition));
                    OnOverheadMessage?.Invoke(new OverheadMessageDto(_playerSerial, "+Полное исцеление!", 0x0044));
                    break;
            }
        }
    }
}
