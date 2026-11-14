using TheVerge.Core.World;

namespace TheVerge.Core.Status
{
    /// <summary>Часть полного 0x11 после здоровья: пол, выносливость, мана, числа и питомцы.</summary>
    public readonly struct StatusSheet
    {
        public StatusSheet(bool female, Vital stam, Vital mana, StatusAttributes attributes, int followers, int followersMax)
        {
            Female = female;
            Stam = stam;
            Mana = mana;
            Attributes = attributes;
            Followers = followers;
            FollowersMax = followersMax;
        }

        public bool Female { get; }
        public Vital Stam { get; }
        public Vital Mana { get; }
        public StatusAttributes Attributes { get; }
        public int Followers { get; }
        public int FollowersMax { get; }
    }
}
