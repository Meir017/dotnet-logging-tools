import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AnalysisService } from './analysisService';
import { Configuration } from './configuration';
import { LoggingInsight } from '../models/insightViewModel';
import { getSolutionState } from './state/SolutionState';
import { SolutionInfo } from './utils/solutionDetector';

/**
 * Command handler implementations for Logger Usage extension
 */
export class Commands {
    private currentInsights: LoggingInsight[] = [];
    private insightsPanel: vscode.WebviewPanel | null = null;
    private treeViewProvider: any = null; // Will be properly typed when implemented
    private problemsProvider: any = null; // Will be properly typed when implemented

    constructor(
        private readonly analysisService: AnalysisService,
        private readonly outputChannel: vscode.OutputChannel
    ) {}

    /**
     * Sets the insights panel reference (called from insightsPanel.ts)
     */
    public setInsightsPanel(panel: vscode.WebviewPanel | null): void {
        this.insightsPanel = panel;
    }

    /**
     * Sets the tree view provider reference (called from extension.ts)
     */
    public setTreeViewProvider(provider: any): void {
        this.treeViewProvider = provider;
    }

    /**
     * Sets the problems provider reference (called from extension.ts)
     */
    public setProblemsProvider(provider: any): void {
        this.problemsProvider = provider;
    }

    /**
     * Gets the current insights
     */
    public getCurrentInsights(): LoggingInsight[] {
        return this.currentInsights;
    }

    /**
     * Command: loggerUsage.analyze
     * Triggers full workspace analysis
     */
    public async analyze(): Promise<void> {
        try {
            // Get solution state
            const solutionState = getSolutionState();
            const activeSolution = solutionState.getActiveSolution();

            if (!activeSolution) {
                // No active solution, try to find or select one
                const newSolution = await this.findOrSelectSolution();
                if (!newSolution) {
                    vscode.window.showWarningMessage('No solution file found. Please select a solution to analyze.');
                    return;
                }
                solutionState.setActiveSolution(newSolution);
            }

            const solutionPath = solutionState.getActiveSolutionPath()!;

            // Get workspace root
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
            if (!workspaceFolder) {
                vscode.window.showErrorMessage('No workspace folder open.');
                return;
            }

            this.outputChannel.appendLine(`Starting analysis of: ${solutionPath}`);

            // Run analysis with progress notification
            await vscode.window.withProgress({
                location: vscode.ProgressLocation.Notification,
                title: 'Analyzing logging usage',
                cancellable: true
            }, async (progress, token) => {
                progress.report({ message: 'Initializing...', increment: 0 });

                const result = await this.analysisService.analyzeWorkspace(
                    workspaceFolder.uri.fsPath,
                    solutionPath,
                    undefined,
                    (progressInfo) => {
                        progress.report({
                            message: progressInfo.message,
                            increment: progressInfo.percentage
                        });
                    },
                    token
                );

                // Convert result to insights
                this.currentInsights = this.convertToInsights(result.result.insights);

                this.outputChannel.appendLine(`Analysis complete. Found ${this.currentInsights.length} logging statements.`);

                // Update providers
                this.updateProviders();

                // Show success message
                vscode.window.showInformationMessage(
                    `Analysis complete: ${this.currentInsights.length} logging statements found.`
                );
            });

        } catch (error) {
            this.outputChannel.appendLine(`Analysis failed: ${error}`);
            vscode.window.showErrorMessage(`Analysis failed: ${error instanceof Error ? error.message : String(error)}`);
        }
    }

    /**
     * Command: loggerUsage.showInsightsPanel
     * Opens or reveals the insights webview panel
     */
    public async showInsightsPanel(): Promise<void> {
        if (this.insightsPanel) {
            // Reveal existing panel
            this.insightsPanel.reveal(vscode.ViewColumn.One);
        } else {
            // Panel will be created by insightsPanel.ts and registered via setInsightsPanel()
            await vscode.commands.executeCommand('loggerUsage.internal.createInsightsPanel');
        }
    }

