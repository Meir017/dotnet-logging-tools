import * as vscode from 'vscode';
import { LoggingInsight, ParameterInconsistency } from '../models/insightViewModel';

/**
 * Diagnostic codes for different inconsistency types
 */
enum DiagnosticCode {
    ParameterNameMismatch = 'LU001',
    MissingEventId = 'LU002',
    SensitiveDataInLog = 'LU003',
    UnknownInconsistency = 'LU999'
}

/**
 * Manages VS Code diagnostics (Problems panel) for logging inconsistencies
 */
export class ProblemsProvider implements vscode.Disposable {
    private readonly diagnosticCollection: vscode.DiagnosticCollection;
    private readonly diagnosticSource = 'LoggerUsage';

    constructor() {
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('loggerUsage');
    }

    /**
     * Updates diagnostics based on current insights
     */
    public updateInsights(insights: LoggingInsight[]): void {
        // Group insights by file
        const diagnosticsByFile = this.groupDiagnosticsByFile(insights);

        // Clear all existing diagnostics
        this.diagnosticCollection.clear();

        // Set diagnostics for each file
        for (const [uri, diagnostics] of diagnosticsByFile) {
            this.diagnosticCollection.set(uri, diagnostics);
        }
    }

    /**
     * Updates diagnostics for a specific file
     */
    public updateFile(filePath: string, insights: LoggingInsight[]): void {
        const uri = vscode.Uri.file(filePath);
        const diagnostics = this.createDiagnosticsForInsights(insights);
        
        this.diagnosticCollection.set(uri, diagnostics);
    }

    /**
     * Clears diagnostics for a specific file
     */
    public clearFile(filePath: string): void {
        const uri = vscode.Uri.file(filePath);
        this.diagnosticCollection.delete(uri);
    }

    /**
     * Clears all diagnostics
     */
    public clearDiagnostics(): void {
        this.diagnosticCollection.clear();
    }

    /**
     * Disposes of the diagnostic collection
     */
    public dispose(): void {
        this.diagnosticCollection.dispose();
    }

    // ==================== Private Methods ====================

    /**
     * Groups diagnostics by file URI
     */
    private groupDiagnosticsByFile(insights: LoggingInsight[]): Map<vscode.Uri, vscode.Diagnostic[]> {
        const diagnosticsByFile = new Map<vscode.Uri, vscode.Diagnostic[]>();

        // Only process insights with inconsistencies
        const insightsWithIssues = insights.filter(i => i.hasInconsistencies && i.inconsistencies);

        for (const insight of insightsWithIssues) {
            const uri = vscode.Uri.file(insight.location.filePath);
            
            if (!diagnosticsByFile.has(uri)) {
                diagnosticsByFile.set(uri, []);
            }

            const diagnostics = diagnosticsByFile.get(uri)!;
            const newDiagnostics = this.createDiagnosticsForInsight(insight);
            diagnostics.push(...newDiagnostics);
        }

        return diagnosticsByFile;
    }

    /**
     * Creates diagnostics for multiple insights
     */
    private createDiagnosticsForInsights(insights: LoggingInsight[]): vscode.Diagnostic[] {
        const diagnostics: vscode.Diagnostic[] = [];

        for (const insight of insights) {
            if (insight.hasInconsistencies && insight.inconsistencies) {
                diagnostics.push(...this.createDiagnosticsForInsight(insight));
            }
        }

        return diagnostics;
    }

    /**
     * Creates diagnostics for a single insight
     */
    private createDiagnosticsForInsight(insight: LoggingInsight): vscode.Diagnostic[] {
        if (!insight.inconsistencies || insight.inconsistencies.length === 0) {
            return [];
        }

        const diagnostics: vscode.Diagnostic[] = [];

        for (const inconsistency of insight.inconsistencies) {
            const diagnostic = this.createDiagnostic(insight, inconsistency);
            diagnostics.push(diagnostic);
        }

        return diagnostics;
    }

    /**
     * Creates a single diagnostic from an inconsistency
     */
    private createDiagnostic(insight: LoggingInsight, inconsistency: ParameterInconsistency): vscode.Diagnostic {
        // Determine the location for the diagnostic
        const location = inconsistency.location || insight.location;

        // Create the range (convert from 1-based to 0-based)
        const range = new vscode.Range(
            new vscode.Position(location.startLine - 1, location.startColumn - 1),
            new vscode.Position(location.endLine - 1, location.endColumn - 1)
        );

        // Map severity
        const severity = this.mapSeverity(inconsistency.severity);

        // Get diagnostic code
        const code = this.getDiagnosticCode(inconsistency.type);

        // Create diagnostic
        const diagnostic = new vscode.Diagnostic(
            range,
            inconsistency.message,
            severity
        );

        diagnostic.source = this.diagnosticSource;
        diagnostic.code = code;

        // Add additional metadata
        this.enhanceDiagnostic(diagnostic, insight, inconsistency);

        return diagnostic;
    }

