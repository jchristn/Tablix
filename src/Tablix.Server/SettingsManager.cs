namespace Tablix.Server
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Helpers;
    using Tablix.Core.Settings;

    /// <summary>
    /// Thread-safe manager for loading, saving, and modifying the settings file.
    /// </summary>
    public class SettingsManager
    {
        #region Public-Members

        /// <summary>
        /// Current settings.
        /// </summary>
        public TablixSettings Settings
        {
            get
            {
                _Semaphore.Wait();
                try { return _Settings; }
                finally { _Semaphore.Release(); }
            }
        }

        /// <summary>
        /// Settings filename.
        /// </summary>
        public string Filename
        {
            get { return _Filename; }
        }

        #endregion

        #region Private-Members

        private readonly string _Filename;
        private TablixSettings _Settings;
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="filename">Path to the settings JSON file.</param>
        public SettingsManager(string filename)
        {
            _Filename = filename ?? throw new ArgumentNullException(nameof(filename));
            _Settings = LoadOrCreate();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Reload settings from disk.
        /// </summary>
        public void Reload()
        {
            _Semaphore.Wait();
            try
            {
                _Settings = LoadOrCreate();
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Replace current settings and persist them.
        /// </summary>
        /// <param name="settings">New settings.</param>
        public void UpdateSettings(TablixSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _Semaphore.Wait();
            try
            {
                _Settings = settings;
                Save();
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        #endregion

        #region Private-Methods

        private TablixSettings LoadOrCreate()
        {
            if (File.Exists(_Filename))
            {
                string json = File.ReadAllText(_Filename, Encoding.UTF8);
                TablixSettings settings = Serializer.DeserializeJson<TablixSettings>(json);
                if (settings != null) return settings;
            }

            TablixSettings defaults = new TablixSettings();
            string defaultJson = Serializer.SerializeJson(defaults, true);
            File.WriteAllText(_Filename, defaultJson, Encoding.UTF8);
            return defaults;
        }

        private void Save()
        {
            string json = Serializer.SerializeJson(_Settings, true);
            File.WriteAllText(_Filename, json, Encoding.UTF8);
        }

        #endregion
    }
}