    /**
     * Command: loggerUsage.selectSolution
     * Shows quick pick to select solution file
     */
    public async selectSolution(): Promise<void> {
        const solutionState = getSolutionState();
        const allSolutions = solutionState.getAllSolutions();

        if (allSolutions.length === 0) {
            vscode.window.showWarningMessage('No solution files (.sln) found in workspace.');
            return;
        }

        if (allSolutions.length === 1) {
            solutionState.setActiveSolution(allSolutions[0]);
            vscode.window.showInformationMessage(`Selected solution: ${allSolutions[0].displayName}`);

            // Trigger re-analysis
            await this.analyze();
            return;
        }

        // Show quick pick for multiple solutions
        const items = allSolutions.map(solution => ({
            label: solution.displayName,
            description: solution.relativePath,
            solution: solution
        }));

        const selected = await vscode.window.showQuickPick(items, {
            placeHolder: 'Select a solution file to analyze',
            ignoreFocusOut: false
        });

        if (selected) {
            solutionState.setActiveSolution(selected.solution);
            vscode.window.showInformationMessage(`Selected solution: ${selected.label}`);

            // Trigger re-analysis
            await this.analyze();
        }
    }

    /**
     * Command: loggerUsage.exportInsights
     * Exports insights to file (JSON, CSV, or Markdown)
     */
    public async exportInsights(): Promise<void> {
        if (this.currentInsights.length === 0) {
            vscode.window.showWarningMessage('No insights to export. Run analysis first.');
            return;
        }

        // Prompt for format
        const format = await vscode.window.showQuickPick(
            [
                { label: 'JSON', value: 'json', description: 'Export as JSON file' },
                { label: 'CSV', value: 'csv', description: 'Export as CSV file' },
                { label: 'Markdown', value: 'markdown', description: 'Export as Markdown report' }
            ],
            { placeHolder: 'Select export format' }
        );

        if (!format) {
            return;
        }

        // Prompt for save location
        const defaultUri = vscode.workspace.workspaceFolders?.[0]?.uri;
        const saveUri = await vscode.window.showSaveDialog({
            defaultUri: defaultUri,
            filters: {
                'JSON': ['json'],
                'CSV': ['csv'],
                'Markdown': ['md'],
                'All Files': ['*']
            },
            saveLabel: 'Export'
        });

        if (!saveUri) {
            return;
        }

        try {
            let content: string;

            switch (format.value) {
                case 'json':
                    content = JSON.stringify(this.currentInsights, null, 2);
                    break;
                case 'csv':
                    content = this.convertToCsv(this.currentInsights);
                    break;
                case 'markdown':
                    content = this.convertToMarkdown(this.currentInsights);
                    break;
                default:
                    throw new Error(`Unknown format: ${format.value}`);
            }

            await vscode.workspace.fs.writeFile(saveUri, Buffer.from(content, 'utf-8'));

            const openFile = await vscode.window.showInformationMessage(
                `Insights exported to ${path.basename(saveUri.fsPath)}`,
                'Open File'
            );

            if (openFile === 'Open File') {
                const doc = await vscode.workspace.openTextDocument(saveUri);
                await vscode.window.showTextDocument(doc);
            }

        } catch (error) {
            vscode.window.showErrorMessage(`Export failed: ${error instanceof Error ? error.message : String(error)}`);
        }
    }

    /**
     * Command: loggerUsage.clearFilters
     * Resets all filters to defaults
     */
    public async clearFilters(): Promise<void> {
        // Reset to default filter state
        const defaultLogLevels = Configuration.getDefaultLogLevels();
        const showInconsistenciesOnly = Configuration.getShowInconsistenciesOnly();

        // If webview is open, send reset message
        if (this.insightsPanel) {
            this.insightsPanel.webview.postMessage({
                command: 'updateFilters',
                filters: {
                    logLevels: defaultLogLevels,
                    showInconsistenciesOnly: showInconsistenciesOnly,
                    searchText: '',
                    methodTypes: ['LoggerExtension', 'LoggerMessageAttribute', 'LoggerMessageDefine', 'BeginScope'],
                    tags: []
                }
            });
        }

        vscode.window.showInformationMessage('Filters cleared.');
    }

    /**
     * Command: loggerUsage.navigateToInsight
     * Opens file at insight location
     */
    public async navigateToInsight(insightId: string): Promise<void> {
        const insight = this.currentInsights.find(i => i.id === insightId);

        if (!insight) {
            vscode.window.showErrorMessage(`Insight not found: ${insightId}`);
            return;
        }

        try {
            const uri = vscode.Uri.file(insight.location.filePath);
            const doc = await vscode.workspace.openTextDocument(uri);
            const editor = await vscode.window.showTextDocument(doc);

            // Set cursor position and reveal (convert from 1-based to 0-based, ensure non-negative)
            const position = new vscode.Position(
                Math.max(0, insight.location.startLine - 1),
                Math.max(0, insight.location.startColumn - 1)
            );
            const range = new vscode.Range(
                position,
                new vscode.Position(
                    Math.max(0, insight.location.endLine - 1),
                    Math.max(0, insight.location.endColumn - 1)
                )
            );

            editor.selection = new vscode.Selection(position, position);
            editor.revealRange(range, vscode.TextEditorRevealType.InCenter);

        } catch (error) {
            vscode.window.showErrorMessage(`Failed to open file: ${error instanceof Error ? error.message : String(error)}`);
        }
    }

