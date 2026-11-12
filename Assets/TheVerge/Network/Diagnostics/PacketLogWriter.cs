using System;
using System.IO;
using TheVerge.Core.Ports;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Запись лога пакетов (PLAN Стадия 1, п. 12; формат — <see cref="PacketLogFormat"/>). Одна строка на пакет или событие
    /// соединения; пароль в 0x80 и 0x91 заменяется звёздочками (<see cref="PacketLogSecrets"/>). Куда писать (файл, память)
    /// решает тот, кто создаёт запись: Bootstrap или бот. Дошли до предела размера — одна строка «#truncated» и
    /// предупреждение; не удалось записать — ошибка в лог. В обоих случаях запись останавливается, а клиент работает
    /// дальше. Вызывается только из главного потока.
    /// </summary>
    public sealed class PacketLogWriter
    {
        private readonly TextWriter _output;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly long _maxChars;
        private readonly long _startMs;
        private readonly PacketLogLine _line = new PacketLogLine();
        private long _written;
        private bool _failed;
        private bool _unflushed;

        /// <param name="maxChars">Предел размера лога в символах (для текста лога это почти байты).</param>
        public PacketLogWriter(TextWriter output, IClock clock, ILog log, long maxChars)
        {
            _output = output;
            _clock = clock;
            _log = log;
            _maxChars = maxChars;
            _startMs = clock.NowMs;
            _line.Append(PacketLogFormat.VersionLine);
            Commit();
        }

        /// <summary>Запись остановлена: достигнут предел размера или не удалось записать.</summary>
        public bool IsStopped { get; private set; }

        /// <summary>Строка заголовка «#ключ значение»: когда начат лог, кто пишет. Читатель её пропускает.</summary>
        public void Header(string key, string value)
        {
            if (!Begin(false))
            {
                return;
            }

            _line.Append(PacketLogFormat.Comment);
            _line.AppendText(key);
            _line.Append(PacketLogFormat.Separator);
            _line.AppendText(value);
            Commit();
        }

        /// <param name="sent">Пакет передан в сокет; false — соединения не было, пакет не ушёл.</param>
        public void Packet(PacketDirection direction, ReadOnlySpan<byte> packet, bool sent)
        {
            if (packet.IsEmpty)
            {
                throw new ArgumentException("Пустой пакет в логе пакетов: ошибка кода клиента.", nameof(packet));
            }

            if (!Begin(true))
            {
                return;
            }

            PacketLogPacketLine.Append(_line, direction, packet, sent);
            Commit();
        }

        public void Connected()
        {
            if (Begin(true))
            {
                _line.Append(PacketLogFormat.Connected);
                Commit();
            }
        }

        public void Closed(ConnectionCloseReason reason, string detail)
        {
            if (!Begin(true))
            {
                return;
            }

            _line.AppendField(PacketLogFormat.Closed);
            _line.AppendField(reason.ToString());
            _line.AppendText(detail);
            Commit();
        }

        /// <summary>
        /// Сбросить накопленное в файл (в конце тика), чтобы при сбое программы лог не потерялся. Если с прошлого сброса
        /// ничего не записано, файл не трогается: тик без пакетов ничего не стоит.
        /// </summary>
        public void Flush()
        {
            if (_failed || !_unflushed)
            {
                return;
            }

            try
            {
                _output.Flush();
                _unflushed = false;
            }
            catch (IOException e)
            {
                Fail(e);
            }
            catch (ObjectDisposedException e)
            {
                Fail(e);
            }
        }

        private long ElapsedMs => Math.Max(0, _clock.NowMs - _startMs);

        // Строка пакета и события начинается с времени, строка заголовка — с «#».
        private bool Begin(bool withTime)
        {
            if (IsStopped)
            {
                return false;
            }

            _line.Clear();
            if (withTime)
            {
                _line.AppendNumber(ElapsedMs);
                _line.Append(PacketLogFormat.Separator);
            }

            return true;
        }

        private void Commit()
        {
            _line.Append('\n');
            if (_written + _line.Length > _maxChars)
            {
                Truncate();
                return;
            }

            Write();
            _written += _line.Length;
        }

        private void Truncate()
        {
            _line.Clear();
            _line.Append(PacketLogFormat.Comment);
            _line.AppendField(PacketLogFormat.Truncated);
            _line.AppendNumber(ElapsedMs);
            _line.Append('\n');
            Write();
            IsStopped = true;
            _log.Warning("Лог пакетов дошёл до предела " + _maxChars + " символов: запись остановлена, клиент работает дальше.");
        }

        private void Write()
        {
            try
            {
                _output.Write(_line.Chars, 0, _line.Length);
                _unflushed = true;
            }
            catch (IOException e)
            {
                Fail(e);
            }
            catch (ObjectDisposedException e)
            {
                Fail(e);
            }
        }

        private void Fail(Exception e)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            IsStopped = true;
            _log.Error("Лог пакетов остановлен: не удалось записать (" + e.Message + "). Клиент работает дальше.");
        }
    }
}
