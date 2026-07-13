#nullable enable
namespace UniT.Audio.DI
{
    using InternalDI;

    public static class AudioManagerInternalDI
    {
        public static void AddAudioManager(this DependencyContainer container)
        {
            container.AddInterfaces<AudioManager>();
        }
    }
}