    /**
     * Command: loggerUsage.refreshTreeView
     * Triggers tree view refresh
     */
    public async refreshTreeView(): Promise<void> {
        if (this.treeViewProvider && typeof this.treeViewProvider.refresh === 'function') {
            this.treeViewProvider.refresh();
            vscode.window.showInformationMessage('Tree view refreshed.');
        } else {
            vscode.window.showWarningMessage('Tree view provider not initialized.');
        }
    }

    /**
     * Command: loggerUsage.analyzeFile
     * Analyzes a single file (incremental)
     */
    public async analyzeFile(fileUri: vscode.Uri): Promise<void> {
        const solutionState = getSolutionState();
        const solutionPath = solutionState.getActiveSolutionPath();

        if (!solutionPath) {
            // No active solution, trigger full analysis
            await this.analyze();
            return;
        }

        try {
            this.outputChannel.appendLine(`Analyzing file: ${fileUri.fsPath}`);

            const result = await this.analysisService.analyzeFile(
                fileUri.fsPath,
                solutionPath
            );

            // Update only insights from this file
            const otherInsights = this.currentInsights.filter(
                i => i.location.filePath !== fileUri.fsPath
            );
            const newInsights = this.convertToInsights(result.result.insights);

            this.currentInsights = [...otherInsights, ...newInsights];

            this.outputChannel.appendLine(`File analysis complete. Found ${newInsights.length} logging statements in file.`);

            // Update providers
            this.updateProviders();

        } catch (error) {
            this.outputChannel.appendLine(`File analysis failed: ${error}`);
            // Don't show error to user for incremental analysis (less disruptive)
        }
    }

    /**
     * Removes insights for a deleted file
     */
    public async removeFileInsights(fileUri: vscode.Uri): Promise<void> {
        const filePath = fileUri.fsPath;

        // Filter out insights from the deleted file
        const insightsBeforeRemoval = this.currentInsights.length;
        this.currentInsights = this.currentInsights.filter(
            i => i.location.filePath !== filePath
        );

        const insightsRemoved = insightsBeforeRemoval - this.currentInsights.length;

        if (insightsRemoved > 0) {
            this.outputChannel.appendLine(`Removed ${insightsRemoved} insights from deleted file: ${filePath}`);

            // Update providers
            this.updateProviders();

            // Clear diagnostics for the deleted file
            if (this.problemsProvider && typeof this.problemsProvider.clearFile === 'function') {
                this.problemsProvider.clearFile(filePath);
            }
        }
    }

    // ==================== Private Helper Methods ====================

    /**
     * Finds or prompts user to select a solution file
     */
    private async findOrSelectSolution(): Promise<SolutionInfo | null> {
        const solutionState = getSolutionState();
        const activeSolution = solutionState.getActiveSolution();

        if (activeSolution && fs.existsSync(activeSolution.filePath)) {
            return activeSolution;
        }

        const allSolutions = solutionState.getAllSolutions();

        if (allSolutions.length === 0) {
            return null;
        }

        if (allSolutions.length === 1) {
            return allSolutions[0];
        }

        // Multiple solutions, prompt user
        await this.selectSolution();
        return solutionState.getActiveSolution();
    }

    /**
     * Converts analysis result to UI insights
     */
    private convertToInsights(insights: any[]): LoggingInsight[] {
        // The analysis service returns data in the correct format already
        // Just ensure proper typing and ID generation
        return insights.map((insight) => ({
            ...insight,
            id: insight.id || `${insight.location.filePath}:${insight.location.startLine}:${insight.location.startColumn}`
        }));
    }

    /**
     * Updates all providers with current insights
     */
    private updateProviders(): void {
        // Update tree view
        if (this.treeViewProvider && typeof this.treeViewProvider.updateInsights === 'function') {
            this.treeViewProvider.updateInsights(this.currentInsights);
        }

        // Update problems panel
        if (this.problemsProvider && typeof this.problemsProvider.updateInsights === 'function') {
            this.problemsProvider.updateInsights(this.currentInsights);
        }

        // Update webview
        if (this.insightsPanel) {
            this.insightsPanel.webview.postMessage({
                command: 'updateInsights',
                insights: this.currentInsights
            });
        }
    }

