export interface DatabaseEntry {
  Id: string;
  Name: string | null;
  Type: string;
  Hostname: string | null;
  Port: number | null;
  User: string | null;
  Password: string | null;
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
  serverUrl: string;
}
