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
  TableId: string | null;
  TableName: string;
  SchemaName: string;
  Context: string | null;
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

export interface BuildTableContextRequest {
  ProviderId: string | null;
  Prompt: string | null;
  TableIds: string[];
}

export interface BuildTableContextResponse {
  Success: boolean;
  DatabaseId: string;
  ProviderId: string;
  Model: string | null;
  Objects: TableContextRead[];
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
  SupportsNativeToolCalls: boolean;
  UseNativeToolCalls: boolean;
  SupportsStrictJson: boolean;
  ToolCapabilityNote: string | null;
  MaxConcurrentRequests: number;
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
  PreferNativeToolCalls?: boolean | null;
  FallbackWhenNativeToolNotCalled?: boolean | null;
}

export interface ChatPromptPreviewResponse {
  Success: boolean;
  DatabaseId: string;
  ProviderId: string;
  Model: string | null;
  SystemPrompt: string | null;
  ContextPrompt: string | null;
  SystemPromptCharacters: number;
  ContextPromptCharacters: number;
  SystemPromptEstimatedTokens: number;
  ContextPromptEstimatedTokens: number;
  ConversationMessages: number;
  Error: string | null;
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
  Phase: string | null;
}

export interface VerifiedAnswer {
  State: 'verified' | 'partial' | 'blocked' | 'ambiguous' | string;
  Summary: string | null;
  Sql: string | null;
  ToolCallId: string | null;
  RowsReturned: number | null;
  Evidence: string[];
  Error: string | null;
}

export interface AmbiguitySignal {
  Term: string | null;
  Reason: string | null;
  Question: string | null;
  Candidates: string[];
}

export interface ChatResponseResult {
  Success: boolean;
  DatabaseId: string;
  ProviderId: string;
  Model: string | null;
  Message: string | null;
  Telemetry: ChatTelemetry | null;
  ToolCalls: ChatToolCall[];
  VerifiedAnswer: VerifiedAnswer | null;
  Ambiguities: AmbiguitySignal[];
  ExecutionPath: string | null;
  CapabilityNotice: string | null;
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
  VerifiedAnswer: VerifiedAnswer | null;
  Ambiguities: AmbiguitySignal[];
  ExecutionPath: string | null;
  CapabilityNotice: string | null;
  Done: boolean;
  Error: string | null;
}

export interface DomainEntity {
  TableId: string | null;
  SchemaName: string | null;
  TableName: string | null;
  Role: string | null;
  Summary: string | null;
  KeyColumns: string[];
  HasContext: boolean;
}

export interface DomainIntelligence {
  Summary: string | null;
  Entities: DomainEntity[];
  Workflows: string[];
  Metrics: string[];
  CommonFilters: string[];
  FreshnessColumns: string[];
  TenantColumns: string[];
  SoftDeleteColumns: string[];
}

export interface RelationshipDetail {
  FromSchema: string | null;
  FromTable: string | null;
  FromColumn: string | null;
  ToSchema: string | null;
  ToTable: string | null;
  ToColumn: string | null;
  ConstraintName: string | null;
  Source: string;
  Confidence: number;
}

export interface ContextQualitySignal {
  Key: string | null;
  Severity: string | null;
  Message: string | null;
  Recommendation: string | null;
}

export interface ContextQualityScore {
  Score: number;
  Label: string | null;
  TablesWithContext: number;
  TotalTables: number;
  DeclaredRelationships: number;
  InferredRelationships: number;
  Signals: ContextQualitySignal[];
}

export interface AgentPackResponse {
  Success: boolean;
  DatabaseId: string | null;
  GeneratedUtc: string;
  Markdown: string | null;
  Instructions: string[];
  SuggestedQuestions: string[];
}

export interface DatabaseIntelligenceResponse {
  Success: boolean;
  DatabaseId: string | null;
  Domain: DomainIntelligence | null;
  Relationships: RelationshipDetail[];
  Ambiguities: AmbiguitySignal[];
  ContextQuality: ContextQualityScore | null;
  AgentPack: AgentPackResponse | null;
  TotalMs: number;
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

export interface PromptProcessingSettings {
  Enabled: boolean;
  PreferNativeToolCalls: boolean;
  RequireExecutionForDataRequests: boolean;
  AllowSqlOnlyByExplicitRequest: boolean;
  FallbackWhenNativeToolNotCalled: boolean;
  RetryAfterSchemaRefresh: boolean;
  MaxNativeToolIterations: number;
  MaxPlanningAttempts: number;
  PlannerTemperature: number;
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
  SupportsNativeToolCalls: boolean;
  UseNativeToolCalls: boolean;
  SupportsStrictJson: boolean;
  ToolCapabilityNote: string | null;
  Temperature: number | null;
  TopP: number | null;
  MaxTokens: number | null;
  RequestTimeoutMs: number;
  MaxConcurrentRequests: number;
}

export interface ModelProviderUpdate extends ModelProviderRead {
  ClearApiKey: boolean;
}

export interface ProviderConnectivityTestRequest {
  Provider: ModelProviderUpdate;
}

export interface ProviderConnectivityTestResponse {
  Success: boolean;
  ProviderId: string | null;
  Model: string | null;
  Message: string | null;
  Error: string | null;
  TotalMs: number;
}

export interface DatabaseConnectivityTestRequest {
  Database: DatabaseEntry;
}

export interface DatabaseConnectivityTestResponse {
  Success: boolean;
  DatabaseId: string | null;
  Message: string | null;
  Error: string | null;
  TotalMs: number;
}

export interface TableContextRead {
  Id: string | null;
  DatabaseId: string | null;
  TableId: string | null;
  SchemaName: string | null;
  TableName: string | null;
  Context: string | null;
  Source: string | null;
  UpdatedUtc: string;
}

export interface TableContextUpdateRequest {
  Context: string | null;
  Mode: 'replace' | 'append';
  Source: string | null;
}

export interface PersistenceDatabaseSettings {
  Type: string;
  Filename: string;
}

export interface PersistenceHealthRead {
  Type: string;
  Filename: string | null;
  ResolvedFilename: string | null;
  Exists: boolean;
  CanOpen: boolean;
  Error: string | null;
}

export interface SetupStateRead {
  Id: string;
  Status: string;
  CurrentStep: string | null;
  SelectedProviderId: string | null;
  SelectedDatabaseId: string | null;
  CompletedUtc: string | null;
  DismissedUtc: string | null;
  UpdatedUtc: string;
  ShouldShowWizard: boolean;
}

export interface SetupStateUpdateRequest {
  Status: string;
  CurrentStep: string | null;
  SelectedProviderId: string | null;
  SelectedDatabaseId: string | null;
}

export interface ChatSettingsRead {
  Enabled: boolean;
  DefaultProviderId: string | null;
  DefaultStreaming: boolean;
  SystemPrompt: string | null;
  MaxContextTables: number;
  Tools: ChatToolSettings;
  PromptProcessing: PromptProcessingSettings;
}

export interface ChatSettingsUpdate extends ChatSettingsRead {}

export interface SettingsReadResponse {
  Persistence: PersistenceDatabaseSettings;
  PersistenceHealth: PersistenceHealthRead;
  Rest: RestSettings;
  Logging: LoggingSettings;
  ApiKeys: string[];
  Chat: ChatSettingsRead;
  RestartRequiredPaths: string[];
}

export interface SettingsUpdateRequest {
  Persistence: PersistenceDatabaseSettings;
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
