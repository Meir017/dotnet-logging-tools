import * as vscode from 'vscode';
import * as fs from 'fs';
import { LoggingInsight } from '../models/insightViewModel';
import { FilterState, DEFAULT_FILTER_STATE } from '../models/filterState';
import { ExtensionToWebviewMessage, WebviewToExtensionMessage } from '../models/webviewMessages';
import { AnalysisSummary } from '../models/ipcMessages';

/**
 * Manages the webview panel for displaying logging insights
 */
export class InsightsPanel implements vscode.Disposable {
    public static readonly viewType = 'loggerUsageInsights';

    private static currentPanel: InsightsPanel | undefined;
    private readonly panel: vscode.WebviewPanel;
    private readonly extensionUri: vscode.Uri;
    private disposables: vscode.Disposable[] = [];

    private currentInsights: LoggingInsight[] = [];
    private currentSummary: AnalysisSummary | null = null;
    private currentFilter: FilterState = DEFAULT_FILTER_STATE;
    
    // Callback for handling webview messages (set by Commands class)
    private onNavigateToInsightCallback?: (insightId: string) => Promise<void>;
    private onExportResultsCallback?: (format: 'json' | 'csv' | 'markdown') => Promise<void>;
    private onRefreshAnalysisCallback?: () => Promise<void>;

    /**
     * Creates or shows the insights panel
     */
    public static createOrShow(extensionUri: vscode.Uri): InsightsPanel {
        const column = vscode.window.activeTextEditor
            ? vscode.window.activeTextEditor.viewColumn
            : undefined;

        // If we already have a panel, show it
        if (InsightsPanel.currentPanel) {
            InsightsPanel.currentPanel.panel.reveal(column);
            return InsightsPanel.currentPanel;
        }

        // Otherwise, create a new panel
        const panel = vscode.window.createWebviewPanel(
            InsightsPanel.viewType,
            'Logger Usage Insights',
            column || vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [
                    vscode.Uri.joinPath(extensionUri, 'out'),
                    vscode.Uri.joinPath(extensionUri, 'views'),
                    vscode.Uri.joinPath(extensionUri, 'media')
                ]
            }
        );

