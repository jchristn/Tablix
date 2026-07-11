namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Persistence database health details.
    /// </summary>
    public class PersistenceHealthRead
    {
        /// <summary>
        /// Persistence database type.
        /// </summary>
        public TablixPersistenceDatabaseTypeEnum Type { get; set; } = TablixPersistenceDatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Configured persistence database filename.
        /// </summary>
        public string Filename { get; set; } = null;

        /// <summary>
        /// Resolved absolute persistence database filename.
        /// </summary>
        public string ResolvedFilename { get; set; } = null;

        /// <summary>
        /// Whether the persistence database initialized successfully.
        /// </summary>
        public bool Healthy { get; set; } = false;

        /// <summary>
        /// Health message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PersistenceHealthRead()
        {
        }
    }
}
