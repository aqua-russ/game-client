using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// Снимок мира: свой персонаж и все объекты — у бота (из <see cref="EntityStore"/>) или у сервера (дамп [DumpVisible,
    /// Custom/TheVerge/Parity/VisibleObjectsDump.cs). Формат JSON у обоих один, поэтому снимки сравниваются поле в поле.
    /// </summary>
    public sealed class WorldSnapshot
    {
        public Serial Player { get; set; }

        public List<SnapshotObject> Objects { get; } = new List<SnapshotObject>();

        public static WorldSnapshot FromStore(EntityStore store)
        {
            var snapshot = new WorldSnapshot { Player = store.PlayerSerial };
            foreach (var m in store.Mobiles)
            {
                snapshot.Objects.Add(new SnapshotObject
                {
                    Serial = m.Serial, Kind = "mobile", Place = "world", Graphic = m.Body, Hue = m.Hue, Amount = 1,
                    X = m.Position.X, Y = m.Position.Y, Z = m.Position.Z
                });
            }

            foreach (var item in store.Items)
            {
                snapshot.Objects.Add(FromItem(item));
            }

            return snapshot;
        }

        public static WorldSnapshot Read(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var snapshot = new WorldSnapshot { Player = ParseSerial(root.GetProperty("player").GetString()) };
            foreach (var o in root.GetProperty("objects").EnumerateArray())
            {
                snapshot.Objects.Add(new SnapshotObject
                {
                    Serial = ParseSerial(o.GetProperty("serial").GetString()),
                    Kind = o.GetProperty("kind").GetString() ?? string.Empty,
                    Place = o.GetProperty("place").GetString() ?? string.Empty,
                    Graphic = o.GetProperty("graphic").GetInt32(),
                    Hue = o.GetProperty("hue").GetInt32(),
                    Amount = o.GetProperty("amount").GetInt32(),
                    X = o.GetProperty("x").GetInt32(),
                    Y = o.GetProperty("y").GetInt32(),
                    Z = o.GetProperty("z").GetInt32(),
                    Parent = ParseSerial(o.GetProperty("parent").GetString()),
                    Layer = o.GetProperty("layer").GetInt32()
                });
            }

            return snapshot;
        }

        public static string Write(WorldSnapshot snapshot)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("player", Hex(snapshot.Player));
                writer.WriteStartArray("objects");
                foreach (var o in snapshot.Objects)
                {
                    WriteObject(writer, o);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>Расхождения бота с сервером, по строке на объект и поле; пусто — миры совпадают.</summary>
        public static List<string> Compare(WorldSnapshot bot, WorldSnapshot server) => SnapshotComparison.Compare(bot, server);

        private static SnapshotObject FromItem(Item item)
        {
            var o = new SnapshotObject
            {
                Serial = item.Serial, Kind = "item", Graphic = item.Graphic, Hue = item.Hue, Amount = item.Amount,
                X = item.Position.X, Y = item.Position.Y, Z = item.Position.Z
            };
            switch (item.Place)
            {
                case ItemPlace.World:
                    o.Place = "world";
                    break;
                case ItemPlace.Equipped:
                    o.Place = "equipped";
                    o.Parent = item.Owner?.Serial ?? Serial.Zero;
                    o.Layer = (int)item.Layer;
                    break;
                case ItemPlace.Container:
                    o.Place = "container";
                    o.Parent = item.Container?.Serial ?? Serial.Zero;
                    break;
                default:
                    o.Place = "unplaced";
                    break;
            }

            return o;
        }

        private static void WriteObject(Utf8JsonWriter writer, SnapshotObject o)
        {
            writer.WriteStartObject();
            writer.WriteString("serial", Hex(o.Serial));
            writer.WriteString("kind", o.Kind);
            writer.WriteString("place", o.Place);
            writer.WriteNumber("graphic", o.Graphic);
            writer.WriteNumber("hue", o.Hue);
            writer.WriteNumber("amount", o.Amount);
            writer.WriteNumber("x", o.X);
            writer.WriteNumber("y", o.Y);
            writer.WriteNumber("z", o.Z);
            writer.WriteString("parent", Hex(o.Parent));
            writer.WriteNumber("layer", o.Layer);
            writer.WriteEndObject();
        }

        private static string Hex(Serial serial) => "0x" + serial.Value.ToString("X8", CultureInfo.InvariantCulture);

        private static Serial ParseSerial(string? text) =>
            text != null && text.StartsWith("0x") && uint.TryParse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
                ? new Serial(value)
                : Serial.Zero;
    }
}
