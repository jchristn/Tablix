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
        /// Add a database entry.
        /// </summary>
        /// <param name="entry">Database entry to add.</param>
        public void AddDatabase(DatabaseEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            _Semaphore.Wait();
            try
            {
                if (_Settings.Databases.Any(d => String.Equals(d.Id, entry.Id, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("A database with ID '" + entry.Id + "' already exists.");

                _Settings.Databases.Add(entry);
                Save();
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Update an existing database entry.
        /// </summary>
        /// <param name="entry">Updated database entry.</param>
        public void UpdateDatabase(DatabaseEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            _Semaphore.Wait();
            try
            {
                int index = _Settings.Databases.FindIndex(d => String.Equals(d.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                    throw new KeyNotFoundException("Database with ID '" + entry.Id + "' not found.");

                _Settings.Databases[index] = entry;
                Save();
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Delete a database entry by ID.
        /// </summary>
        /// <param name="id">Database entry ID.</param>
        public void DeleteDatabase(string id)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            _Semaphore.Wait();
            try
            {
                int removed = _Settings.Databases.RemoveAll(d => String.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed == 0)
                    throw new KeyNotFoundException("Database with ID '" + id + "' not found.");

                Save();
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        /// <summary>
        /// Get a database entry by ID.
        /// </summary>
        /// <param name="id">Database entry ID.</param>
        /// <returns>Database entry or null.</returns>
        public DatabaseEntry GetDatabase(string id)
        {
            _Semaphore.Wait();
            try
            {
                return _Settings.Databases.FirstOrDefault(d => String.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
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
