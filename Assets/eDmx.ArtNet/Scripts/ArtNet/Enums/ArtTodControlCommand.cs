namespace ArtNet.Enums
{
    /// <summary>
    /// ArtNet OpTodControl Command 命令
    /// </summary>
    public enum ArtTodControlCommand
    {
        /// <summary>
        /// No action
        /// </summary>
        AtcNone = 0,
        /// <summary>
        /// Node 节点刷新它的 TOD 并且促使它可以被发现
        /// </summary>
        AtcFlush = 1
    }
}
