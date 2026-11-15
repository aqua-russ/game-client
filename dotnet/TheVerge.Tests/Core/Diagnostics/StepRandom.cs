using System;
using System.Collections.Generic;

namespace TheVerge.Tests.Core.Diagnostics
{
    /// <summary>
    /// «Случайные» числа по списку: каждый вызов Next(min, max) берёт следующее значение (по кругу) и проверяет, что оно в
    /// пределах [min, max). Для точных проверок разброса задержки.
    /// </summary>
    internal sealed class StepRandom : Random
    {
        private readonly Queue<int> _values;

        public StepRandom(params int[] values)
        {
            _values = new Queue<int>(values);
        }

        public override int Next(int minValue, int maxValue)
        {
            int value = _values.Dequeue();
            _values.Enqueue(value);
            if (value < minValue || value >= maxValue)
            {
                throw new InvalidOperationException("значение " + value + " вне [" + minValue + ", " + maxValue + ")");
            }

            return value;
        }
    }
}
