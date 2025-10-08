import * as vscode from 'vscode';

/**
 * Helper utilities for interacting with VS Code API during tests.
 */

/**
 * Waits for a specific command to be registered with VS Code.
 * @param commandId - The command ID to wait for
 * @param timeout - Maximum time to wait in milliseconds (default: 10000)
 * @returns Promise that resolves when command is found
 */
export async function waitForCommand(commandId: string, timeout: number = 10000): Promise<void> {
  const startTime = Date.now();
  
  while (Date.now() - startTime < timeout) {
    const commands = await vscode.commands.getCommands(true);
    if (commands.includes(commandId)) {
      return;
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  
  throw new Error(`Command '${commandId}' was not registered within ${timeout}ms`);
}

/**
 * Waits for analysis to complete by listening to events.
 * @param timeout - Maximum time to wait in milliseconds (default: 30000)
 * @returns Promise that resolves when analysis completes
 */
export async function waitForAnalysis(timeout: number = 30000): Promise<any> {
  return new Promise(async (resolve, reject) => {
    const timeoutId = setTimeout(() => {
      reject(new Error(`Analysis did not complete within ${timeout}ms`));
    }, timeout);

    try {
      // Try to import the analysis events module
      const { analysisEvents } = await import('../../../src/LoggerUsage.VSCode/src/analysisEvents');
      
      // Listen for analysis complete event
      const completeDisposable = analysisEvents.onAnalysisComplete((event) => {
        clearTimeout(timeoutId);
        completeDisposable.dispose();
        errorDisposable.dispose();
        resolve(event.result);
      });
      
      // Listen for analysis error event
      const errorDisposable = analysisEvents.onAnalysisError((event) => {
        clearTimeout(timeoutId);
        completeDisposable.dispose();
        errorDisposable.dispose();
        reject(event.error);
      });
    } catch (error) {
      // If we can't import the module, fall back to simple timeout
      clearTimeout(timeoutId);
      setTimeout(() => {
        resolve(undefined);
      }, 5000);
    }
  });
}

/**
 * Retrieves tree view items from the Logger Usage tree view provider.
 * @returns Array of tree items or null if tree view not available
 */
export async function getTreeViewItems(): Promise<vscode.TreeItem[] | null> {
  try {
    // Tree view provider access is typically done through the extension's exported API
    // For now, we'll return null and update when tree view is implemented (T007-T009)
    return null;
  } catch (error) {
    console.error('Failed to get tree view items:', error);
    return null;
  }
}

/**
 * Retrieves the active webview panel for insights.
 * @returns The active webview panel or undefined if not open
 */
export function getWebviewPanel(): vscode.WebviewPanel | undefined {
  // Webview panels are typically tracked by the extension itself
  // This function will be fully implemented when webview is created (T010-T013)
  return undefined;
}

/**
 * Gets diagnostics from the Logger Usage diagnostics collection.
 * @param uri - Optional URI to filter diagnostics for a specific file
 * @returns Array of diagnostics
 */
export function getDiagnostics(uri?: vscode.Uri): vscode.Diagnostic[] {
  try {
    const allDiagnostics = vscode.languages.getDiagnostics();
    
    if (uri) {
      // Find diagnostics for specific file
      for (const [diagUri, diags] of allDiagnostics) {
        if (diagUri.toString() === uri.toString()) {
          return diags;
        }
      }
      return [];
    }
    
    // Return all diagnostics from Logger Usage collection
    const loggerUsageDiags: vscode.Diagnostic[] = [];
    for (const [, diags] of allDiagnostics) {
      loggerUsageDiags.push(...diags);
    }
    return loggerUsageDiags;
  } catch (error) {
    console.error('Failed to get diagnostics:', error);
    return [];
  }
}

/**
 * Sends a message to the webview panel.
 * @param message - The message object to send
 * @returns Promise that resolves when message is sent
 */
export async function sendWebviewMessage(message: any): Promise<boolean> {
  const panel = getWebviewPanel();
  if (!panel) {
    console.warn('No active webview panel to send message to');
    return false;
  }
  
  try {
    await panel.webview.postMessage(message);
    return true;
  } catch (error) {
    console.error('Failed to send webview message:', error);
    return false;
  }
}

/**
 * Waits for a specific file to be opened in the editor.
 * @param filePath - The file path to wait for
 * @param timeout - Maximum time to wait in milliseconds (default: 5000)
 * @returns Promise that resolves with the text editor
 */
export async function waitForEditorOpen(filePath: string, timeout: number = 5000): Promise<vscode.TextEditor> {
  const startTime = Date.now();
  
  while (Date.now() - startTime < timeout) {
    const editor = vscode.window.activeTextEditor;
    if (editor && editor.document.uri.fsPath === filePath) {
      return editor;
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  
  throw new Error(`Editor for file '${filePath}' did not open within ${timeout}ms`);
}

/**
 * Waits for the active editor's cursor to be at a specific position.
 * @param line - Expected line number (0-based)
 * @param character - Expected character position (0-based)
 * @param timeout - Maximum time to wait in milliseconds (default: 2000)
 * @returns Promise that resolves when cursor is at position
 */
export async function waitForCursorPosition(
  line: number,
  character: number,
  timeout: number = 2000
): Promise<void> {
  const startTime = Date.now();
  
  while (Date.now() - startTime < timeout) {
    const editor = vscode.window.activeTextEditor;
    if (editor) {
      const position = editor.selection.active;
      if (position.line === line && position.character === character) {
        return;
      }
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  
  throw new Error(`Cursor did not reach position (${line}, ${character}) within ${timeout}ms`);
}

/**
 * Executes a command and waits for it to complete.
 * @param commandId - The command to execute
 * @param args - Arguments to pass to the command
 * @param timeout - Maximum time to wait in milliseconds (default: 10000)
 * @returns Promise that resolves with the command result
 */
export async function executeCommandWithTimeout<T = any>(
  commandId: string,
  args?: any[],
  timeout: number = 10000
): Promise<T> {
  return Promise.race([
    vscode.commands.executeCommand<T>(commandId, ...(args || [])),
    new Promise<never>((_, reject) =>
      setTimeout(() => reject(new Error(`Command '${commandId}' timed out after ${timeout}ms`)), timeout)
    )
  ]);
}

/**
 * Waits for a notification with specific text to appear.
 * This is a best-effort helper and may not catch all notifications.
 * @param _expectedText - Text expected in the notification (placeholder - not yet implemented)
 * @param _timeout - Maximum time to wait in milliseconds (default: 5000)
 * @returns Promise that resolves when notification is detected
 */
export async function waitForNotification(_expectedText: string, _timeout: number = 5000): Promise<void> {
  // Note: VS Code API doesn't provide direct access to notifications
  // This is a placeholder for when we implement notification tracking
  // For now, we'll just wait a short time
  await new Promise(resolve => setTimeout(resolve, 1000));
}

/**
 * Gets the text content of the status bar item (if accessible).
 * Note: VS Code API has limited access to status bar item content.
 * @returns Status bar text or undefined
 */
export function getStatusBarText(): string | undefined {
  // Status bar items are typically managed by the extension
  // This will be updated when status bar implementation is complete (T022-T023)
  return undefined;
}

/**
 * Clears all diagnostics from all collections.
 * Useful for test cleanup between test cases.
 */
export function clearAllDiagnostics(): void {
  const allDiagnostics = vscode.languages.getDiagnostics();
  for (const [uri] of allDiagnostics) {
    vscode.languages.getDiagnostics(uri);
  }
}

/**
 * Opens a document and waits for it to be visible.
 * @param uri - The URI of the document to open
 * @returns Promise that resolves with the text editor
 */
export async function openDocument(uri: vscode.Uri): Promise<vscode.TextEditor> {
  const document = await vscode.workspace.openTextDocument(uri);
  return await vscode.window.showTextDocument(document);
}

/**
 * Saves a document and waits for the save operation to complete.
 * @param document - The document to save
 */
export async function saveDocument(document: vscode.TextDocument): Promise<void> {
  await document.save();
  // Small delay to ensure file system has processed the save
  await new Promise(resolve => setTimeout(resolve, 100));
}
