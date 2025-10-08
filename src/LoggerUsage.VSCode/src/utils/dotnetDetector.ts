import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

/**
 * Result of .NET SDK detection
 */
export interface DotNetSdkCheckResult {
    installed: boolean;
    version?: string;
    error?: string;
}

/**
 * Checks if .NET SDK is installed by running 'dotnet --version'
 * @param timeoutMs - Timeout in milliseconds (default: 5000)
 * @returns Promise with check result
 */
export async function checkDotNetSdk(timeoutMs: number = 5000): Promise<DotNetSdkCheckResult> {
    try {
        // Create abort controller for timeout
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

        try {
            // Run dotnet --version command
            const { stdout, stderr } = await execAsync('dotnet --version', {
                signal: controller.signal,
                timeout: timeoutMs
            });

            clearTimeout(timeoutId);

            // Check if we got a version string
            const version = stdout.trim();
            if (version && version.length > 0) {
                return {
                    installed: true,
                    version: version
                };
            } else if (stderr && stderr.length > 0) {
                return {
                    installed: false,
                    error: `dotnet command failed: ${stderr.trim()}`
                };
            } else {
                return {
                    installed: false,
                    error: 'dotnet --version returned no output'
                };
            }
        } catch (error: any) {
            clearTimeout(timeoutId);

            if (error.name === 'AbortError') {
                return {
                    installed: false,
                    error: `Timeout: dotnet command did not respond within ${timeoutMs}ms`
                };
            }

            throw error;
        }
    } catch (error: any) {
        // Command not found or execution error
        if (error.code === 'ENOENT') {
            return {
                installed: false,
                error: '.NET SDK not found in PATH'
            };
        }

        return {
            installed: false,
            error: `Failed to check .NET SDK: ${error.message}`
        };
    }
}

/**
 * Gets the download URL for .NET SDK
 */
export function getDotNetDownloadUrl(): string {
    return 'https://dotnet.microsoft.com/download/dotnet/10.0';
}
