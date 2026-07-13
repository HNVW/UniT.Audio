#nullable enable
namespace UniT.Audio.Default
{
    using System;
    using System.Collections.Generic;
    using Extensions;
    using Logging;
    using ResourceManagement;
    using UnityEngine;
    using UnityEngine.Scripting;
    using ILogger = Logging.ILogger;
    using Object = UnityEngine.Object;

    public sealed class AudioManager : IAudioManager, IDisposable
    {
        #region Constructor

        private readonly ILogger logger;

        private readonly AudioSettings masterSettings = new();
        private readonly GameObject sourceContainer = new GameObject(nameof(AudioManager)).DontDestroyOnLoad();
        private readonly Stack<AudioSource> sourcePool = new();
        private readonly AudioPool soundPool;
        private readonly AudioPool musicPool;

        [Preserve]
        public AudioManager(IAssetManager assetManager, ILoggerManager loggerManager)
        {
            this.logger = loggerManager.GetLogger(this);

            this.soundPool = new(this.masterSettings, assetManager, this.sourceContainer, this.sourcePool, this.logger);
            this.musicPool = new(this.masterSettings, assetManager, this.sourceContainer, this.sourcePool, this.logger);

            this.logger.Debug("Constructed");
        }

        #endregion

        IAudioSettings IAudioManager.MasterSettings => this.masterSettings;

        IAudioPool IAudioManager.Sound => this.soundPool;

        IAudioPool IAudioManager.Music => this.musicPool;

        void IDisposable.Dispose()
        {
            ((IDisposable)this.soundPool).Dispose();
            ((IDisposable)this.musicPool).Dispose();
            this.sourcePool.Clear();
            if (this.sourceContainer) Object.Destroy(this.sourceContainer);
            this.logger.Debug("Disposed");
        }
    }
}