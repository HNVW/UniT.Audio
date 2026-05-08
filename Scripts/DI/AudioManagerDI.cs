#if UNIT_DI
#nullable enable
namespace UniT.Audio.DI
{
    using UniT.DI;
    using UniT.Logging.DI;
    using UniT.ResourceManagement.DI;

    public static class AudioManagerDI
    {
        public static void AddAudioManager(this DependencyContainer container)
        {
            if (container.Contains<IAudioManager>()) return;
            container.AddLoggerManager();
            container.AddAssetsManager();
            container.AddInterfaces<AudioManager>();
        }
    }
}
#endif