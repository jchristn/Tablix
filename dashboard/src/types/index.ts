export interface DatabaseEntry {
  Id: string;
  Name: string | null;
  Type: string;
  Hostname: string | null;
  Port: number | null;
  User: string | null;
  Password: string | null;
  HasUser?: boolean;
  HasPassword?: boolean;
  DatabaseName: string | null;
  Schema: string | null;
  Filename: string | null;
  AllowedQueries: string[];
  Context: string | null;
}

export interface ColumnDetail {
  ColumnName: string;
  DataType: string;
  IsNullable: boolean;
  IsPrimaryKey: boolean;
  DefaultValue: string | null;
  MaxLength: number | null;
}

export interface ForeignKeyDetail {
  ConstraintName: string;
  ColumnName: string;
  ReferencedTable: string;
  ReferencedColumn: string;
}

export interface IndexDetail {
  IndexName: string;
  Columns: string[];
  IsUnique: boolean;
}

export interface TableDetail {
  TableName: string;
  SchemaName: string;
  Columns: ColumnDetail[];
  ForeignKeys: ForeignKeyDetail[];
  Indexes: IndexDetail[];
}

export interface DatabaseDetail {
  DatabaseId: string;
  Type: string;
  DatabaseName: string;
  Schema: string | null;
  Context: string | null;
  Tables: TableDetail[];
  CrawledUtc: string | null;
  IsCrawled: boolean;
  CrawlError: string | null;
  Name?: string | null;
  Hostname?: string | null;
  Port?: number | null;
  HasUser?: boolean;
  HasPassword?: boolean;
  Filename?: string | null;
  AllowedQueries?: string[];
}

export interface CrawlProgressEvent {
  EventType: string;
  Stage: string;
  DatabaseId: string;
  Message: string;
  Percent: number;
  Terminal: boolean;
  TotalMs: number;
  TableName: string | null;
  TableIndex: number | null;
  TableCount: number | null;
  RelationshipCount: number | null;
  Error: string | null;
  Detail: DatabaseDetail | null;
}

export interface ContextUpdateRequest {
  Context: string | null;
  Mode: 'replace' | 'append';
}

export interface ContextUpdateResponse {
  Success: boolean;
  DatabaseId: string;
  Context: string | null;
  Mode: string;
}

export interface BuildContextRequest {
  ProviderId: string | null;
  Prompt: string | null;
}

export interface BuildContextResponse {
  Success: boolean;
  DatabaseId: string;
  ProviderId: string;
  Context: string | null;
  Model: string | null;
  Telemetry: ChatTelemetry | null;
  Error: string | null;
}

export interface ModelProviderSummary {
  Id: string;
  Name: string | null;
  Type: string;
  Endpoint: string | null;
  Model: string | null;
  Enabled: boolean;
  DefaultStreaming: boolean;
  HasApiKey: boolean;
}

export interface ChatOptionsResponse {
  Enabled: boolean;
  DefaultProviderId: string | null;
  DefaultStreaming: boolean;
  Databases: DatabaseSummary[];
  Providers: ModelProviderSummary[];
}

export interface DatabaseSummary {
  Id: string;
  Name: string | null;
  Type: string;
  DatabaseName: string | null;
  Schema: string | null;
  Filename: string | null;
  AllowedQueries: string[];
  Context: string | null;
  IsCrawled: boolean;
  CrawlError: string | null;
  HasUser: boolean;
  HasPassword: boolean;
}

export interface ChatMessageRequest {
  Role: string;
  Content: string;
}

export interface ChatRequest {
  DatabaseId: string;
  ProviderId: string;
  Messages: ChatMessageRequest[];
  Streaming: boolean | null;
}

export interface ChatTelemetry {
  TimeToFirstTokenMs: number | null;
  TotalStreamingTimeMs: number | null;
  InputTokens: number | null;
  OutputTokens: number | null;
  TotalTokens: number | null;
  EstimatedTokens: boolean;
}

