import * as vscode from 'vscode';
import { SolutionInfo } from '../utils/solutionDetector';

/**
 * Manages the state of solutions in the workspace
 * Tracks which solution is currently active for analysis
 */
export class SolutionState implements vscode.Disposable {
    private _allSolutions: SolutionInfo[] = [];
    private _activeSolution: SolutionInfo | null = null;
    private _onDidChangeSolution = new vscode.EventEmitter<SolutionInfo | null>();

    /**
     * Event fired when the active solution changes
     */
    public readonly onDidChangeSolution: vscode.Event<SolutionInfo | null> = this._onDidChangeSolution.event;

    constructor() {}

    /**
     * Gets all discovered solutions
     */
    public getAllSolutions(): SolutionInfo[] {
        return [...this._allSolutions];
    }

    /**
     * Sets all available solutions
     */
    public setAllSolutions(solutions: SolutionInfo[]): void {
        this._allSolutions = solutions;
    }

    /**
     * Gets the currently active solution
     */
    public getActiveSolution(): SolutionInfo | null {
        return this._activeSolution;
    }

    /**
     * Sets the active solution
     */
    public setActiveSolution(solution: SolutionInfo | null): void {
        const previousSolution = this._activeSolution;

        this._activeSolution = solution;

        // Fire event only if solution actually changed
        if (previousSolution?.filePath !== solution?.filePath) {
            this._onDidChangeSolution.fire(solution);
        }
    }

    /**
     * Checks if there is an active solution
     */
    public hasActiveSolution(): boolean {
        return this._activeSolution !== null;
    }

    /**
     * Checks if multiple solutions are available
     */
    public hasMultipleSolutions(): boolean {
        return this._allSolutions.length > 1;
    }

    /**
     * Gets the count of available solutions
     */
    public getSolutionCount(): number {
        return this._allSolutions.length;
    }

    /**
     * Finds a solution by file path
     */
    public findSolutionByPath(filePath: string): SolutionInfo | null {
        return this._allSolutions.find(s => s.filePath === filePath) || null;
    }

    /**
     * Finds a solution by display name
     */
    public findSolutionByName(displayName: string): SolutionInfo | null {
        return this._allSolutions.find(s => s.displayName === displayName) || null;
    }

    /**
     * Gets the active solution's file path (convenience method)
     */
    public getActiveSolutionPath(): string | null {
        return this._activeSolution?.filePath || null;
    }

    /**
     * Disposes of the solution state
     */
    public dispose(): void {
        this._onDidChangeSolution.dispose();
    }
}

/**
 * Singleton instance of SolutionState
 * Use this throughout the extension for consistent state management
 */
let _instance: SolutionState | null = null;

/**
 * Gets the singleton instance of SolutionState
 */
export function getSolutionState(): SolutionState {
    if (!_instance) {
        _instance = new SolutionState();
    }
    return _instance;
}

/**
 * Resets the singleton instance (for testing)
 */
export function resetSolutionState(): void {
    if (_instance) {
        _instance.dispose();
        _instance = null;
    }
}
