import { LoggingInsight } from './insightViewModel';

/**
 * IPC message types for communication between extension and C# bridge
 */

/** Request to analyze a workspace/solution */
export interface AnalysisRequest {
  command: 'analyze';
  workspacePath: string;
  solutionPath: string | null;
  excludePatterns?: string[];
}

/** Request to re-analyze a single file (incremental) */
export interface IncrementalAnalysisRequest {
  command: 'analyzeFile';
  filePath: string;
  solutionPath: string;
}

/** Request to ping bridge (handshake) */
export interface PingRequest {
  command: 'ping';
}

/** Request to shutdown bridge */
export interface ShutdownRequest {
  command: 'shutdown';
}

export type BridgeRequest = AnalysisRequest | IncrementalAnalysisRequest | PingRequest | ShutdownRequest;

/** Progress update from bridge */
export interface AnalysisProgress {
  status: 'progress';
  percentage: number;
  message: string;
  currentFile?: string;
}

/** Successful analysis response */
export interface AnalysisSuccessResponse {
  status: 'success';
  result: {
    insights: LoggingInsight[];
    summary: AnalysisSummary;
  };
}

/** Error response */
export interface AnalysisErrorResponse {
  status: 'error';
  message: string;
  details: string;
  errorCode?: string;
}

/** Ready response (handshake confirmation) */
export interface ReadyResponse {
  status: 'ready';
  version: string;
}

/** Summary statistics */
export interface AnalysisSummary {
  totalInsights: number;
  byMethodType: Record<string, number>;
  byLogLevel: Record<string, number>;
  inconsistenciesCount: number;
  filesAnalyzed: number;
  analysisTimeMs: number;
}

export type AnalysisResponse = AnalysisSuccessResponse | AnalysisErrorResponse | AnalysisProgress | ReadyResponse;
