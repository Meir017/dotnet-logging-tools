import * as vscode from 'vscode';
import { AnalysisProgress, AnalysisSuccessResponse } from '../models/ipcMessages';

/**
 * Event data for analysis started
 */
export interface AnalysisStartedEvent {
  workspacePath: string;
  solutionPath: string | null;
  timestamp: number;
}

/**
 * Event data for analysis progress
 */
export interface AnalysisProgressEvent extends AnalysisProgress {
  timestamp: number;
}

/**
 * Event data for analysis complete
 */
export interface AnalysisCompleteEvent {
  result: AnalysisSuccessResponse;
  timestamp: number;
  duration: number;
}

/**
 * Event data for analysis error
 */
export interface AnalysisErrorEvent {
  error: Error;
  message: string;
  timestamp: number;
}

/**
 * Central event emitter for analysis lifecycle events.
 * This allows UI components to react to analysis events without tight coupling.
 */
class AnalysisEventEmitter {
  private readonly _onAnalysisStarted = new vscode.EventEmitter<AnalysisStartedEvent>();
  private readonly _onAnalysisProgress = new vscode.EventEmitter<AnalysisProgressEvent>();
  private readonly _onAnalysisComplete = new vscode.EventEmitter<AnalysisCompleteEvent>();
  private readonly _onAnalysisError = new vscode.EventEmitter<AnalysisErrorEvent>();

  /**
   * Event fired when analysis starts
   */
  public readonly onAnalysisStarted: vscode.Event<AnalysisStartedEvent> = this._onAnalysisStarted.event;

  /**
   * Event fired when analysis progress is reported
   */
  public readonly onAnalysisProgress: vscode.Event<AnalysisProgressEvent> = this._onAnalysisProgress.event;

  /**
   * Event fired when analysis completes successfully
   */
  public readonly onAnalysisComplete: vscode.Event<AnalysisCompleteEvent> = this._onAnalysisComplete.event;

  /**
   * Event fired when analysis fails
   */
  public readonly onAnalysisError: vscode.Event<AnalysisErrorEvent> = this._onAnalysisError.event;

  /**
   * Fires the analysis started event
   */
  public fireAnalysisStarted(workspacePath: string, solutionPath: string | null): void {
    this._onAnalysisStarted.fire({
      workspacePath,
      solutionPath,
      timestamp: Date.now()
    });
  }

  /**
   * Fires the analysis progress event
   */
  public fireAnalysisProgress(progress: AnalysisProgress): void {
    this._onAnalysisProgress.fire({
      ...progress,
      timestamp: Date.now()
    });
  }

  /**
   * Fires the analysis complete event
   */
  public fireAnalysisComplete(result: AnalysisSuccessResponse, startTime: number): void {
    this._onAnalysisComplete.fire({
      result,
      timestamp: Date.now(),
      duration: Date.now() - startTime
    });
  }

  /**
   * Fires the analysis error event
   */
  public fireAnalysisError(error: Error, message?: string): void {
    this._onAnalysisError.fire({
      error,
      message: message || error.message,
      timestamp: Date.now()
    });
  }

  /**
   * Disposes all event emitters
   */
  public dispose(): void {
    this._onAnalysisStarted.dispose();
    this._onAnalysisProgress.dispose();
    this._onAnalysisComplete.dispose();
    this._onAnalysisError.dispose();
  }
}

/**
 * Singleton instance of the analysis event emitter.
 * Import this in components that need to listen to or fire analysis events.
 */
export const analysisEvents = new AnalysisEventEmitter();
