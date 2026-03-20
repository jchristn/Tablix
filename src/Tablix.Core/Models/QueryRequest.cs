namespace Tablix.Core.Models
{
    /// <summary>
    /// Request body for executing a SQL query.
    /// </summary>
    public class QueryRequest
    {
        #region Public-Members

        /// <summary>
        /// SQL query to execute.
        /// </summary>
        public string Query { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QueryRequest()
        {
        }

        #endregion
    }
}
