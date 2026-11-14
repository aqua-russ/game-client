namespace TheVerge.Core.World
{
    /// <summary>
    /// Здоровье, мана или выносливость так, как их прислал сервер: текущее и максимум. Своему персонажу сервер шлёт точные
    /// числа, чужим — долю: максимум 100 (OutgoingMobilePackets.WriteAttribute, normalize, AttributeMaximum = 100).
    /// </summary>
    public readonly struct Vital
    {
        public Vital(int current, int maximum)
        {
            Current = current;
            Maximum = maximum;
        }

        public int Current { get; }
        public int Maximum { get; }

        public override string ToString() => Current + "/" + Maximum;
    }
}
