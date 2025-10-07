import * as vscode from 'vscode';

/**
 * Configuration manager for Logger Usage extension settings
 */
export class Configuration {
    private static readonly SECTION = 'loggerUsage';

    /**
     * Gets whether to automatically analyze on save
     */
    public static getAutoAnalyzeOnSave(): boolean {
        return this.getConfig<boolean>('autoAnalyzeOnSave', true);
    }

    /**
     * Gets file exclude patterns for analysis
     */
    public static getExcludePatterns(): string[] {
        return this.getConfig<string[]>('excludePatterns', ['**/obj/**', '**/bin/**']);
    }

    /**
     * Gets maximum files per analysis
     */
    public static getMaxFilesPerAnalysis(): number {
        return this.getConfig<number>('performanceThresholds.maxFilesPerAnalysis', 1000);
    }

    /**
     * Gets analysis timeout in milliseconds
     */
    public static getAnalysisTimeoutMs(): number {
        return this.getConfig<number>('performanceThresholds.analysisTimeoutMs', 300000);
    }

    /**
     * Gets whether problems integration is enabled
     */
    public static getEnableProblemsIntegration(): boolean {
        return this.getConfig<boolean>('enableProblemsIntegration', true);
    }

    /**
     * Gets default log levels to filter
     */
    public static getDefaultLogLevels(): string[] {
        return this.getConfig<string[]>('filterDefaults.logLevels', ['Information', 'Warning', 'Error']);
    }

    /**
     * Gets whether to show inconsistencies only by default
     */
    public static getShowInconsistenciesOnly(): boolean {
        return this.getConfig<boolean>('filterDefaults.showInconsistenciesOnly', false);
    }

    /**
     * Registers configuration change listeners
     * @param callback Function to call when configuration changes
     * @returns Disposable to unregister the listener
     */
    public static onDidChangeConfiguration(
        callback: (e: vscode.ConfigurationChangeEvent) => void
    ): vscode.Disposable {
        return vscode.workspace.onDidChangeConfiguration((e) => {
            if (e.affectsConfiguration(this.SECTION)) {
                callback(e);
            }
        });
    }

    /**
     * Gets a configuration value with default fallback
     */
    private static getConfig<T>(key: string, defaultValue: T): T {
        const config = vscode.workspace.getConfiguration(this.SECTION);
        return config.get<T>(key, defaultValue);
    }

    /**
     * Updates a configuration value
     * @param key Configuration key (without section prefix)
     * @param value New value
     * @param target Configuration target (Global, Workspace, or WorkspaceFolder)
     */
    public static async updateConfig<T>(
        key: string,
        value: T,
        target: vscode.ConfigurationTarget = vscode.ConfigurationTarget.Workspace
    ): Promise<void> {
        const config = vscode.workspace.getConfiguration(this.SECTION);
        await config.update(key, value, target);
    }

    /**
     * Resets all configuration to defaults
     */
    public static async resetToDefaults(): Promise<void> {
        const config = vscode.workspace.getConfiguration(this.SECTION);
        const keys = [
            'autoAnalyzeOnSave',
            'excludePatterns',
            'performanceThresholds.maxFilesPerAnalysis',
            'performanceThresholds.analysisTimeoutMs',
            'enableProblemsIntegration',
            'filterDefaults.logLevels',
            'filterDefaults.showInconsistenciesOnly'
        ];

        for (const key of keys) {
            await config.update(key, undefined, vscode.ConfigurationTarget.Workspace);
        }
    }
}
