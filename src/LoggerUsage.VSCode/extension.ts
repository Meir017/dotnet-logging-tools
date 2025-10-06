import * as vscode from 'vscode';
import { AnalysisService } from './src/analysisService';
import { Commands } from './src/commands';
import { InsightsPanel } from './src/insightsPanel';
import { ProblemsProvider } from './src/problemsProvider';
import { LoggerTreeViewProvider } from './src/treeViewProvider';
import { Configuration } from './src/configuration';

let analysisService: AnalysisService;
let commands: Commands;
let problemsProvider: ProblemsProvider;
let treeViewProvider: LoggerTreeViewProvider;
let outputChannel: vscode.OutputChannel;
let statusBarItem: vscode.StatusBarItem;

/**
 * Extension activation entry point
 */
export async function activate(context: vscode.ExtensionContext): Promise<void> {
    console.log('Logger Usage extension is now activating...');

    // Create output channel
    outputChannel = vscode.window.createOutputChannel('Logger Usage');
    outputChannel.appendLine('Logger Usage extension activated');
    context.subscriptions.push(outputChannel);

    // Create status bar item
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    statusBarItem.text = '$(search) Logger Usage';
    statusBarItem.tooltip = 'Click to analyze logging usage';
    statusBarItem.command = 'loggerUsage.analyze';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);

    try {
        // Initialize services
        analysisService = new AnalysisService(context, outputChannel);
        context.subscriptions.push(analysisService);

        // Initialize providers
        problemsProvider = new ProblemsProvider();
        context.subscriptions.push(problemsProvider);

        treeViewProvider = new LoggerTreeViewProvider();
        context.subscriptions.push(treeViewProvider);

        // Register tree view
        const treeView = vscode.window.createTreeView('loggerUsageTreeView', {
            treeDataProvider: treeViewProvider,
            showCollapseAll: true
        });
        context.subscriptions.push(treeView);

        // Initialize command handler
        commands = new Commands(analysisService, outputChannel);
        commands.setProblemsProvider(problemsProvider);
        commands.setTreeViewProvider(treeViewProvider);

        // Register commands
        registerCommands(context);

        // Set up webview panel callbacks
        setupWebviewCallbacks();

        // Set up file watchers if auto-analyze is enabled
        if (Configuration.getAutoAnalyzeOnSave()) {
            setupFileWatchers(context);
        }

        // Listen for configuration changes
        context.subscriptions.push(
            Configuration.onDidChangeConfiguration((e) => {
                handleConfigurationChange(e);
            })
        );

        outputChannel.appendLine('All services initialized successfully');

        // Show welcome message on first activation
        const hasShownWelcome = context.globalState.get('hasShownWelcome', false);
        if (!hasShownWelcome) {
            showWelcomeMessage();
            context.globalState.update('hasShownWelcome', true);
        }

    } catch (error) {
        const errorMessage = `Failed to activate Logger Usage extension: ${error instanceof Error ? error.message : String(error)}`;
        outputChannel.appendLine(errorMessage);
        vscode.window.showErrorMessage(errorMessage);
        throw error;
    }
}

/**
 * Extension deactivation
 */
export function deactivate(): void {
    console.log('Logger Usage extension is now deactivating...');

    if (outputChannel) {
        outputChannel.appendLine('Logger Usage extension deactivated');
    }

    // Services will be disposed automatically via context.subscriptions
}

// ==================== Private Functions ====================

/**
 * Registers all extension commands
 */
