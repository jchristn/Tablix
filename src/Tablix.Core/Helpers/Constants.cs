namespace Tablix.Core.Helpers
{
    /// <summary>
    /// Application constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Product name.
        /// </summary>
        public static readonly string ProductName = "Tablix";

        /// <summary>
        /// Product version.
        /// </summary>
        public static readonly string ProductVersion = "0.1.0";

        /// <summary>
        /// Default settings filename.
        /// </summary>
        public static readonly string SettingsFilename = "tablix.json";

        /// <summary>
        /// JSON content type.
        /// </summary>
        public static readonly string JsonContentType = "application/json";

        /// <summary>
        /// Authorization header name.
        /// </summary>
        public static readonly string AuthorizationHeader = "Authorization";

        /// <summary>
        /// Bearer token prefix.
        /// </summary>
        public static readonly string BearerPrefix = "Bearer ";

        /// <summary>
        /// ASCII art logo.
        /// </summary>
        public static readonly string Logo = "\n" +
            @"   _        _     _ _" + "\n" +
            @"  | |_ __ _| |__ | (_)_  __" + "\n" +
            @"  | __/ _` | '_ \| | \ \/ /" + "\n" +
            @"  | || (_| | |_) | | |>  < " + "\n" +
            @"   \__\__,_|_.__/|_|_/_/\_\";
    }
}
