#nullable enable
namespace UniT.Audio
{
    using System;

    public sealed class AudioSettings
    {
        public event Action? VolumeChanged;

        public event Action? MuteChanged;

        public float Volume
        {
            get => this.volume;
            set
            {
                if (value is < 0 or > 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 1");
                }
                this.volume = value;
                this.VolumeChanged?.Invoke();
            }
        }

        public bool Mute
        {
            get => this.mute;
            set
            {
                this.mute = value;
                this.MuteChanged?.Invoke();
            }
        }

        private float volume = 1;
        private bool  mute   = false;
    }
}