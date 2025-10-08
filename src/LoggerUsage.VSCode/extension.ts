import * as vscode from 'vscode';
import { AnalysisService } from './src/analysisService';
import { Commands } from './src/commands';
import { InsightsPanel } from './src/insightsPanel';
import { ProblemsProvider } from './src/problemsProvider';
import { LoggerTreeViewProvider } from './src/treeViewProvider';
import { Configuration } from './src/configuration';
import { debounceAsync } from './src/utils/debounce';
import { getSolutionState } from './src/state/SolutionState';
import { findAllSolutions, getDefaultSolution } from './src/utils/solutionDetector';

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

        // Initialize solution state
        await initializeSolutionState();

        // Listen for solution changes
        const solutionState = getSolutionState();
        context.subscriptions.push(
            solutionState.onDidChangeSolution(async (solution) => {
                if (solution) {
                    outputChannel.appendLine(`Active solution changed to: ${solution.displayName}`);
                    updateStatusBarForSolution(solution);
                    
                    // Clear current insights when switching solutions
                    problemsProvider.clearDiagnostics();
                    treeViewProvider.updateInsights([], solution.filePath);
                }
            })
        );
        context.subscriptions.push(solutionState);

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
    // Create a debounced version of analyzeFile to handle rapid saves
    const debouncedAnalyzeFile = debounceAsync(
        async (uri: vscode.Uri) => {
            try {
                await commands.analyzeFile(uri);
            } catch (error) {
                outputChannel.appendLine(`Auto-analysis failed: ${error}`);
            }
        },
        500 // 500ms debounce delay
    );

    // Watch for C# file saves
    const fileWatcher = vscode.workspace.onDidSaveTextDocument(async (document) => {
        // Handle .csproj or .sln files - trigger full re-analysis
        if (document.fileName.endsWith('.csproj') || document.fileName.endsWith('.sln')) {
            outputChannel.appendLine(`Project/Solution file modified: ${document.fileName}. Triggering full re-analysis...`);
            
            vscode.window.showInformationMessage(
                'Project structure changed. Re-analyzing workspace...',
                'Analyze Now'
            ).then(selection => {
                if (selection === 'Analyze Now') {
                    vscode.commands.executeCommand('loggerUsage.analyze');
                }
            });
            return;
        }

        // Handle C# files - trigger incremental analysis
        if (document.languageId === 'csharp') {
            outputChannel.appendLine(`C# file saved: ${document.fileName}`);

            const activeSolution = getSolutionState().getActiveSolution();
            if (activeSolution) {
                // Use debounced function to prevent multiple rapid analyses
                await debouncedAnalyzeFile(document.uri);
            }
        }
    });

    // Watch for file deletions
    const deleteWatcher = vscode.workspace.onDidDeleteFiles(async (event) => {
        for (const uri of event.files) {
            // Check if deleted file was a C# file
            if (uri.fsPath.endsWith('.cs')) {
                outputChannel.appendLine(`C# file deleted: ${uri.fsPath}`);
                
                // Remove insights for this file
                await commands.removeFileInsights(uri);
            }
        }
    });

    context.subscriptions.push(fileWatcher, deleteWatcher);
    outputChannel.appendLine('File watchers enabled (auto-analyze on save with 500ms debounce, file deletion handling, .csproj/.sln change detection)');

    // Watch for active editor changes to auto-switch solutions
    const editorWatcher = vscode.window.onDidChangeActiveTextEditor(async (editor) => {
        const autoSwitch = vscode.workspace.getConfiguration('loggerUsage').get<boolean>('autoSwitchSolution', false);
        
        if (!autoSwitch || !editor || editor.document.languageId !== 'csharp') {
            return;
        }

        const filePath = editor.document.uri.fsPath;
        const solutionState = getSolutionState();
        const currentSolution = solutionState.getActiveSolution();
        const allSolutions = solutionState.getAllSolutions();

        // Find which solution this file belongs to
        const { findSolutionForFile } = await import('./src/utils/solutionDetector');
        const targetSolution = await findSolutionForFile(filePath, allSolutions);

        // If file belongs to a different solution, switch to it
        if (targetSolution && currentSolution && targetSolution.filePath !== currentSolution.filePath) {
            outputChannel.appendLine(`Auto-switching to solution: ${targetSolution.displayName}`);
            solutionState.setActiveSolution(targetSolution);
        }
    });

    context.subscriptions.push(editorWatcher);
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
 * Updates status bar with solution information
 */
function updateStatusBarForSolution(solution: { displayName: string; filePath: string } | null): void {
    if (!statusBarItem) {
        return;
    }

    const solutionState = getSolutionState();
    const solutionCount = solutionState.getSolutionCount();

    if (!solution) {
        statusBarItem.text = '$(warning) No Solution';
        statusBarItem.tooltip = 'No solution selected. Click to select a solution.';
        statusBarItem.command = 'loggerUsage.selectSolution';
    } else {
        const countText = solutionCount > 1 ? ` (1 of ${solutionCount})` : '';
        statusBarItem.text = `$(database) ${solution.displayName}${countText}`;
        statusBarItem.tooltip = `Solution: ${solution.filePath}\nClick to ${solutionCount > 1 ? 'switch solution' : 'analyze'}`;
        statusBarItem.command = solutionCount > 1 ? 'loggerUsage.selectSolution' : 'loggerUsage.analyze';
    }
}

/**
 * Initializes solution state on activation
 */
async function initializeSolutionState(): Promise<void> {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders) {
        outputChannel.appendLine('No workspace folders open');
        return;
    }

    const solutionState = getSolutionState();
    
    // Find all solutions in workspace
    const solutions = await findAllSolutions(workspaceFolders);
    solutionState.setAllSolutions(solutions);

    outputChannel.appendLine(`Found ${solutions.length} solution(s) in workspace`);

    // Set default active solution (first one)
    if (solutions.length > 0) {
        const defaultSolution = await getDefaultSolution(workspaceFolders);
        if (defaultSolution) {
            solutionState.setActiveSolution(defaultSolution);
            outputChannel.appendLine(`Default solution set to: ${defaultSolution.displayName}`);
            updateStatusBarForSolution(defaultSolution);
        }
    } else {
        updateStatusBarForSolution(null);
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
