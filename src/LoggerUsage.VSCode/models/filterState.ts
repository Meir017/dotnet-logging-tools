/**
 * Tracks user-applied filters in the UI
 */
export interface FilterState {
  /** Selected log levels (empty = all) */
  logLevels: string[];

  /** Selected method types (empty = all) */
  methodTypes: ('LoggerExtension' | 'LoggerMessageAttribute' | 'LoggerMessageDefine' | 'BeginScope')[];

  /** Search query for message templates */
  searchQuery: string;

  /** Show only entries with inconsistencies */
  showInconsistenciesOnly: boolean;

  /** Selected tags (empty = all) */
  tags: string[];

  /** Selected files/projects (empty = all) */
  filePaths: string[];
}

/**
 * Default filter state
 */
export const DEFAULT_FILTER_STATE: FilterState = {
  logLevels: [],
  methodTypes: [],
  searchQuery: '',
  showInconsistenciesOnly: false,
  tags: [],
  filePaths: []
};

/**
 * Validates filter state
 */
export function isValidFilterState(filter: FilterState): boolean {
  return (
    Array.isArray(filter.logLevels) &&
    Array.isArray(filter.methodTypes) &&
    typeof filter.searchQuery === 'string' &&
    typeof filter.showInconsistenciesOnly === 'boolean' &&
    Array.isArray(filter.tags) &&
    Array.isArray(filter.filePaths)
  );
}
