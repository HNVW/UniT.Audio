#nullable enable
namespace UniT.Audio.DI
{
    using VContainer;

    public static class AudioManagerVContainer
    {
        public static void RegisterAudioManager(this IContainerBuilder builder)
        {
            builder.Register<AudioManager>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}