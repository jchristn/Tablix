namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Classified user prompt intent for chat execution policy.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PromptIntentTypeEnum
    {
        /// <summary>
        /// Intent could not be determined.
        /// </summary>
        Unknown,

        /// <summary>
        /// User asked for data or an answer based on database rows.
        /// </summary>
        DataAnswerRequest,

        /// <summary>
        /// User explicitly asked for SQL text only.
        /// </summary>
        SqlOnlyRequest,

        /// <summary>
        /// User asked about schema, tables, columns, keys, or relationships.
        /// </summary>
        SchemaQuestion,

        /// <summary>
        /// User asked about persisted context.
        /// </summary>
        ContextQuestion,

        /// <summary>
        /// User is discussing the selected database without an execution request.
        /// </summary>
        DatabaseConversation,

        /// <summary>
        /// User explicitly requested a write operation.
        /// </summary>
        ExplicitWriteRequest
    }
}
