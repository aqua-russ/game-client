// Сгенерировано tools/gen/packet_table.py из docs/protocol/packets.csv. Руками не править: поправьте CSV и
// запустите генератор (docs/protocol/packets.md); tools/check/all.sh сверяет этот файл со свежей генерацией.
namespace TheVerge.Network.Framing
{
    /// <summary>
    /// Длины и имена пакетов протокола для клиента 7.0.95.0 из матрицы docs/protocol/packets.csv. Номера или
    /// подкоманды нет в матрице — возвращается значение по умолчанию: «неизвестен».
    /// </summary>
    public static class PacketTable
    {
        /// <summary>Пакет по номеру — для нарезки потока, обработчиков и логов.</summary>
        public static PacketInfo Get(PacketDirection direction, byte id) =>
            direction == PacketDirection.ServerToClient ? FromServer(id) : FromClient(id);

        /// <summary>
        /// Подкоманда пакета 0xBF, 0xD7, 0x12, 0xF0 или 0xF1. Для номера без подкоманд — «неизвестен».
        /// Подкоманда 0xBF и 0xD7 занимает 2 байта, поэтому параметр не byte: 0x0119 не должна стать 0x19.
        /// </summary>
        public static PacketInfo Get(PacketDirection direction, byte id, ushort subCommand) =>
            direction == PacketDirection.ServerToClient ? FromServer(id, subCommand) : FromClient(id, subCommand);

