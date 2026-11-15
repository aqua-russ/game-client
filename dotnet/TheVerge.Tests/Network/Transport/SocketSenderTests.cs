using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>
    /// Отправка без блокировки (<see cref="SocketSender"/>, PLAN Стадия 1, п. 4). После Dispose отправка не трогает
    /// освобождённое: Send отказывает без исключения и без «ошибки сети» — закрытие выбрал владелец.
    /// </summary>
    public sealed class SocketSenderTests
    {
        [Test]
        public void SendAfterDispose_RefusedWithoutExceptionOrFailure()
        {
            using (var server = new LocalServer())
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Connect(IPAddress.Loopback, server.Port);
                server.Accept();
                var failures = 0;
                var sender = new SocketSender(socket, _ => failures++);

                sender.Dispose();

                Assert.That(sender.Send(new byte[] { 1, 2, 3 }), Is.False);
                Assert.That(failures, Is.EqualTo(0));
            }
        }

        [Test]
        public void SendBeforeDispose_BytesArrive()
        {
            using (var server = new LocalServer())
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Connect(IPAddress.Loopback, server.Port);
                server.Accept();
                var sender = new SocketSender(socket, _ => { });

                Assert.That(sender.Send(new byte[] { 1, 2, 3 }), Is.True);

                Assert.That(server.ReadExactly(3), Is.EqualTo(new byte[] { 1, 2, 3 }));
                sender.Dispose();
            }
        }
    }
}
