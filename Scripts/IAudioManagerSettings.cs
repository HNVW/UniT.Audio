#nullable enable
namespace UniT.Audio
{
    public interface IAudioManagerSettings
    {
        public AudioSettings MasterSettings { get; }

        public AudioSettings SoundSettings { get; }

        public AudioSettings MusicSettings { get; }
    }
}