    /**
     * Maps inconsistency severity to VS Code diagnostic severity
     */
    private mapSeverity(severity: 'Warning' | 'Error'): vscode.DiagnosticSeverity {
        switch (severity) {
            case 'Error':
                return vscode.DiagnosticSeverity.Error;
            case 'Warning':
                return vscode.DiagnosticSeverity.Warning;
            default:
                return vscode.DiagnosticSeverity.Information;
        }
    }

    /**
     * Gets diagnostic code for inconsistency type
     */
    private getDiagnosticCode(type: 'NameMismatch' | 'MissingEventId' | 'SensitiveDataInLog'): string {
        switch (type) {
            case 'NameMismatch':
                return DiagnosticCode.ParameterNameMismatch;
            case 'MissingEventId':
                return DiagnosticCode.MissingEventId;
            case 'SensitiveDataInLog':
                return DiagnosticCode.SensitiveDataInLog;
            default:
                return DiagnosticCode.UnknownInconsistency;
        }
    }

    /**
     * Enhances diagnostic with additional metadata
     */
    private enhanceDiagnostic(
        diagnostic: vscode.Diagnostic,
        insight: LoggingInsight,
        inconsistency: ParameterInconsistency
    ): void {
        // Add related information if available
        const relatedInfo: vscode.DiagnosticRelatedInformation[] = [];

        // Add context about the logging statement
        relatedInfo.push(
            new vscode.DiagnosticRelatedInformation(
                new vscode.Location(
                    vscode.Uri.file(insight.location.filePath),
                    new vscode.Range(
                        new vscode.Position(insight.location.startLine - 1, insight.location.startColumn - 1),
                        new vscode.Position(insight.location.endLine - 1, insight.location.endColumn - 1)
                    )
                ),
                `Logging statement: ${insight.messageTemplate}`
            )
        );

        // Add method type information
        relatedInfo.push(
            new vscode.DiagnosticRelatedInformation(
                new vscode.Location(
                    vscode.Uri.file(insight.location.filePath),
                    new vscode.Range(
                        new vscode.Position(insight.location.startLine - 1, 0),
                        new vscode.Position(insight.location.startLine - 1, 0)
                    )
                ),
                `Method Type: ${insight.methodType}, Log Level: ${insight.logLevel || 'Unknown'}`
            )
        );

        // Add inconsistency-specific information
        switch (inconsistency.type) {
            case 'NameMismatch':
                relatedInfo.push(
                    new vscode.DiagnosticRelatedInformation(
                        new vscode.Location(
                            vscode.Uri.file(insight.location.filePath),
                            new vscode.Range(
                                new vscode.Position(insight.location.startLine - 1, 0),
                                new vscode.Position(insight.location.startLine - 1, 0)
                            )
                        ),
                        `Expected parameters: ${insight.parameters.join(', ')}`
                    )
                );
                break;

            case 'MissingEventId':
                relatedInfo.push(
                    new vscode.DiagnosticRelatedInformation(
                        new vscode.Location(
                            vscode.Uri.file(insight.location.filePath),
                            new vscode.Range(
                                new vscode.Position(insight.location.startLine - 1, 0),
                                new vscode.Position(insight.location.startLine - 1, 0)
                            )
                        ),
                        'Consider adding an EventId for better log correlation and filtering'
                    )
                );
                break;

            case 'SensitiveDataInLog':
                relatedInfo.push(
                    new vscode.DiagnosticRelatedInformation(
                        new vscode.Location(
                            vscode.Uri.file(insight.location.filePath),
                            new vscode.Range(
                                new vscode.Position(insight.location.startLine - 1, 0),
                                new vscode.Position(insight.location.startLine - 1, 0)
                            )
                        ),
                        'Sensitive data classifications: ' + 
                        insight.dataClassifications.map(dc => `${dc.parameterName} (${dc.classificationType})`).join(', ')
                    )
                );
                break;
        }

        diagnostic.relatedInformation = relatedInfo;

        // Add tags for better categorization
        const tags: vscode.DiagnosticTag[] = [];
        
        // Mark as unnecessary if it's a warning about missing EventId
        if (inconsistency.type === 'MissingEventId') {
            // This is a suggestion, not critical
            tags.push(vscode.DiagnosticTag.Unnecessary);
        }

        // Mark as deprecated if sensitive data detected (for visibility)
        if (inconsistency.type === 'SensitiveDataInLog') {
            tags.push(vscode.DiagnosticTag.Deprecated);
        }

        if (tags.length > 0) {
            diagnostic.tags = tags;
        }
    }

    /**
     * Publishes diagnostics (alias for updateInsights for compatibility)
     */
    public publishDiagnostics(insights: LoggingInsight[]): void {
        this.updateInsights(insights);
    }

    /**
     * Clears diagnostics for a file (alias for clearFile for compatibility)
     */
    public clearFileDiagnostics(filePath: string): void {
        this.clearFile(filePath);
    }
}