        private static PacketInfo FromServer(byte id) => id switch
        {
            0x0B => Fixed(7, "Damage"),
            0x11 => Variable("MobileStatus"),
            0x15 => Fixed(9, "FollowMessage"), // не используется
            0x17 => Variable("Healthbar"),
            0x1A => Variable("WorldItemOld"), // отложено
            0x1B => Fixed(37, "LoginConfirm"),
            0x1C => Variable("AsciiMessage"),
            0x1D => Fixed(5, "RemoveEntity"),
            0x20 => Fixed(19, "MobileUpdate"),
            0x21 => Fixed(8, "MoveReject"),
            0x22 => Fixed(3, "MoveAck"),
            0x23 => Fixed(26, "DragEffect"),
            0x24 => Fixed(9, "DisplayContainer"),
            0x25 => Fixed(21, "ContainerItem"),
            0x27 => Fixed(2, "LiftReject"),
            0x2C => Fixed(2, "DeathStatus"),
            0x2D => Fixed(17, "MobileAttributes"),
            0x2E => Fixed(15, "EquipUpdate"),
            0x2F => Fixed(10, "Swing"),
            0x38 => Fixed(7, "PathfindMessage"), // не используется
            0x3A => Variable("SkillsUpdate"),
            0x3B => Variable("VendorClose"),
            0x3C => Variable("ContainerContent"),
            0x4E => Fixed(6, "PersonalLight"),
            0x4F => Fixed(2, "GlobalLight"),
            0x51 => Variable("ShardStatsCompact"), // не используется
            0x53 => Fixed(2, "PopupMessage"),
            0x54 => Fixed(12, "PlaySound"),
            0x55 => Fixed(1, "LoginComplete"),
            0x56 => Fixed(11, "MapPinCommand"),
            0x5B => Fixed(4, "CurrentTime"),
            0x65 => Fixed(4, "Weather"),
            0x66 => Variable("BookPages"),
            0x6C => Fixed(19, "TargetRequest"),
            0x6D => Fixed(3, "PlayMusic"),
            0x6E => Fixed(14, "MobileAnimation"),
            0x6F => Variable("SecureTrade"),
            0x70 => Fixed(28, "ScreenEffect"),
            0x71 => Variable("BulletinBoard"),
            0x72 => Fixed(5, "WarMode"),
            0x73 => Fixed(2, "PingAck"),
            0x74 => Variable("VendorBuyList"),
            0x76 => Fixed(16, "ServerChange"),
            0x77 => Fixed(17, "MobileMoving"),
            0x78 => Variable("MobileIncoming"),
            0x7B => Fixed(2, "Sequence"), // не используется
            0x7C => Variable("Menu"),
            0x81 => Variable("ChangeCharacter"), // не используется
            0x82 => Fixed(2, "LoginRejected"),
            0x85 => Fixed(2, "DeleteResult"),
            0x86 => Variable("CharacterListUpdate"),
            0x88 => Fixed(66, "Paperdoll"),
            0x89 => Variable("CorpseEquipment"),
            0x8B => Variable("SignGump"), // не используется
            0x8C => Fixed(11, "ServerRedirect"),
            0x90 => Fixed(19, "MapDetailsOld"), // не используется
            0x95 => Fixed(9, "HuePicker"),
            0x97 => Fixed(2, "MovePlayer"), // не используется
            0x98 => Variable("MobileName"),
            0x99 => Fixed(30, "MultiTargetRequest"),
            0x9E => Variable("VendorSellList"),
            0xA1 => Fixed(9, "MobileHits"),
            0xA2 => Fixed(9, "MobileMana"),
            0xA3 => Fixed(9, "MobileStamina"),
            0xA5 => Variable("LaunchBrowser"), // отложено
            0xA6 => Variable("TipWindow"), // не используется
            0xA8 => Variable("ServerList"),
            0xA9 => Variable("CharacterList"),
            0xAA => Fixed(5, "ChangeCombatant"),
            0xAE => Variable("UnicodeMessage"),
            0xAF => Fixed(13, "DeathAnimation"),
            0xB2 => Variable("ChatMessage"), // отложено
            0xB7 => Variable("HelpResponse"), // не используется
            0xB8 => Variable("Profile"),
            0xB9 => Fixed(5, "SupportedFeatures"),
            0xBA => Fixed(10, "QuestArrow"),
            0xBC => Fixed(3, "SeasonChange"),
            0xBD => Variable("ClientVersionRequest"),
            0xBE => Variable("AssistantVersionRequest"), // не используется
            0xBF => Variable("ExtendedCommand"),
            0xC0 => Fixed(36, "HuedEffect"),
            0xC1 => Variable("LocalizedMessage"),
            0xC2 => Variable("UnicodePrompt"),
            0xC6 => Fixed(1, "InvalidMap"), // не используется
            0xC7 => Fixed(49, "ParticleEffect"),
            0xC8 => Fixed(2, "ViewRange"),
            0xCC => Variable("LocalizedAffixMessage"),
            0xD1 => Fixed(2, "LogoutAck"),
            0xD4 => Variable("BookHeader"),
            0xD6 => Variable("PropertyList"), // не используется
            0xD8 => Variable("HouseDesignDetailed"), // не используется
            0xDA => Variable("Mahjong"),
            0xDC => Fixed(9, "PropertyListHash"), // не используется
            0xDD => Variable("CompressedGump"),
            0xDF => Variable("BuffIcon"), // отложено
            0xE2 => Fixed(10, "NewMobileAnimation"), // не используется
            0xF0 => Variable("AssistantProtocol"),
            0xF2 => Fixed(25, "TimeSyncResponse"), // не используется
            0xF3 => Fixed(26, "WorldObject"),
            0xF5 => Fixed(21, "MapDetails"),
            0xF6 => Variable("BoatMove"), // не используется
            0xF7 => Variable("PacketContainer"), // не используется
            _ => default,
        };

        private static PacketInfo FromServer(byte id, ushort subCommand) => (id, subCommand) switch
        {
            (0xBF, 0x04) => Variable("CloseGump"),
            (0xBF, 0x06) => Variable("Party"),
            (0xBF, 0x08) => Variable("MapChange"),
            (0xBF, 0x10) => Variable("EquipmentInfo"),
            (0xBF, 0x14) => Variable("ContextMenu"),
            (0xBF, 0x17) => Variable("HelpTopic"), // отложено
            (0xBF, 0x18) => Variable("MapPatches"),
            (0xBF, 0x19) => Variable("ExtendedStats"),
            (0xBF, 0x1B) => Variable("NewSpellbookContent"), // не используется
            (0xBF, 0x1D) => Variable("HouseDesignState"), // не используется
            (0xBF, 0x20) => Variable("HouseCustomization"), // не используется
            (0xBF, 0x21) => Variable("ClearWeaponAbility"), // не используется
            (0xBF, 0x22) => Variable("DamageOld"), // не используется
            (0xBF, 0x25) => Variable("ToggleSpecialAbility"), // не используется
            (0xBF, 0x26) => Variable("SpeedControl"),
            (0xBF, 0x2A) => Variable("RaceChange"), // не используется
            (0xF0, 0x01) => Variable("PartyLocations"),
            (0xF0, 0x02) => Variable("GuildLocations"),
            (0xF0, 0xFE) => Variable("AssistantHandshake"), // не используется
            _ => default,
        };