        InsightsPanel.currentPanel = new InsightsPanel(panel, extensionUri);
        return InsightsPanel.currentPanel;
    }

    /**
     * Gets the current panel instance (if exists)
     */
    public static getCurrentPanel(): InsightsPanel | undefined {
        return InsightsPanel.currentPanel;
    }

    private constructor(panel: vscode.WebviewPanel, extensionUri: vscode.Uri) {
        this.panel = panel;
        this.extensionUri = extensionUri;

        // Set the webview's initial HTML content
        this.updateWebviewContent();

        // Listen for theme changes
        this.disposables.push(
            vscode.window.onDidChangeActiveColorTheme(() => {
                this.sendThemeUpdate();
            })
        );

        // Handle messages from the webview
        this.panel.webview.onDidReceiveMessage(
            (message: WebviewToExtensionMessage) => this.handleWebviewMessage(message),
            null,
            this.disposables
        );

        // Handle panel disposal
        this.panel.onDidDispose(
            () => this.dispose(),
            null,
            this.disposables
        );

        // Send initial theme
        this.sendThemeUpdate();
    }

    /**
     * Updates the insights displayed in the panel
     */
    public updateInsights(insights: LoggingInsight[], summary?: AnalysisSummary): void {
        this.currentInsights = insights;
        this.currentSummary = summary || this.calculateSummary(insights);

        const message: ExtensionToWebviewMessage = {
            command: 'updateInsights',
            insights: this.currentInsights,
            summary: this.currentSummary
        };

        this.panel.webview.postMessage(message);
    }

    /**
     * Updates the filter state
     */
    public updateFilters(filters: FilterState): void {
        this.currentFilter = filters;

        const message: ExtensionToWebviewMessage = {
            command: 'updateFilters',
            filters: this.currentFilter
        };

        this.panel.webview.postMessage(message);
    }

    /**
     * Shows an error in the webview
     */
    public showError(message: string, details?: string): void {
        const errorMessage: ExtensionToWebviewMessage = {
            command: 'showError',
            message: message,
            details: details
        };

        this.panel.webview.postMessage(errorMessage);
    }

    /**
     * Sets callback for navigating to insight
     */
    public setNavigateToInsightCallback(callback: (insightId: string) => Promise<void>): void {
        this.onNavigateToInsightCallback = callback;
    }

    /**
     * Sets callback for exporting results
     */
    public setExportResultsCallback(callback: (format: 'json' | 'csv' | 'markdown') => Promise<void>): void {
        this.onExportResultsCallback = callback;
    }

    /**
     * Sets callback for refreshing analysis
     */
    public setRefreshAnalysisCallback(callback: () => Promise<void>): void {
        this.onRefreshAnalysisCallback = callback;
    }

    /**
     * Disposes of the panel
     */
    public dispose(): void {
        InsightsPanel.currentPanel = undefined;

        // Clean up resources
        this.panel.dispose();

        while (this.disposables.length) {
            const disposable = this.disposables.pop();
            if (disposable) {
                disposable.dispose();
            }
        }
    }

    // ==================== Private Methods ====================

    /**
     * Handles messages received from the webview
     */
    private async handleWebviewMessage(message: WebviewToExtensionMessage): Promise<void> {
        switch (message.command) {
            case 'applyFilters':
                this.currentFilter = message.filters;
                // Filter insights and send update
                const filteredInsights = this.applyFilters(this.currentInsights, message.filters);
                const filteredSummary = this.calculateSummary(filteredInsights);
                this.updateInsights(filteredInsights, filteredSummary);
                break;

            case 'navigateToInsight':
                if (this.onNavigateToInsightCallback) {
                    await this.onNavigateToInsightCallback(message.insightId);
                } else {
                    // Fallback to command
                    await vscode.commands.executeCommand('loggerUsage.navigateToInsight', message.insightId);
                }
                break;

            case 'exportResults':
                if (this.onExportResultsCallback) {
                    await this.onExportResultsCallback(message.format);
                } else {
                    // Fallback to command
                    await vscode.commands.executeCommand('loggerUsage.exportInsights');
                }
                break;

            case 'refreshAnalysis':
                if (this.onRefreshAnalysisCallback) {
                    await this.onRefreshAnalysisCallback();
                } else {
                    // Fallback to command
                    await vscode.commands.executeCommand('loggerUsage.analyze');
                }
                break;
        }
    }

    /**
     * Sends theme update to webview
     */
    private sendThemeUpdate(): void {
        const theme = this.detectTheme();
        const message: ExtensionToWebviewMessage = {
            command: 'updateTheme',
            theme: theme
        };

        this.panel.webview.postMessage(message);
    }

    /**
     * Detects the current VS Code theme
     */
    private detectTheme(): 'light' | 'dark' | 'high-contrast' {
        const colorTheme = vscode.window.activeColorTheme;
        
        // Check for high contrast first
        if (colorTheme.kind === vscode.ColorThemeKind.HighContrast ||
            colorTheme.kind === vscode.ColorThemeKind.HighContrastLight) {
            return 'high-contrast';
        }
        
        // Check for light theme
        if (colorTheme.kind === vscode.ColorThemeKind.Light) {
            return 'light';
        }
        
        // Default to dark
        return 'dark';
    }

    /**
     * Updates the webview's HTML content
     */
    private updateWebviewContent(): void {
        const webview = this.panel.webview;
        
        // Try to load from HTML template file
        const htmlPath = vscode.Uri.joinPath(this.extensionUri, 'views', 'insightsView.html');
        
        try {
            if (fs.existsSync(htmlPath.fsPath)) {
                let htmlContent = fs.readFileSync(htmlPath.fsPath, 'utf-8');
                
                // Replace template variables
                htmlContent = this.replaceTemplateVariables(htmlContent, webview);
                
                webview.html = htmlContent;
            } else {
                // Fallback to inline HTML
                webview.html = this.getInlineHtmlContent(webview);
            }
        } catch (error) {
            // Fallback to inline HTML
            webview.html = this.getInlineHtmlContent(webview);
        }
    }

    /**
     * Replaces template variables in HTML
     */
    private replaceTemplateVariables(html: string, webview: vscode.Webview): string {
        const nonce = this.getNonce();
        
        // Replace CSP nonce
        html = html.replace(/\$\{nonce\}/g, nonce);
        
        // Replace CSP source
        html = html.replace(/\$\{cspSource\}/g, webview.cspSource);
        
        return html;
    }

    /**
     * Gets inline HTML content (fallback)
     */
    private getInlineHtmlContent(webview: vscode.Webview): string {
        const nonce = this.getNonce();

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src 'nonce-${nonce}';">
    <title>Logger Usage Insights</title>
    <style>
        body {
            padding: 20px;
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
            font-family: var(--vscode-font-family);
            font-size: var(--vscode-font-size);
        }
        
        h1 {
            color: var(--vscode-foreground);
            font-size: 24px;
            margin-bottom: 20px;
        }
        
        .toolbar {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }
        
        .toolbar button {
            background-color: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border: none;
            padding: 6px 14px;
            cursor: pointer;
            border-radius: 2px;
        }
        
        .toolbar button:hover {
            background-color: var(--vscode-button-hoverBackground);
        }
        
        .filters {
            background-color: var(--vscode-editor-inactiveSelectionBackground);
            padding: 15px;
            margin-bottom: 20px;
            border-radius: 4px;
        }
        
        .filter-group {
            margin-bottom: 10px;
        }
        
        .filter-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: bold;
        }
        
        .filter-group input[type="text"] {
            width: 100%;
            padding: 6px;
            background-color: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            border-radius: 2px;
        }
        
        .filter-group input[type="checkbox"] {
            margin-right: 5px;
        }
        
        .insights-table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 20px;
        }
        
        .insights-table th,
        .insights-table td {
            padding: 8px;
            text-align: left;
            border-bottom: 1px solid var(--vscode-panel-border);
        }
        
        .insights-table th {
            background-color: var(--vscode-editor-inactiveSelectionBackground);
            font-weight: bold;
        }
        
        .insights-table tr:hover {
            background-color: var(--vscode-list-hoverBackground);
            cursor: pointer;
        }
        
        .badge {
            display: inline-block;
            padding: 2px 6px;
            border-radius: 3px;
            font-size: 11px;
            font-weight: bold;
        }
        
        .badge-inconsistency {
            background-color: var(--vscode-editorWarning-foreground);
            color: var(--vscode-editor-background);
        }
        
        .badge-method-type {
            background-color: var(--vscode-editorInfo-foreground);
            color: var(--vscode-editor-background);
        }
        
        .summary {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }
        
        .summary-card {
            background-color: var(--vscode-editor-inactiveSelectionBackground);
            padding: 15px;
            border-radius: 4px;
        }
        
        .summary-card h3 {
            margin: 0 0 10px 0;
            font-size: 14px;
            color: var(--vscode-descriptionForeground);
        }
        
        .summary-card .value {
            font-size: 32px;
            font-weight: bold;
        }
        
        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: var(--vscode-descriptionForeground);
        }
        
        .empty-state h2 {
            margin-bottom: 10px;
        }
    </style>