    /**
     * Converts insights to CSV format
     */
    private convertToCsv(insights: LoggingInsight[]): string {
        const headers = [
            'File',
            'Line',
            'Method Type',
            'Log Level',
            'Message Template',
            'Event ID',
            'Event Name',
            'Parameters',
            'Has Inconsistencies',
            'Tags'
        ];

        const rows = insights.map(insight => [
            insight.location.filePath,
            insight.location.startLine.toString(),
            insight.methodType,
            insight.logLevel || '',
            this.escapeCsv(insight.messageTemplate),
            insight.eventId?.id?.toString() || '',
            insight.eventId?.name || '',
            insight.parameters.join('; '),
            insight.hasInconsistencies ? 'Yes' : 'No',
            insight.tags.join('; ')
        ]);

        const csvContent = [
            headers.join(','),
            ...rows.map(row => row.map(cell => this.escapeCsv(cell)).join(','))
        ].join('\n');

        return csvContent;
    }

    /**
     * Escapes CSV cell content
     */
    private escapeCsv(value: string): string {
        if (value.includes(',') || value.includes('"') || value.includes('\n')) {
            return `"${value.replace(/"/g, '""')}"`;
        }
        return value;
    }

    /**
     * Converts insights to Markdown report
     */
    private convertToMarkdown(insights: LoggingInsight[]): string {
        const lines: string[] = [];

        lines.push('# Logger Usage Analysis Report');
        lines.push('');
        lines.push(`**Total Logging Statements:** ${insights.length}`);
        lines.push('');

        // Group by method type
        const byMethodType = this.groupBy(insights, i => i.methodType);
        lines.push('## Summary by Method Type');
        lines.push('');
        for (const [methodType, items] of Object.entries(byMethodType)) {
            lines.push(`- **${methodType}:** ${items.length}`);
        }
        lines.push('');

        // Group by log level
        const byLogLevel = this.groupBy(insights, i => i.logLevel || 'Unknown');
        lines.push('## Summary by Log Level');
        lines.push('');
        for (const [logLevel, items] of Object.entries(byLogLevel)) {
            lines.push(`- **${logLevel}:** ${items.length}`);
        }
        lines.push('');

        // Inconsistencies
        const withInconsistencies = insights.filter(i => i.hasInconsistencies);
        if (withInconsistencies.length > 0) {
            lines.push('## Inconsistencies Found');
            lines.push('');
            lines.push(`**Total:** ${withInconsistencies.length}`);
            lines.push('');

            for (const insight of withInconsistencies) {
                lines.push(`### ${path.basename(insight.location.filePath)}:${insight.location.startLine}`);
                lines.push('');
                lines.push(`**Method Type:** ${insight.methodType}`);
                lines.push(`**Log Level:** ${insight.logLevel || 'N/A'}`);
                lines.push(`**Message:** \`${insight.messageTemplate}\``);
                lines.push('');

                if (insight.inconsistencies) {
                    lines.push('**Issues:**');
                    for (const issue of insight.inconsistencies) {
                        lines.push(`- [${issue.severity}] ${issue.message}`);
                    }
                    lines.push('');
                }
            }
        }

        // Detailed listing
        lines.push('## Detailed Logging Statements');
        lines.push('');

        for (const insight of insights) {
            const fileName = path.basename(insight.location.filePath);
            lines.push(`### ${fileName}:${insight.location.startLine}`);
            lines.push('');
            lines.push(`- **Method Type:** ${insight.methodType}`);
            lines.push(`- **Log Level:** ${insight.logLevel || 'N/A'}`);
            lines.push(`- **Message Template:** \`${insight.messageTemplate}\``);
            if (insight.eventId) {
                lines.push(`- **Event ID:** ${insight.eventId.id} (${insight.eventId.name || 'N/A'})`);
            }
            if (insight.parameters.length > 0) {
                lines.push(`- **Parameters:** ${insight.parameters.join(', ')}`);
            }
            if (insight.tags.length > 0) {
                lines.push(`- **Tags:** ${insight.tags.join(', ')}`);
            }
            lines.push('');
        }

        return lines.join('\n');
    }

    /**
     * Groups array by key function
     */
    private groupBy<T>(array: T[], keyFn: (item: T) => string): Record<string, T[]> {
        return array.reduce((result, item) => {
            const key = keyFn(item);
            if (!result[key]) {
                result[key] = [];
            }
            result[key].push(item);
            return result;
        }, {} as Record<string, T[]>);
    }
}