        private static PacketInfo FromClient(byte id) => id switch
        {
            0x00 => Fixed(104, "CreateCharacterOld"), // не используется
            0x01 => Fixed(5, "Disconnect"), // не используется
            0x02 => Fixed(7, "MoveRequest"),
            0x03 => Variable("AsciiSpeech"),
            0x05 => Fixed(5, "AttackRequest"),
            0x06 => Fixed(5, "DoubleClick"),
            0x07 => Fixed(7, "LiftRequest"),
            0x08 => Fixed(15, "DropRequest"),
            0x09 => Fixed(5, "SingleClick"),
            0x12 => Variable("TextCommand"),
            0x13 => Fixed(10, "EquipRequest"),
            0x22 => Fixed(3, "Resynchronize"),
            0x2C => Fixed(2, "DeathStatusResponse"), // не используется
            0x34 => Fixed(10, "MobileQuery"),
            0x3A => Variable("SkillLockChange"),
            0x3B => Variable("VendorBuyReply"),
            0x56 => Fixed(11, "MapPinCommand"),
            0x5D => Fixed(73, "PlayCharacter"),
            0x66 => Variable("BookPagesChange"),
            0x6C => Fixed(19, "TargetResponse"),
            0x6F => Variable("SecureTradeAction"),
            0x71 => Variable("BulletinBoardRequest"),
            0x72 => Fixed(5, "WarModeRequest"),
            0x73 => Fixed(2, "Ping"),
            0x75 => Fixed(35, "RenameRequest"),
            0x7D => Fixed(13, "MenuResponse"),
            0x80 => Fixed(62, "AccountLogin"),
            0x83 => Fixed(39, "DeleteCharacter"),
            0x91 => Fixed(65, "GameLogin"),
            0x93 => Fixed(99, "BookHeaderOld"), // не используется
            0x95 => Fixed(9, "HuePickerResponse"),
            0x98 => Variable("MobileNameRequest"),
            0x9A => Variable("AsciiPromptResponse"), // не используется
            0x9B => Fixed(258, "HelpRequest"),
            0x9F => Variable("VendorSellReply"),
            0xA0 => Fixed(3, "SelectServer"),
            0xA4 => Fixed(149, "SystemInfo"), // не используется
            0xA7 => Fixed(4, "TipWindowRequest"), // не используется
            0xAD => Variable("UnicodeSpeech"),
            0xB1 => Variable("GumpResponse"),
            0xB3 => Variable("ChatAction"), // отложено
            0xB5 => Fixed(64, "OpenChatWindow"), // отложено
            0xB6 => Fixed(9, "ObjectHelpRequest"),
            0xB8 => Variable("ProfileRequest"),
            0xBB => Fixed(9, "UltimaMessenger"), // не используется
            0xBD => Variable("ClientVersion"),
            0xBE => Variable("AssistantVersion"), // не используется
            0xBF => Variable("ExtendedCommand"),
            0xC2 => Variable("UnicodePromptResponse"),
            0xC8 => Fixed(2, "ViewRange"),
            0xD0 => Variable("ConfigurationFile"), // не используется
            0xD1 => Fixed(2, "Logout"),
            0xD4 => Variable("BookHeaderChange"),
            0xD6 => Variable("BatchQueryProperties"), // не используется
            0xD7 => Variable("EncodedCommand"), // не используется
            0xD9 => Fixed(268, "HardwareInfo"), // не используется
            0xDA => Variable("MahjongAction"),
            0xE1 => Variable("ClientType"), // не используется
            0xEC => Variable("EquipMacro"), // не используется
            0xED => Variable("UnequipMacro"), // не используется
            0xEF => Fixed(21, "LoginSeed"),
            0xF0 => Variable("AssistantProtocol"),
            0xF1 => Variable("FreeshardProtocol"), // не используется
            0xF4 => Variable("CrashReport"), // не используется
            0xF8 => Fixed(106, "CreateCharacter"),
            0xFA => Fixed(1, "UltimaStoreOpen"), // не используется
            0xFB => Fixed(2, "PublicHouseContent"), // не используется
            _ => default,
        };

