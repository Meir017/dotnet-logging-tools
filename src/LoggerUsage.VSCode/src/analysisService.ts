import * as vscode from 'vscode';
import * as childProcess from 'child_process';
import * as path from 'path';
import * as fs from 'fs';
import {
    AnalysisRequest,
    IncrementalAnalysisRequest,
    BridgeRequest,
    AnalysisResponse,
    AnalysisSuccessResponse,
    AnalysisErrorResponse,
    AnalysisProgress,
    ReadyResponse
} from '../models/ipcMessages';
import { analysisEvents } from './analysisEvents';
import { checkDotNetSdk, getDotNetDownloadUrl } from './utils/dotnetDetector';

/**
 * Progress callback for analysis updates
 */
export type ProgressCallback = (progress: AnalysisProgress) => void;

/**
 * Service for managing C# Bridge process and IPC communication
 */
export class AnalysisService implements vscode.Disposable {
    private bridgeProcess: childProcess.ChildProcess | null = null;
    private outputChannel: vscode.OutputChannel;
    private isReady: boolean = false;
    private readyPromise: Promise<void> | null = null;
    private pendingResponses: Map<string, {
        resolve: (value: AnalysisSuccessResponse) => void;
        reject: (reason: any) => void;
        onProgress?: ProgressCallback;
    }> = new Map();
    private currentRequestId: number = 0;
    private lineBuffer: string = '';

    // Error handling state
    private crashCount: number = 0;
    private maxRetries: number = 3;
    private lastCrashTime: number = 0;
    private crashResetInterval: number = 60000; // Reset crash count after 1 minute
    private isShuttingDown: boolean = false;

    constructor(
        private context: vscode.ExtensionContext,
        outputChannel?: vscode.OutputChannel
    ) {
        this.outputChannel = outputChannel ?? vscode.window.createOutputChannel('Logger Usage Analysis');
    }

    /**
     * Starts the C# Bridge process
     */
    public async startBridge(): Promise<void> {
        if (this.bridgeProcess) {
            return this.readyPromise ?? Promise.resolve();
        }

        this.readyPromise = this.spawnBridgeProcess();
        return this.readyPromise;
    }

    /**
     * Analyzes an entire workspace
     */
    public async analyzeWorkspace(
        workspacePath: string,
        solutionPath: string | null,
        excludePatterns?: string[],
        onProgress?: ProgressCallback,
        cancellationToken?: vscode.CancellationToken
    ): Promise<AnalysisSuccessResponse> {
        // Check if .NET SDK is installed before proceeding
        const sdkCheck = await checkDotNetSdk();
        if (!sdkCheck.installed) {
            const errorMessage = '.NET SDK not found. Please install .NET 10 SDK or later.';
            this.outputChannel.appendLine(`[ERROR] ${errorMessage}`);
            if (sdkCheck.error) {
                this.outputChannel.appendLine(`Details: ${sdkCheck.error}`);
            }

            // Show error notification with download option
            const choice = await vscode.window.showErrorMessage(
                errorMessage,
                'Download .NET',
                'Show Details'
            );

            if (choice === 'Download .NET') {
                vscode.env.openExternal(vscode.Uri.parse(getDotNetDownloadUrl()));
            } else if (choice === 'Show Details') {
                this.outputChannel.show();
            }

            throw new Error(errorMessage);
        }

        this.outputChannel.appendLine(`[INFO] .NET SDK detected: ${sdkCheck.version}`);

        await this.ensureBridgeReady();

        const startTime = Date.now();

        // Emit analysis started event
        analysisEvents.fireAnalysisStarted(workspacePath, solutionPath);

        const request: AnalysisRequest = {
            command: 'analyze',
            workspacePath,
            solutionPath,
            excludePatterns
        };

        try {
            const result = await this.sendRequest(request, onProgress, cancellationToken);

            // Emit analysis complete event
            analysisEvents.fireAnalysisComplete(result, startTime);

            // Check for compilation warnings
            if (result.result.summary.warningsCount && result.result.summary.warningsCount > 0) {
                vscode.window.showWarningMessage(
                    `Analysis completed with ${result.result.summary.warningsCount} compilation warning(s). Results may be incomplete.`,
                    'Show Output'
                ).then(choice => {
                    if (choice === 'Show Output') {
                        this.outputChannel.show();
                    }
                });
            }

            return result;
        } catch (error) {
            // Emit analysis error event
            const err = error instanceof Error ? error : new Error(String(error));
            analysisEvents.fireAnalysisError(err);
            throw error;
        }
    }

