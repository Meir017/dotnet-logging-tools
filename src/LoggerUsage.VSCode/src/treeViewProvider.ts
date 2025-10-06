import * as vscode from 'vscode';
import * as path from 'path';
import { LoggingInsight } from '../models/insightViewModel';

/**
 * Tree node types for the logger usage tree view
 */
enum TreeNodeType {
    Solution = 'solution',
    Project = 'project',
    File = 'file',
    Insight = 'insight',
    Empty = 'empty'
}

/**
 * Represents a node in the tree view
 */
interface TreeNode {
    type: TreeNodeType;
    label: string;
    description?: string;
    tooltip?: string;
    children?: TreeNode[];
    insight?: LoggingInsight;
    filePath?: string;
    projectName?: string;
    collapsibleState?: vscode.TreeItemCollapsibleState;
}

/**
 * Tree data provider for logging insights in Explorer sidebar
 */
export class LoggerTreeViewProvider implements vscode.TreeDataProvider<TreeNode>, vscode.Disposable {
    private _onDidChangeTreeData: vscode.EventEmitter<TreeNode | undefined | null | void> = new vscode.EventEmitter<TreeNode | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<TreeNode | undefined | null | void> = this._onDidChangeTreeData.event;

    private currentInsights: LoggingInsight[] = [];
    private solutionPath: string | null = null;

    constructor() {}

    /**
     * Updates the insights data and triggers a refresh of the tree view
     */

    /**
     * Updates insights and refreshes tree
     */
    public updateInsights(insights: LoggingInsight[], solutionPath?: string): void {
        this.currentInsights = insights;
        if (solutionPath) {
            this.solutionPath = solutionPath;
        }
        this.refresh();
    }

    /**
     * Refreshes the tree view
     */
    public refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    /**
     * Gets tree item representation for a node
     */
    public getTreeItem(element: TreeNode): vscode.TreeItem {
        const treeItem = new vscode.TreeItem(
            element.label,
            element.collapsibleState ?? this.getCollapsibleState(element)
        );

        treeItem.description = element.description;
        treeItem.tooltip = element.tooltip || element.label;
        treeItem.iconPath = this.getIcon(element);
        treeItem.contextValue = element.type;

        // Set command for insight nodes (click to navigate)
        if (element.type === TreeNodeType.Insight && element.insight) {
            treeItem.command = {
                command: 'loggerUsage.navigateToInsight',
                title: 'Navigate to Insight',
                arguments: [element.insight.id]
            };
        }

        return treeItem;
    }

    /**
     * Gets children of a tree node
     */
    public getChildren(element?: TreeNode): Thenable<TreeNode[]> {
        if (!element) {
            // Root level - return solution node or empty state
            return Promise.resolve(this.getRootNodes());
        }

        switch (element.type) {
            case TreeNodeType.Solution:
                return Promise.resolve(this.getProjectNodes());
            case TreeNodeType.Project:
                return Promise.resolve(this.getFileNodes(element.projectName!));
            case TreeNodeType.File:
                return Promise.resolve(this.getInsightNodes(element.filePath!));
            default:
                return Promise.resolve([]);
        }
    }

    /**
     * Disposes of the tree view provider
     */
    public dispose(): void {
        this._onDidChangeTreeData.dispose();
    }

    // ==================== Private Methods ====================

    /**
     * Gets root nodes (solution or empty state)
     */
    private getRootNodes(): TreeNode[] {
        if (this.currentInsights.length === 0) {
            return [{
                type: TreeNodeType.Empty,
                label: 'No logging insights',
                description: 'Run analysis to view insights',
                collapsibleState: vscode.TreeItemCollapsibleState.None
            }];
        }

        const solutionName = this.solutionPath 
            ? path.basename(this.solutionPath, '.sln')
            : 'Workspace';

        return [{
            type: TreeNodeType.Solution,
            label: solutionName,
            description: `${this.currentInsights.length} insights`,
            tooltip: this.solutionPath || 'Current workspace',
            collapsibleState: vscode.TreeItemCollapsibleState.Expanded
        }];
    }

    /**
     * Gets project nodes (grouped by project/directory)
     */
    private getProjectNodes(): TreeNode[] {
        const projectMap = new Map<string, LoggingInsight[]>();

        // Group insights by project (inferred from file path)
        for (const insight of this.currentInsights) {
            const projectName = this.inferProjectName(insight.location.filePath);
            
            if (!projectMap.has(projectName)) {
                projectMap.set(projectName, []);
            }
            projectMap.get(projectName)!.push(insight);
        }

        // Create project nodes
        const projectNodes: TreeNode[] = [];
        for (const [projectName, insights] of projectMap) {
            projectNodes.push({
                type: TreeNodeType.Project,
                label: projectName,
                description: `${insights.length} insights`,
                projectName: projectName,
                collapsibleState: vscode.TreeItemCollapsibleState.Collapsed
            });
        }

        // Sort by project name
        projectNodes.sort((a, b) => a.label.localeCompare(b.label));

        return projectNodes;
    }

