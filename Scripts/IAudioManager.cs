#nullable enable
namespace UniT.Audio
{
    public interface IAudioManager
    {
        public IAudioSettings MasterSettings { get; }

        public IAudioPool Sound { get; }

        public IAudioPool Music { get; }
    }
}