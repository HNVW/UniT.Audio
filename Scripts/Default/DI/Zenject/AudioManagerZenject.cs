#nullable enable
namespace UniT.Audio.DI
{
    using Zenject;

    public static class AudioManagerZenject
    {
        public static void BindAudioManager(this DiContainer container)
        {
            container.BindInterfacesTo<AudioManager>().AsSingle();
        }
    }
}