namespace TheVerge.Presentation.UI.Movement
{
    /// <summary>
    /// Числа одной строки экрана с прошлого показа: текст строки собирается заново, только когда они изменились (без новой
    /// памяти на кадр, пока всё стоит).
    /// </summary>
    internal sealed class ChangedNumbers
    {
        private readonly long[] _values = new long[4];
        private bool _seen;

        public bool Changed(long a, long b = 0, long c = 0, long d = 0)
        {
            if (_seen && _values[0] == a && _values[1] == b && _values[2] == c && _values[3] == d)
            {
                return false;
            }

            _seen = true;
            _values[0] = a;
            _values[1] = b;
            _values[2] = c;
            _values[3] = d;
            return true;
        }
    }
}
