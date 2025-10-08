import * as vscode from 'vscode';
import * as path from 'path';

/**
 * Information about a discovered solution file
 */
export interface SolutionInfo {
    /** Full file system path to the .sln file */
    filePath: string;
    /** Display name (just the filename without path) */
    displayName: string;
    /** Relative path from workspace root */
    relativePath: string;
    /** Parent directory path */
    directory: string;
}

/**
 * Finds all solution files in the workspace
 * @param workspaceFolders - VS Code workspace folders to search
 * @param excludePatterns - Optional glob patterns to exclude
 * @returns Array of solution information objects
 */
export async function findAllSolutions(
    workspaceFolders?: readonly vscode.WorkspaceFolder[],
    excludePatterns?: string[]
): Promise<SolutionInfo[]> {
    if (!workspaceFolders || workspaceFolders.length === 0) {
        return [];
    }

    const solutions: SolutionInfo[] = [];

    for (const folder of workspaceFolders) {
        // Create glob pattern for .sln files
        const pattern = new vscode.RelativePattern(folder, '**/*.sln');

        // Create exclude pattern
        const exclude = excludePatterns ? excludePatterns.join(',') : '**/node_modules/**';

        // Find all .sln files
        const solutionFiles = await vscode.workspace.findFiles(pattern, exclude);

        for (const uri of solutionFiles) {
            const filePath = uri.fsPath;
            const displayName = path.basename(filePath, '.sln');
            const directory = path.dirname(filePath);
            const relativePath = path.relative(folder.uri.fsPath, filePath);

            solutions.push({
                filePath,
                displayName,
                relativePath,
                directory
            });
        }
    }

    // Sort by display name
    solutions.sort((a, b) => a.displayName.localeCompare(b.displayName));

    return solutions;
}

/**
 * Finds the solution file that contains a given file path
 * @param filePath - The file path to check
 * @param allSolutions - Array of all known solutions
 * @returns The solution info if found, null otherwise
 */
export function findSolutionForFile(
    filePath: string,
    allSolutions: SolutionInfo[]
): SolutionInfo | null {
    // Normalize path separators
    const normalizedFilePath = filePath.replace(/\\/g, '/');

    // Find the solution whose directory contains this file
    // Prefer the most specific match (longest common path)
    let bestMatch: SolutionInfo | null = null;
    let longestMatchLength = 0;

    for (const solution of allSolutions) {
        const solutionDir = solution.directory.replace(/\\/g, '/');

        // Check if file is under this solution's directory
        if (normalizedFilePath.startsWith(solutionDir)) {
            const matchLength = solutionDir.length;
            if (matchLength > longestMatchLength) {
                bestMatch = solution;
                longestMatchLength = matchLength;
            }
        }
    }

    return bestMatch;
}

/**
 * Gets the default solution (first one found)
 * @param workspaceFolders - VS Code workspace folders
 * @returns The first solution found, or null if none exist
 */
export async function getDefaultSolution(
    workspaceFolders?: readonly vscode.WorkspaceFolder[]
): Promise<SolutionInfo | null> {
    const solutions = await findAllSolutions(workspaceFolders);
    return solutions.length > 0 ? solutions[0] : null;
}

/**
 * Checks if a file path is a solution file
 * @param filePath - The file path to check
 * @returns True if the file is a .sln file
 */
export function isSolutionFile(filePath: string): boolean {
    return filePath.endsWith('.sln');
}

/**
 * Checks if multiple solutions exist in the workspace
 * @param workspaceFolders - VS Code workspace folders
 * @returns True if more than one solution exists
 */
export async function hasMultipleSolutions(
    workspaceFolders?: readonly vscode.WorkspaceFolder[]
): Promise<boolean> {
    const solutions = await findAllSolutions(workspaceFolders);
    return solutions.length > 1;
}

/**
 * Gets solution count in workspace
 * @param workspaceFolders - VS Code workspace folders
 * @returns Number of solutions found
 */
export async function getSolutionCount(
    workspaceFolders?: readonly vscode.WorkspaceFolder[]
): Promise<number> {
    const solutions = await findAllSolutions(workspaceFolders);
    return solutions.length;
}
