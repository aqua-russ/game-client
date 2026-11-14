namespace TheVerge.Core.MapView
{
    /// <summary>Откуда взялся блок отладочной карты.</summary>
    public enum SceneKind : byte
    {
        Land = 0,
        Static = 1,
        MultiPart = 2,
        Item = 3,
        Mobile = 4
    }
}