        private static PacketInfo FromClient(byte id, ushort subCommand) => (id, subCommand) switch
        {
            (0x12, 0x24) => Variable("UseSkill"),
            (0x12, 0x27) => Variable("CastSpellFromBook"),
            (0x12, 0x2F) => Variable("OldScrollDoubleClick"), // не используется
            (0x12, 0x43) => Variable("OpenSpellbook"),
            (0x12, 0x56) => Variable("CastSpellFromMacro"),
            (0x12, 0x58) => Variable("OpenDoor"),
            (0x12, 0xC7) => Variable("AnimateEmote"),
            (0x12, 0xF4) => Variable("InvokeVirtue"), // отложено
            (0xBF, 0x05) => Variable("ScreenSize"), // не используется
            (0xBF, 0x06) => Variable("PartyCommand"),
            (0xBF, 0x07) => Variable("QuestArrowClick"),
            (0xBF, 0x09) => Variable("DisarmRequest"),
            (0xBF, 0x0A) => Variable("StunRequest"),
            (0xBF, 0x0B) => Variable("Language"),
            (0xBF, 0x0C) => Variable("CloseStatus"),
            (0xBF, 0x0E) => Variable("Animate"),
            (0xBF, 0x0F) => Variable("Unknown0F"), // не используется
            (0xBF, 0x10) => Variable("QueryProperties"), // не используется
            (0xBF, 0x13) => Variable("ContextMenuRequest"),
            (0xBF, 0x15) => Variable("ContextMenuResponse"),
            (0xBF, 0x1A) => Variable("StatLockChange"),
            (0xBF, 0x1C) => Variable("CastSpell"),
            (0xBF, 0x1E) => Variable("HouseDesignQuery"), // не используется
            (0xBF, 0x24) => Variable("Unknown24"), // не используется
            (0xBF, 0x2A) => Variable("RaceChangeReply"), // не используется
            (0xBF, 0x2C) => Variable("BandageTarget"),
            (0xBF, 0x2D) => Variable("TargetedSpell"),
            (0xBF, 0x2E) => Variable("TargetedSkill"),
            (0xBF, 0x30) => Variable("TargetByResource"),
            (0xBF, 0x32) => Variable("ToggleFlying"), // не используется
            (0xD7, 0x02) => Variable("DesignerBackup"), // не используется
            (0xD7, 0x03) => Variable("DesignerRestore"), // не используется
            (0xD7, 0x04) => Variable("DesignerCommit"), // не используется
            (0xD7, 0x05) => Variable("DesignerDelete"), // не используется
            (0xD7, 0x06) => Variable("DesignerBuild"), // не используется
            (0xD7, 0x0C) => Variable("DesignerClose"), // не используется
            (0xD7, 0x0D) => Variable("DesignerStairs"), // не используется
            (0xD7, 0x0E) => Variable("DesignerSync"), // не используется
            (0xD7, 0x10) => Variable("DesignerClear"), // не используется
            (0xD7, 0x12) => Variable("DesignerLevel"), // не используется
            (0xD7, 0x13) => Variable("DesignerRoof"), // не используется
            (0xD7, 0x14) => Variable("DesignerRoofDelete"), // не используется
            (0xD7, 0x19) => Variable("SetWeaponAbility"), // не используется
            (0xD7, 0x1A) => Variable("DesignerRevert"), // не используется
            (0xD7, 0x28) => Variable("GuildButton"), // не используется
            (0xD7, 0x32) => Variable("QuestButton"), // не используется
            (0xF0, 0x00) => Variable("QueryPartyLocations"),
            (0xF0, 0x01) => Variable("QueryGuildLocations"),
            (0xF0, 0xFF) => Variable("AssistantHandshakeReply"), // не используется
            (0xF1, 0xFE) => Variable("QueryShardStatsCompact"), // не используется
            (0xF1, 0xFF) => Variable("QueryShardStatsExtended"), // не используется
            _ => default,
        };

        private static PacketInfo Fixed(int length, string name) => new PacketInfo(length, name);

        private static PacketInfo Variable(string name) => new PacketInfo(PacketInfo.VariableLength, name);
    }
}