    /**
     * Gets file nodes for a project
     */
    private getFileNodes(projectName: string): TreeNode[] {
        // Filter insights for this project
        const projectInsights = this.currentInsights.filter(
            i => this.inferProjectName(i.location.filePath) === projectName
        );

        // Group by file
        const fileMap = new Map<string, LoggingInsight[]>();
        for (const insight of projectInsights) {
            const filePath = insight.location.filePath;
            
            if (!fileMap.has(filePath)) {
                fileMap.set(filePath, []);
            }
            fileMap.get(filePath)!.push(insight);
        }

        // Create file nodes
        const fileNodes: TreeNode[] = [];
        for (const [filePath, insights] of fileMap) {
            const fileName = path.basename(filePath);
            const inconsistenciesCount = insights.filter(i => i.hasInconsistencies).length;
            
            let description = `${insights.length} insights`;
            if (inconsistenciesCount > 0) {
                description += ` (${inconsistenciesCount} issues)`;
            }

            fileNodes.push({
                type: TreeNodeType.File,
                label: fileName,
                description: description,
                tooltip: filePath,
                filePath: filePath,
                collapsibleState: vscode.TreeItemCollapsibleState.Collapsed
            });
        }

        // Sort by file name
        fileNodes.sort((a, b) => a.label.localeCompare(b.label));

        return fileNodes;
    }

    /**
     * Gets insight nodes for a file
     */
    private getInsightNodes(filePath: string): TreeNode[] {
        // Filter insights for this file
        const fileInsights = this.currentInsights.filter(
            i => i.location.filePath === filePath
        );

        // Create insight nodes
        const insightNodes: TreeNode[] = fileInsights.map(insight => {
            const line = insight.location.startLine;
            const methodType = this.getShortMethodType(insight.methodType);
            const logLevel = insight.logLevel || '?';
            
            // Create label
            let label = `Line ${line}: ${methodType}`;
            
            // Add inconsistency indicator
            if (insight.hasInconsistencies) {
                label += ' ⚠️';
            }

            // Create description (log level)
            const description = logLevel;

            // Create tooltip (message template preview)
            let tooltip = `${insight.messageTemplate}`;
            if (insight.eventId) {
                tooltip += `\nEvent ID: ${insight.eventId.id}`;
            }
            if (insight.hasInconsistencies && insight.inconsistencies) {
                tooltip += `\n\nIssues:\n${insight.inconsistencies.map(inc => `• ${inc.message}`).join('\n')}`;
            }

            return {
                type: TreeNodeType.Insight,
                label: label,
                description: description,
                tooltip: tooltip,
                insight: insight,
                collapsibleState: vscode.TreeItemCollapsibleState.None
            };
        });

        // Sort by line number
        insightNodes.sort((a, b) => {
            const lineA = a.insight?.location.startLine || 0;
            const lineB = b.insight?.location.startLine || 0;
            return lineA - lineB;
        });

        return insightNodes;
    }

    /**
     * Infers project name from file path
     */
    private inferProjectName(filePath: string): string {
        // Look for .csproj directory in path
        const parts = filePath.split(/[\\/]/);
        
        // Find the directory containing a likely project file
        // (heuristic: directory before 'src' or similar, or second-to-last directory)
        for (let i = parts.length - 1; i >= 0; i--) {
            const part = parts[i];
            
            // If we find common source folders, the previous part might be project name
            if (part.toLowerCase() === 'src' || 
                part.toLowerCase() === 'lib' || 
                part.toLowerCase() === 'app') {
                if (i + 1 < parts.length) {
                    return parts[i + 1];
                }
            }
            
            // Look for .csproj-like names (capitalized, PascalCase)
            if (part && part[0] === part[0].toUpperCase() && part.includes('.')) {
                return part;
            }
        }

        // Fallback: use directory name before filename
        if (parts.length >= 2) {
            return parts[parts.length - 2];
        }

        return 'Unknown';
    }

    /**
     * Gets collapsible state for a node
     */
    private getCollapsibleState(element: TreeNode): vscode.TreeItemCollapsibleState {
        switch (element.type) {
            case TreeNodeType.Solution:
                return vscode.TreeItemCollapsibleState.Expanded;
            case TreeNodeType.Project:
            case TreeNodeType.File:
                return vscode.TreeItemCollapsibleState.Collapsed;
            case TreeNodeType.Insight:
            case TreeNodeType.Empty:
            default:
                return vscode.TreeItemCollapsibleState.None;
        }
    }

    /**
     * Gets icon for a node type
     */
    private getIcon(element: TreeNode): vscode.ThemeIcon | undefined {
        switch (element.type) {
            case TreeNodeType.Solution:
                return new vscode.ThemeIcon('folder-library');
            case TreeNodeType.Project:
                return new vscode.ThemeIcon('project');
            case TreeNodeType.File:
                return new vscode.ThemeIcon('file-code');
            case TreeNodeType.Insight:
                if (element.insight?.hasInconsistencies) {
                    return new vscode.ThemeIcon('warning', new vscode.ThemeColor('problemsWarningIcon.foreground'));
                }
                return new vscode.ThemeIcon('symbol-event');
            case TreeNodeType.Empty:
                return new vscode.ThemeIcon('info');
            default:
                return undefined;
        }
    }

    /**
     * Gets short method type name
     */
    private getShortMethodType(methodType: string): string {
        switch (methodType) {
            case 'LoggerExtension':
                return 'Log';
            case 'LoggerMessageAttribute':
                return 'LogMsg';
            case 'LoggerMessageDefine':
                return 'LogDef';
            case 'BeginScope':
                return 'Scope';
            default:
                return methodType;
        }
    }
}
