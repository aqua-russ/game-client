using System;
using NUnit.Framework;
using TheVerge.Core.Primitives;

namespace TheVerge.Tests.Core.Primitives
{
    /// <summary>
    /// Номер слоя приходит в пакетах 0x2E, 0x78, 0x25 и уходит в 0x13, поэтому все слои сервера должны быть у клиента
    /// с теми же номерами: Projects/Server/Items/Layer.cs. Слои 0x1A–0x1C — товары торговца (Стадия 7, вендоры),
    /// 0x1D — банк. Имена — как на сервере, кроме Unused_xF (у нас UnusedXF: без подчёркивания по AGENTS §3.3).
    /// </summary>
    public sealed class LayerTests
    {
        [TestCase("Invalid", 0x00)]
        [TestCase("OneHanded", 0x01)]
        [TestCase("TwoHanded", 0x02)]
        [TestCase("Shoes", 0x03)]
        [TestCase("Pants", 0x04)]
        [TestCase("Shirt", 0x05)]
        [TestCase("Helm", 0x06)]
        [TestCase("Gloves", 0x07)]
        [TestCase("Ring", 0x08)]
        [TestCase("Talisman", 0x09)]
        [TestCase("Neck", 0x0A)]
        [TestCase("Hair", 0x0B)]
        [TestCase("Waist", 0x0C)]
        [TestCase("InnerTorso", 0x0D)]
        [TestCase("Bracelet", 0x0E)]
        [TestCase("UnusedXF", 0x0F)]
        [TestCase("FacialHair", 0x10)]
        [TestCase("MiddleTorso", 0x11)]
        [TestCase("Earrings", 0x12)]
        [TestCase("Arms", 0x13)]
        [TestCase("Cloak", 0x14)]
        [TestCase("Backpack", 0x15)]
        [TestCase("OuterTorso", 0x16)]
        [TestCase("OuterLegs", 0x17)]
        [TestCase("InnerLegs", 0x18)]
        [TestCase("Mount", 0x19)]
        [TestCase("ShopBuy", 0x1A)]
        [TestCase("ShopResale", 0x1B)]
        [TestCase("ShopSell", 0x1C)]
        [TestCase("Bank", 0x1D)]
        public void Layer_ExistsWithServerNumber(string serverName, int serverValue)
        {
            bool exists = Enum.TryParse(serverName, out Layer layer);

            Assert.That(exists, Is.True, "у клиента нет слоя " + serverName);
            Assert.That((int)layer, Is.EqualTo(serverValue));
        }

        [Test]
        public void Layer_HasNoValuesBeyondServer()
        {
            foreach (Layer layer in Enum.GetValues(typeof(Layer)))
            {
                Assert.That((int)layer, Is.InRange(0x00, 0x1D), layer.ToString());
            }
        }
    }
}