    /**
     * Analyzes a single file incrementally
     */
    public async analyzeFile(
        filePath: string,
        solutionPath: string,
        onProgress?: ProgressCallback,
        cancellationToken?: vscode.CancellationToken
    ): Promise<AnalysisSuccessResponse> {
        await this.ensureBridgeReady();

        const startTime = Date.now();

        // Emit analysis started event (single file)
        analysisEvents.fireAnalysisStarted(filePath, solutionPath);

        const request: IncrementalAnalysisRequest = {
            command: 'analyzeFile',
            filePath,
            solutionPath
        };

        try {
            const result = await this.sendRequest(request, onProgress, cancellationToken);

            // Emit analysis complete event
            analysisEvents.fireAnalysisComplete(result, startTime);

            return result;
        } catch (error) {
            // Emit analysis error event
            const err = error instanceof Error ? error : new Error(String(error));
            analysisEvents.fireAnalysisError(err);
            throw error;
        }
    }

    /**
     * Disposes the service and closes the bridge process
     */
    public dispose(): void {
        this.isShuttingDown = true;

        if (this.bridgeProcess) {
            try {
                // Send shutdown command
                const shutdownRequest: BridgeRequest = { command: 'shutdown' };
                this.writeToBridge(shutdownRequest);

                // Give it a moment to shutdown gracefully
                setTimeout(() => {
                    if (this.bridgeProcess && !this.bridgeProcess.killed) {
                        this.bridgeProcess.kill();
                    }
                }, 1000);
            } catch (error) {
                this.outputChannel.appendLine(`Error during shutdown: ${error}`);
                if (this.bridgeProcess && !this.bridgeProcess.killed) {
                    this.bridgeProcess.kill();
                }
            }

            this.bridgeProcess = null;
            this.isReady = false;
            this.readyPromise = null;
        }
    }

