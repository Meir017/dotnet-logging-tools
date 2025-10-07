import { AnalysisSummary } from './ipcMessages';
import { LoggingInsight } from './insightViewModel';
import { FilterState } from './filterState';

/**
 * Messages sent from extension → webview
 */
export type ExtensionToWebviewMessage =
  | { command: 'updateInsights'; insights: LoggingInsight[]; summary: AnalysisSummary }
  | { command: 'updateFilters'; filters: FilterState }
  | { command: 'showError'; message: string; details?: string }
  | { command: 'updateTheme'; theme: 'light' | 'dark' | 'high-contrast' };

/**
 * Messages sent from webview → extension
 */
export type WebviewToExtensionMessage =
  | { command: 'applyFilters'; filters: FilterState }
  | { command: 'navigateToInsight'; insightId: string }
  | { command: 'exportResults'; format: 'json' | 'csv' | 'markdown' }
  | { command: 'refreshAnalysis' };