function registerCommands(context: vscode.ExtensionContext): void {
    // Main analysis command
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.analyze', async () => {
            updateStatusBar('Analyzing...', true);
            try {
                await commands.analyze();
                updateStatusBar('Analysis complete', false);
            } catch (error) {
                updateStatusBar('Analysis failed', false);
                throw error;
            }
        })
    );

    // Show insights panel command
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.showInsightsPanel', async () => {
            const panel = InsightsPanel.createOrShow(context.extensionUri);
            commands.setInsightsPanel(panel.getPanel());

            // Set up panel callbacks
            panel.setNavigateToInsightCallback(async (insightId: string) => {
                await commands.navigateToInsight(insightId);
            });

            panel.setExportResultsCallback(async (_format: 'json' | 'csv' | 'markdown') => {
                await commands.exportInsights();
            });

            panel.setRefreshAnalysisCallback(async () => {
                await commands.analyze();
            });

            // Send current insights to panel
            const currentInsights = commands.getCurrentInsights();
            if (currentInsights.length > 0) {
                panel.updateInsights(currentInsights);
            }
        })
    );

    // Internal command for creating insights panel (called from commands.ts)
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.internal.createInsightsPanel', async () => {
            await vscode.commands.executeCommand('loggerUsage.showInsightsPanel');
        })
    );

    // Select solution command
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.selectSolution', async () => {
            await commands.selectSolution();
        })
    );

    // Export insights command
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.exportInsights', async () => {
            await commands.exportInsights();
        })
    );

    // Clear filters command
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.clearFilters', async () => {
            await commands.clearFilters();
        })
    );

    // Navigate to insight command
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.navigateToInsight', async (insightId: string) => {
            await commands.navigateToInsight(insightId);
        })
    );

    // Refresh tree view command
    context.subscriptions.push(
        vscode.commands.registerCommand('loggerUsage.refreshTreeView', async () => {
            await commands.refreshTreeView();
        })
    );

    outputChannel.appendLine('Commands registered successfully');
}

/**
 * Sets up webview panel callbacks
 */
function setupWebviewCallbacks(): void {
    // Callbacks are set up when panel is created in showInsightsPanel command
    // This ensures fresh callbacks with current context
}

/**
 * Sets up file watchers for auto-analysis on save
 */
function setupFileWatchers(context: vscode.ExtensionContext): void {
    // Watch for C# file saves
    const fileWatcher = vscode.workspace.onDidSaveTextDocument(async (document) => {
        if (document.languageId === 'csharp') {
            outputChannel.appendLine(`File saved: ${document.fileName}`);

            const activeSolution = commands.getActiveSolutionPath();
            if (activeSolution) {
                try {
                    await commands.analyzeFile(document.uri);
                } catch (error) {
                    outputChannel.appendLine(`Auto-analysis failed: ${error}`);
                }
            }
        }
    });

    context.subscriptions.push(fileWatcher);
    outputChannel.appendLine('File watchers enabled (auto-analyze on save)');
}

/**
 * Handles configuration changes
 */
function handleConfigurationChange(e: vscode.ConfigurationChangeEvent): void {
    outputChannel.appendLine('Configuration changed');

    // If auto-analyze setting changed, restart file watchers if needed
    // (This is handled by setupFileWatchers being conditional)

    // If problems integration is disabled, clear diagnostics
    if (e.affectsConfiguration('loggerUsage.enableProblemsIntegration')) {
        const enabled = Configuration.getEnableProblemsIntegration();
        if (!enabled) {
            problemsProvider.clearDiagnostics();
            outputChannel.appendLine('Problems integration disabled - diagnostics cleared');
        }
    }
}

/**
 * Updates status bar item
 */
function updateStatusBar(text: string, busy: boolean): void {
    if (statusBarItem) {
        statusBarItem.text = busy ? `$(sync~spin) ${text}` : `$(search) ${text}`;
    }
}

/**
 * Shows welcome message on first activation
 */
function showWelcomeMessage(): void {
    vscode.window.showInformationMessage(
        'Logger Usage extension activated! Press Ctrl+Shift+L to analyze logging in your workspace.',
        'Analyze Now',
        'View Documentation'
    ).then(selection => {
        if (selection === 'Analyze Now') {
            vscode.commands.executeCommand('loggerUsage.analyze');
        } else if (selection === 'View Documentation') {
            vscode.env.openExternal(vscode.Uri.parse('https://github.com/Meir017/dotnet-logging-usage#readme'));
        }
    });
}
