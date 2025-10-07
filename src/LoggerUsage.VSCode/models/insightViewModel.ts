/**
 * Represents a single logging statement insight in the UI
 */
export interface LoggingInsight {
  /** Unique identifier (filePath:line:column) */
  id: string;

  /** The logging method type */
  methodType: 'LoggerExtension' | 'LoggerMessageAttribute' | 'LoggerMessageDefine' | 'BeginScope';

  /** Message template string */
  messageTemplate: string;

  /** Log level (e.g., Information, Warning, Error) */
  logLevel: string | null;

  /** Event ID information */
  eventId: EventIdInfo | null;

  /** Parameter names extracted from template or method signature */
  parameters: string[];

  /** File location */
  location: Location;

  /** Tags/categories for filtering */
  tags: string[];

  /** Data classification information (sensitive data detection) */
  dataClassifications: DataClassification[];

  /** Whether this insight has any inconsistencies */
  hasInconsistencies: boolean;

  /** Inconsistency details if applicable */
  inconsistencies?: ParameterInconsistency[];
}

export interface EventIdInfo {
  id: number | null;
  name: string | null;
}

export interface Location {
  filePath: string;
  startLine: number;
  startColumn: number;
  endLine: number;
  endColumn: number;
}

export interface DataClassification {
  parameterName: string;
  classificationType: string; // e.g., 'PersonalData', 'SensitiveData'
}

export interface ParameterInconsistency {
  type: 'NameMismatch' | 'MissingEventId' | 'SensitiveDataInLog';
  message: string;
  severity: 'Warning' | 'Error';
  location?: Location;
}