    /**
     * Spawns the C# Bridge process and establishes communication
     */
    private async spawnBridgeProcess(): Promise<void> {
        const bridgeExecutable = this.findBridgeExecutable();

        if (!bridgeExecutable) {
            const error = 'C# Bridge executable not found. Please ensure the extension is properly installed.';
            this.outputChannel.appendLine(error);
            throw new Error(error);
        }

        this.outputChannel.appendLine(`Starting C# Bridge: ${bridgeExecutable}`);

        try {
            this.bridgeProcess = childProcess.spawn(bridgeExecutable, [], {
                stdio: ['pipe', 'pipe', 'pipe'],
                windowsHide: true
            });

            // Handle stdout - JSON responses
            if (this.bridgeProcess.stdout) {
                this.bridgeProcess.stdout.setEncoding('utf8');
                this.bridgeProcess.stdout.on('data', (data: string) => {
                    this.handleStdout(data);
                });
            }

            // Handle stderr - debug logs
            if (this.bridgeProcess.stderr) {
                this.bridgeProcess.stderr.setEncoding('utf8');
                this.bridgeProcess.stderr.on('data', (data: string) => {
                    this.outputChannel.appendLine(`[Bridge stderr] ${data}`);
                });
            }

            // Handle process exit
            this.bridgeProcess.on('exit', (code, signal) => {
                this.outputChannel.appendLine(`Bridge process exited with code ${code}, signal ${signal}`);
                this.handleBridgeExit(code, signal);
            });

            // Handle process errors
            this.bridgeProcess.on('error', (error) => {
                this.outputChannel.appendLine(`Bridge process error: ${error.message}`);
                this.handleBridgeError(error);
            });

            // Send handshake ping
            await this.sendHandshake();

            this.outputChannel.appendLine('C# Bridge ready');

        } catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            this.outputChannel.appendLine(`Failed to start bridge: ${message}`);
            throw new Error(`Failed to start C# Bridge: ${message}`);
        }
    }

    /**
     * Sends handshake ping and waits for ready response
     */
    private async sendHandshake(): Promise<void> {
        return new Promise((resolve, reject) => {
            const timeout = setTimeout(() => {
                reject(new Error('Bridge handshake timeout'));
            }, 10000); // 10 second timeout

            const pingRequest: BridgeRequest = { command: 'ping' };

            // Set up a temporary handler for the ready response
            const originalHandler = this.handleResponse.bind(this);
            this.handleResponse = (response: AnalysisResponse) => {
                if (response.status === 'ready') {
                    clearTimeout(timeout);
                    this.isReady = true;
                    this.handleResponse = originalHandler;
                    this.outputChannel.appendLine(`Bridge ready (version: ${(response as ReadyResponse).version})`);
                    resolve();
                } else {
                    // Not ready yet, keep waiting
                    originalHandler(response);
                }
            };

            this.writeToBridge(pingRequest);
        });
    }

    /**
     * Sends a request to the bridge and returns a promise for the response
     */
    private sendRequest(
        request: BridgeRequest,
        onProgress?: ProgressCallback,
        cancellationToken?: vscode.CancellationToken
    ): Promise<AnalysisSuccessResponse> {
        const requestId = `req_${++this.currentRequestId}`;

        return new Promise((resolve, reject) => {
            // Store the promise handlers
            this.pendingResponses.set(requestId, { resolve, reject, onProgress });

            // Handle cancellation
            if (cancellationToken) {
                cancellationToken.onCancellationRequested(() => {
                    this.pendingResponses.delete(requestId);
                    // Note: We don't have a cancel command in the protocol yet
                    // The C# bridge would need to support cancellation tokens
                    reject(new Error('Analysis cancelled'));
                });
            }

            // Send the request
            this.writeToBridge(request);
        });
    }

    /**
     * Writes a request to the bridge's stdin
     */
    private writeToBridge(request: BridgeRequest): void {
        if (!this.bridgeProcess || !this.bridgeProcess.stdin) {
            throw new Error('Bridge process not available');
        }

        const json = JSON.stringify(request);
        this.outputChannel.appendLine(`> ${json}`);
        this.bridgeProcess.stdin.write(json + '\n');
    }

    /**
     * Handles stdout data from the bridge (line-buffered JSON)
     */
    private handleStdout(data: string): void {
        this.lineBuffer += data;

        // Process complete lines
        let newlineIndex: number;
        while ((newlineIndex = this.lineBuffer.indexOf('\n')) !== -1) {
            const line = this.lineBuffer.substring(0, newlineIndex).trim();
            this.lineBuffer = this.lineBuffer.substring(newlineIndex + 1);

            if (line) {
                try {
                    const response = JSON.parse(line) as AnalysisResponse;
                    this.outputChannel.appendLine(`< ${line.substring(0, 200)}${line.length > 200 ? '...' : ''}`);
                    this.handleResponse(response);
                } catch (error) {
                    this.outputChannel.appendLine(`Failed to parse JSON: ${line}`);
                    this.outputChannel.appendLine(`Error: ${error}`);
                }
            }
        }
    }

    /**
     * Handles a response from the bridge
     */
    private handleResponse(response: AnalysisResponse): void {
        // Find the pending request (for now, we only support one at a time)
        const pendingEntry = Array.from(this.pendingResponses.values())[0];

        if (!pendingEntry) {
            // No pending request, might be unsolicited progress or ready message
            return;
        }

        switch (response.status) {
            case 'progress':
                const progressResponse = response as AnalysisProgress;

                // Emit analysis progress event
                analysisEvents.fireAnalysisProgress(progressResponse);

                if (pendingEntry.onProgress) {
                    pendingEntry.onProgress(progressResponse);
                }
                break;

            case 'success':
                // Remove from pending and resolve
                const requestId = Array.from(this.pendingResponses.keys())[0];
                this.pendingResponses.delete(requestId);
                pendingEntry.resolve(response as AnalysisSuccessResponse);
                break;

            case 'error':
                // Remove from pending and reject
                const errorRequestId = Array.from(this.pendingResponses.keys())[0];
                this.pendingResponses.delete(errorRequestId);
                const errorResponse = response as AnalysisErrorResponse;
                const error = new Error(`Analysis failed: ${errorResponse.message}\n${errorResponse.details}`);
                pendingEntry.reject(error);

                // Show user-friendly error message based on error code
                this.showErrorNotification(errorResponse);
                break;

            case 'ready':
                // Handled by handshake
                break;
        }
    }

    /**
     * Handles bridge process exit
     */
    private handleBridgeExit(code: number | null, signal: string | null): void {
        const now = Date.now();

        // Reset crash count if enough time has passed since last crash
        if (now - this.lastCrashTime > this.crashResetInterval) {
            this.crashCount = 0;
        }

        this.bridgeProcess = null;
        this.isReady = false;
        this.readyPromise = null;

        // Reject all pending requests
        for (const [requestId, pending] of this.pendingResponses.entries()) {
            pending.reject(new Error(`Bridge process exited unexpectedly (code: ${code}, signal: ${signal})`));
            this.pendingResponses.delete(requestId);
        }

        // If this was an intentional shutdown, don't show errors
        if (this.isShuttingDown || code === 0) {
            this.outputChannel.appendLine(`Bridge process exited normally (code: ${code})`);
            return;
        }

        // Record the crash
        this.crashCount++;
        this.lastCrashTime = now;

        this.outputChannel.appendLine(`Bridge process crashed (exit code: ${code}, signal: ${signal})`);
        this.outputChannel.appendLine(`Crash count: ${this.crashCount}/${this.maxRetries}`);

        // Emit error event
        analysisEvents.fireAnalysisError(
            new Error(`Bridge process crashed (exit code: ${code})`),
            `The analysis bridge crashed unexpectedly. Crash ${this.crashCount}/${this.maxRetries}`
        );

        // Check if we should offer retry
        if (this.crashCount < this.maxRetries) {
            // Offer retry
            vscode.window.showErrorMessage(
                `Logger Usage bridge process crashed (attempt ${this.crashCount}/${this.maxRetries})`,
                'Retry',
                'Show Logs'
            ).then(choice => {
                if (choice === 'Retry') {
                    this.retryAfterCrash();
                } else if (choice === 'Show Logs') {
                    this.outputChannel.show();
                }
            });
        } else {
            // Max retries exceeded
            vscode.window.showErrorMessage(
                `Logger Usage bridge process crashed ${this.crashCount} times. Please check the logs for details.`,
                'Show Logs',
                'Reset'
            ).then(choice => {
                if (choice === 'Show Logs') {
                    this.outputChannel.show();
                } else if (choice === 'Reset') {
                    this.resetCrashCount();
                }
            });
        }
    }

    /**
     * Retries starting the bridge after a crash
     */
    private async retryAfterCrash(): Promise<void> {
        this.outputChannel.appendLine('Retrying bridge startup...');

        try {
            await this.startBridge();
            this.outputChannel.appendLine('Bridge restarted successfully');
            vscode.window.showInformationMessage('Logger Usage bridge restarted successfully');

            // Reset crash count after successful restart
            this.crashCount = 0;
        } catch (error) {
            this.outputChannel.appendLine(`Retry failed: ${error}`);
            vscode.window.showErrorMessage(
                `Failed to restart bridge: ${error instanceof Error ? error.message : String(error)}`,
                'Show Logs'
            ).then(choice => {
                if (choice === 'Show Logs') {
                    this.outputChannel.show();
                }
            });
        }
    }

    /**
     * Resets the crash count (useful for manual recovery)
     */
    private resetCrashCount(): void {
        this.crashCount = 0;
        this.lastCrashTime = 0;
        this.outputChannel.appendLine('Crash count reset');
        vscode.window.showInformationMessage('Crash count reset. You can try running analysis again.');
    }

    /**
     * Handles bridge process errors
     */
    private handleBridgeError(error: Error): void {
        this.outputChannel.appendLine(`Bridge error: ${error.message}`);

        // Reject all pending requests
        for (const [requestId, pending] of this.pendingResponses.entries()) {
            pending.reject(error);
            this.pendingResponses.delete(requestId);
        }

        vscode.window.showErrorMessage(
            `Logger Usage bridge error: ${error.message}`,
            'Show Logs'
        ).then(choice => {
            if (choice === 'Show Logs') {
                this.outputChannel.show();
            }
        });
    }

    /**
     * Ensures the bridge is ready before sending requests
     */
    private async ensureBridgeReady(): Promise<void> {
        if (this.isReady) {
            return;
        }

        if (!this.bridgeProcess) {
            await this.startBridge();
        }

        if (this.readyPromise) {
            await this.readyPromise;
        }
    }

    /**
     * Finds the C# Bridge executable
     */
    private findBridgeExecutable(): string | null {
        // Determine the executable name based on platform
        const exeName = process.platform === 'win32'
            ? 'LoggerUsage.VSCode.Bridge.exe'
            : 'LoggerUsage.VSCode.Bridge';

        // Try different possible locations
        const possiblePaths = [
            // Development: built locally in Debug
            path.join(this.context.extensionPath, '..', 'LoggerUsage.VSCode.Bridge', 'bin', 'Debug', 'net10.0', exeName),
            // Development: built locally in Release
            path.join(this.context.extensionPath, '..', 'LoggerUsage.VSCode.Bridge', 'bin', 'Release', 'net10.0', exeName),
            // Packaged extension: bridge bundled in extension
            path.join(this.context.extensionPath, 'bridge', exeName),
        ];

        for (const execPath of possiblePaths) {
            if (fs.existsSync(execPath)) {
                return execPath;
            }
        }

        this.outputChannel.appendLine('Bridge executable not found in:');
        for (const execPath of possiblePaths) {
            this.outputChannel.appendLine(`  - ${execPath}`);
        }

        return null;
    }

    /**
     * Shows user-friendly error notification based on error code
     */
    private showErrorNotification(errorResponse: AnalysisErrorResponse): void {
        const errorCode = errorResponse.errorCode;
        let message: string;
        let actions: string[] = [];

        switch (errorCode) {
            case 'INVALID_SOLUTION':
                message = `Solution file is invalid or corrupted: ${errorResponse.message}`;
                actions = ['Check Solution File', 'Show Details'];
                break;

            case 'FILE_NOT_FOUND':
                message = `File not found: ${errorResponse.message}`;
                actions = ['Show Details'];
                break;

            case 'NO_SOLUTION':
                message = 'No solution or project file found in workspace';
                actions = ['Show Details'];
                break;

            case 'COMPILATION_ERROR':
                message = `Compilation failed: ${errorResponse.message}`;
                actions = ['Show Details'];
                break;

            case 'FILE_SYSTEM_ERROR':
                message = `File system error: ${errorResponse.message}`;
                actions = ['Show Details'];
                break;

            case 'MISSING_DEPENDENCIES':
                message = `Missing NuGet packages: ${errorResponse.message}`;
                actions = ['Run dotnet restore', 'Show Details'];
                break;

            case 'CANCELLED':
                // Don't show notification for user-cancelled operations
                return;

            default:
                message = `Analysis failed: ${errorResponse.message}`;
                actions = ['Show Details'];
                break;
        }

        vscode.window.showErrorMessage(message, ...actions).then(choice => {
            if (choice === 'Show Details') {
                this.outputChannel.appendLine('\n=== Analysis Error Details ===');
                this.outputChannel.appendLine(`Error Code: ${errorResponse.errorCode || 'UNKNOWN'}`);
                this.outputChannel.appendLine(`Message: ${errorResponse.message}`);
                this.outputChannel.appendLine(`Details: ${errorResponse.details}`);
                this.outputChannel.appendLine('==============================\n');
                this.outputChannel.show();
            } else if (choice === 'Check Solution File') {
                // Open workspace folder to allow user to check the solution file
                vscode.commands.executeCommand('workbench.files.action.showActiveFileInExplorer');
            } else if (choice === 'Run dotnet restore') {
                // Open integrated terminal and run dotnet restore
                const terminal = vscode.window.createTerminal('dotnet restore');
                terminal.show();
                terminal.sendText('dotnet restore');
            }
        });
    }
}

