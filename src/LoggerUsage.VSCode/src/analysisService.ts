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
        await this.ensureBridgeReady();

        const request: AnalysisRequest = {
            command: 'analyze',
            workspacePath,
            solutionPath,
            excludePatterns
        };

        return this.sendRequest(request, onProgress, cancellationToken);
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

        const request: IncrementalAnalysisRequest = {
            command: 'analyzeFile',
            filePath,
            solutionPath
        };

        return this.sendRequest(request, onProgress, cancellationToken);
    }

    /**
     * Disposes the service and closes the bridge process
     */
    public dispose(): void {
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
                if (pendingEntry.onProgress) {
                    pendingEntry.onProgress(response as AnalysisProgress);
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
                
                // Show error to user
                vscode.window.showErrorMessage(
                    `Logger Usage Analysis Failed: ${errorResponse.message}`,
                    'Show Details'
                ).then(choice => {
                    if (choice === 'Show Details') {
                        this.outputChannel.show();
                    }
                });
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
        this.bridgeProcess = null;
        this.isReady = false;
        this.readyPromise = null;

        // Reject all pending requests
        for (const [requestId, pending] of this.pendingResponses.entries()) {
            pending.reject(new Error(`Bridge process exited unexpectedly (code: ${code}, signal: ${signal})`));
            this.pendingResponses.delete(requestId);
        }

        if (code !== 0 && code !== null) {
            vscode.window.showErrorMessage(
                `Logger Usage bridge process crashed (exit code: ${code})`,
                'Show Logs'
            ).then(choice => {
                if (choice === 'Show Logs') {
                    this.outputChannel.show();
                }
            });
        }
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
        // Try different possible locations
        const possiblePaths = [
            // Development: built locally
            path.join(this.context.extensionPath, '..', 'LoggerUsage.VSCode.Bridge', 'bin', 'Debug', 'net10.0', 'win-x64', 'LoggerUsage.VSCode.Bridge.exe'),
            path.join(this.context.extensionPath, '..', 'LoggerUsage.VSCode.Bridge', 'bin', 'Release', 'net10.0', 'win-x64', 'LoggerUsage.VSCode.Bridge.exe'),
            
            // Packaged extension: bridge bundled in extension
            path.join(this.context.extensionPath, 'bridge', 'LoggerUsage.VSCode.Bridge.exe'),
            path.join(this.context.extensionPath, 'bridge', 'win-x64', 'LoggerUsage.VSCode.Bridge.exe'),
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
}