</head>
<body>
    <h1>Logger Usage Insights</h1>
    
    <div class="toolbar">
        <button id="refreshBtn">Refresh Analysis</button>
        <button id="exportJsonBtn">Export JSON</button>
        <button id="exportCsvBtn">Export CSV</button>
        <button id="exportMarkdownBtn">Export Markdown</button>
        <button id="clearFiltersBtn">Clear Filters</button>
    </div>
    
    <div class="summary" id="summary">
        <!-- Summary cards will be inserted here -->
    </div>
    
    <div class="filters">
        <div class="filter-group">
            <label for="searchInput">Search:</label>
            <input type="text" id="searchInput" placeholder="Search message templates...">
        </div>
        <div class="filter-group">
            <label>
                <input type="checkbox" id="showInconsistenciesOnly">
                Show Only Inconsistencies
            </label>
        </div>
    </div>
    
    <div id="content">
        <div class="empty-state">
            <h2>No insights available</h2>
            <p>Run analysis to view logging insights</p>
        </div>
    </div>
    
    <script nonce="${nonce}">
        const vscode = acquireVsCodeApi();
        
        let currentInsights = [];
        let currentSummary = null;
        
        // Handle messages from extension
        window.addEventListener('message', event => {
            const message = event.data;
            
            switch (message.command) {
                case 'updateInsights':
                    currentInsights = message.insights;
                    currentSummary = message.summary;
                    renderInsights();
                    renderSummary();
                    break;
                    
                case 'updateFilters':
                    // Update filter UI
                    break;
                    
                case 'updateTheme':
                    // Theme changed
                    document.body.className = 'theme-' + message.theme;
                    break;
                    
                case 'showError':
                    alert('Error: ' + message.message);
                    break;
            }
        });
        
        // Toolbar buttons
        document.getElementById('refreshBtn').addEventListener('click', () => {
            vscode.postMessage({ command: 'refreshAnalysis' });
        });
        
        document.getElementById('exportJsonBtn').addEventListener('click', () => {
            vscode.postMessage({ command: 'exportResults', format: 'json' });
        });
        
        document.getElementById('exportCsvBtn').addEventListener('click', () => {
            vscode.postMessage({ command: 'exportResults', format: 'csv' });
        });
        
        document.getElementById('exportMarkdownBtn').addEventListener('click', () => {
            vscode.postMessage({ command: 'exportResults', format: 'markdown' });
        });
        
        document.getElementById('clearFiltersBtn').addEventListener('click', () => {
            document.getElementById('searchInput').value = '';
            document.getElementById('showInconsistenciesOnly').checked = false;
            applyFilters();
        });
        
        // Filter changes
        document.getElementById('searchInput').addEventListener('input', applyFilters);
        document.getElementById('showInconsistenciesOnly').addEventListener('change', applyFilters);
        
        function applyFilters() {
            const searchQuery = document.getElementById('searchInput').value;
            const showInconsistenciesOnly = document.getElementById('showInconsistenciesOnly').checked;
            
            vscode.postMessage({
                command: 'applyFilters',
                filters: {
                    logLevels: [],
                    methodTypes: [],
                    searchQuery: searchQuery,
                    showInconsistenciesOnly: showInconsistenciesOnly,
                    tags: [],
                    filePaths: []
                }
            });
        }
        
        function renderSummary() {
            const summaryEl = document.getElementById('summary');
            
            if (!currentSummary) {
                summaryEl.innerHTML = '';
                return;
            }
            
            summaryEl.innerHTML = \`
                <div class="summary-card">
                    <h3>Total Logging Statements</h3>
                    <div class="value">\${currentSummary.totalInsights || 0}</div>
                </div>
                <div class="summary-card">
                    <h3>Files Analyzed</h3>
                    <div class="value">\${currentSummary.filesAnalyzed || 0}</div>
                </div>
                <div class="summary-card">
                    <h3>Inconsistencies Found</h3>
                    <div class="value">\${currentSummary.inconsistenciesCount || 0}</div>
                </div>
            \`;
        }
        
        function renderInsights() {
            const contentEl = document.getElementById('content');
            
            if (!currentInsights || currentInsights.length === 0) {
                contentEl.innerHTML = \`
                    <div class="empty-state">
                        <h2>No insights available</h2>
                        <p>Run analysis to view logging insights</p>
                    </div>
                \`;
                return;
            }
            
            let html = '<table class="insights-table"><thead><tr>';
            html += '<th>File</th><th>Line</th><th>Method Type</th>';
            html += '<th>Log Level</th><th>Message</th><th>Status</th>';
            html += '</tr></thead><tbody>';
            
            for (const insight of currentInsights) {
                const fileName = insight.location.filePath.split(/[\\\\/]/).pop();
                const inconsistencyBadge = insight.hasInconsistencies 
                    ? '<span class="badge badge-inconsistency">!</span>' 
                    : '';
                
                html += \`<tr onclick="navigateToInsight('\${insight.id}')">\`;
                html += \`<td>\${fileName}</td>\`;
                html += \`<td>\${insight.location.startLine}</td>\`;
                html += \`<td><span class="badge badge-method-type">\${insight.methodType}</span></td>\`;
                html += \`<td>\${insight.logLevel || 'N/A'}</td>\`;
                html += \`<td>\${escapeHtml(insight.messageTemplate)}</td>\`;
                html += \`<td>\${inconsistencyBadge}</td>\`;
                html += '</tr>';
            }
            
            html += '</tbody></table>';
            contentEl.innerHTML = html;
        }
        
        function navigateToInsight(insightId) {
            vscode.postMessage({ command: 'navigateToInsight', insightId: insightId });
        }
        
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
    </script>
</body>
</html>`;
    }

    /**
     * Generates a cryptographically secure nonce
     */
    private getNonce(): string {
        let text = '';
        const possible = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
        for (let i = 0; i < 32; i++) {
            text += possible.charAt(Math.floor(Math.random() * possible.length));
        }
        return text;
    }

    /**
     * Applies filters to insights
     */
    private applyFilters(insights: LoggingInsight[], filters: FilterState): LoggingInsight[] {
        let filtered = insights;

        // Filter by search query
        if (filters.searchQuery) {
            const query = filters.searchQuery.toLowerCase();
            filtered = filtered.filter(i => 
                i.messageTemplate.toLowerCase().includes(query)
            );
        }

        // Filter by inconsistencies only
        if (filters.showInconsistenciesOnly) {
            filtered = filtered.filter(i => i.hasInconsistencies);
        }

        // Filter by log levels
        if (filters.logLevels.length > 0) {
            filtered = filtered.filter(i => 
                i.logLevel && filters.logLevels.includes(i.logLevel)
            );
        }

        // Filter by method types
        if (filters.methodTypes.length > 0) {
            filtered = filtered.filter(i => 
                filters.methodTypes.includes(i.methodType)
            );
        }

        // Filter by tags
        if (filters.tags.length > 0) {
            filtered = filtered.filter(i => 
                i.tags.some(tag => filters.tags.includes(tag))
            );
        }

        // Filter by file paths
        if (filters.filePaths.length > 0) {
            filtered = filtered.filter(i => 
                filters.filePaths.includes(i.location.filePath)
            );
        }

        return filtered;
    }

    /**
     * Calculates summary statistics from insights
     */
    private calculateSummary(insights: LoggingInsight[]): AnalysisSummary {
        const uniqueFiles = new Set(insights.map(i => i.location.filePath));
        const inconsistencies = insights.filter(i => i.hasInconsistencies).length;

        return {
            totalInsights: insights.length,
            filesAnalyzed: uniqueFiles.size,
            inconsistenciesCount: inconsistencies,
            byMethodType: this.groupByMethodType(insights),
            byLogLevel: this.groupByLogLevel(insights),
            analysisTimeMs: 0 // Not tracked in this context
        };
    }

    /**
     * Groups insights by method type
     */
    private groupByMethodType(insights: LoggingInsight[]): Record<string, number> {
        const result: Record<string, number> = {};
        for (const insight of insights) {
            result[insight.methodType] = (result[insight.methodType] || 0) + 1;
        }
        return result;
    }

    /**
     * Groups insights by log level
     */
    private groupByLogLevel(insights: LoggingInsight[]): Record<string, number> {
        const result: Record<string, number> = {};
        for (const insight of insights) {
            const level = insight.logLevel || 'Unknown';
            result[level] = (result[level] || 0) + 1;
        }
        return result;
    }
}