export interface ChatToolCall {
  Id: string;
  Name: string;
  Arguments: string | null;
  Result: string | null;
  Error: string | null;
  Success: boolean;
  TotalMs: number;
}

export interface ChatResponseResult {
  Success: boolean;
  DatabaseId: string;
  ProviderId: string;
  Model: string | null;
  Message: string | null;
  Telemetry: ChatTelemetry | null;
  ToolCalls: ChatToolCall[];
  Error: string | null;
}

export interface ChatStreamEvent {
  EventType: string;
  Delta: string | null;
  Message: string | null;
  DatabaseId: string | null;
  ProviderId: string | null;
  Model: string | null;
  Telemetry: ChatTelemetry | null;
  ToolCall: ChatToolCall | null;
  Done: boolean;
  Error: string | null;
}

export interface RestSettings {
  Hostname: string;
  Port: number;
  Ssl: boolean;
  McpPort: number;
}

export interface SyslogServerSettings {
  Hostname: string;
  Port: number;
}

export interface LoggingSettings {
  Servers: SyslogServerSettings[];
  ConsoleLogging: boolean;
  FileLogging: boolean;
  LogDirectory: string;
  LogFilename: string;
  MinimumSeverity: number;
  EnableColors: boolean;
}

export interface ChatToolSettings {
  Enabled: boolean;
  AllowReadOnlyQueries: boolean;
  AllowContextUpdates: boolean;
  MaxToolIterations: number;
  MaxToolCalls: number;
  ToolTimeoutMs: number;
  MaxToolOutputCharacters: number;
}

export interface ModelProviderRead {
  Id: string;
  Name: string | null;
  Type: string;
  Endpoint: string | null;
  ApiKey: string | null;
  HasApiKey: boolean;
  Model: string | null;
  SystemPrompt: string | null;
  Enabled: boolean;
  DefaultStreaming: boolean;
  Temperature: number | null;
  TopP: number | null;
  MaxTokens: number | null;
  RequestTimeoutMs: number;
}

export interface ModelProviderUpdate extends ModelProviderRead {
  ClearApiKey: boolean;
}

export interface ChatSettingsRead {
  Enabled: boolean;
  DefaultProviderId: string | null;
  DefaultStreaming: boolean;
  SystemPrompt: string | null;
  MaxContextTables: number;
  Tools: ChatToolSettings;
  Providers: ModelProviderRead[];
}

export interface ChatSettingsUpdate extends Omit<ChatSettingsRead, 'Providers'> {
  Providers: ModelProviderUpdate[];
}

export interface SettingsReadResponse {
  Rest: RestSettings;
  Logging: LoggingSettings;
  ApiKeys: string[];
  Chat: ChatSettingsRead;
  RestartRequiredPaths: string[];
}

export interface SettingsUpdateRequest {
  Rest: RestSettings;
  Logging: LoggingSettings;
  ApiKeys: string[];
  Chat: ChatSettingsUpdate;
}

export interface EnumerationResult<T> {
  Success: boolean;
  MaxResults: number;
  Skip: number;
  TotalRecords: number;
  RecordsRemaining: number;
  EndOfResults: boolean;
  TotalMs: number;
  Objects: T[];
}

export interface QueryRequest {
  Query: string;
}

export interface QueryResult {
  Success: boolean;
  DatabaseId: string;
  RowsReturned: number;
  TotalMs: number;
  Data: SerializableDataTable | null;
  Error: string | null;
}

export interface SerializableDataTable {
  Name: string;
  Columns: SerializableColumn[];
  Rows: Record<string, unknown>[];
}

export interface SerializableColumn {
  Name: string;
  Type: string;
}

export interface ApiErrorResponse {
  Error: string;
  Message: string;
  StatusCode: number;
  Description: string | null;
}

export interface RuntimeConfig {
  serverUrl?: string;
  displayServerUrl?: string;
}
