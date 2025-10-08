import * as vscode from 'vscode';

/**
 * Centralized logging utility using VS Code Output Channel
 */
class Logger {
    private outputChannel: vscode.OutputChannel | null = null;
    private readonly channelName = 'Logger Usage';

    /**
     * Initializes the logger with an output channel
     */
    public initialize(outputChannel?: vscode.OutputChannel): void {
        this.outputChannel = outputChannel ?? vscode.window.createOutputChannel(this.channelName);
    }

    /**
     * Gets the output channel (creates if not exists)
     */
    private getChannel(): vscode.OutputChannel {
        if (!this.outputChannel) {
            this.outputChannel = vscode.window.createOutputChannel(this.channelName);
        }
        return this.outputChannel;
    }

    /**
     * Logs an informational message
     */
    public logInfo(message: string, ...args: any[]): void {
        const formatted = this.formatMessage('INFO', message, args);
        this.getChannel().appendLine(formatted);
    }

    /**
     * Logs a warning message
     */
    public logWarning(message: string, ...args: any[]): void {
        const formatted = this.formatMessage('WARN', message, args);
        this.getChannel().appendLine(formatted);
    }

    /**
     * Logs an error message
     */
    public logError(message: string, error?: Error | unknown, ...args: any[]): void {
        let formatted = this.formatMessage('ERROR', message, args);
        
        if (error) {
            if (error instanceof Error) {
                formatted += `\n  Error: ${error.message}`;
                if (error.stack) {
                    formatted += `\n  Stack: ${error.stack}`;
                }
            } else {
                formatted += `\n  Error: ${String(error)}`;
            }
        }
        
        this.getChannel().appendLine(formatted);
    }

    /**
     * Logs a debug message (only in development)
     */
    public logDebug(message: string, ...args: any[]): void {
        if (process.env.NODE_ENV === 'development') {
            const formatted = this.formatMessage('DEBUG', message, args);
            this.getChannel().appendLine(formatted);
        }
    }

    /**
     * Shows the output channel
     */
    public show(preserveFocus: boolean = true): void {
        this.getChannel().show(preserveFocus);
    }

    /**
     * Clears the output channel
     */
    public clear(): void {
        this.getChannel().clear();
    }

    /**
     * Formats a log message with timestamp and level
     */
    private formatMessage(level: string, message: string, args: any[]): string {
        const timestamp = new Date().toISOString();
        let formatted = `[${timestamp}] [${level}] ${message}`;
        
        if (args.length > 0) {
            const argsStr = args.map(arg => {
                if (typeof arg === 'object') {
                    try {
                        return JSON.stringify(arg);
                    } catch {
                        return String(arg);
                    }
                }
                return String(arg);
            }).join(' ');
            
            formatted += ` ${argsStr}`;
        }
        
        return formatted;
    }

    /**
     * Disposes the logger and output channel
     */
    public dispose(): void {
        if (this.outputChannel) {
            this.outputChannel.dispose();
            this.outputChannel = null;
        }
    }
}

/**
 * Singleton logger instance
 */
export const logger = new Logger();
