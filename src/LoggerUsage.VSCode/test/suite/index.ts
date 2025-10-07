import * as path from 'path';
import Mocha from 'mocha';
import * as fs from 'fs';
import * as vscode from 'vscode';

export function run(): Promise<void> {
  // Create the mocha test runner
  const mocha = new Mocha({
    ui: 'tdd',
    color: true,
    timeout: 10000
  });

  // Use correct relative path to compiled test files
  // From out/src/LoggerUsage.VSCode/test/suite, go up 4 levels to root, then to out/test/
  const testsRoot = path.resolve(__dirname, '../../../../test/LoggerUsage.VSCode.Tests');

  return new Promise((resolve, reject) => {
    try {
      // Find all test files recursively
      const files = findTestFiles(testsRoot);

      // Add files to the test suite
      files.forEach((f: string) => mocha.addFile(f));

      // Add global setup hook to wait for extension activation
      mocha.suite.beforeAll('Wait for extension activation', async function() {
        this.timeout(30000); // 30 seconds timeout
        
        // Wait for extension commands to be registered
        const maxWaitTime = 20000; // 20 seconds max
        const startTime = Date.now();
        
        while (Date.now() - startTime < maxWaitTime) {
          const commands = await vscode.commands.getCommands(true);
          const extensionCommands = commands.filter(cmd => cmd.startsWith('loggerUsage.'));
          
          if (extensionCommands.length > 0) {
            console.log(`Extension activated successfully. Found ${extensionCommands.length} commands.`);
            return; // Extension is ready
          }
          
          // Wait 500ms before checking again
          await new Promise(resolve => setTimeout(resolve, 500));
        }
        
        // If we get here, extension didn't activate in time
        console.warn('Warning: Extension did not activate within timeout period');
      });

      // Run the mocha test
      mocha.run((failures: number) => {
        if (failures > 0) {
          reject(new Error(`${failures} tests failed.`));
        } else {
          resolve();
        }
      });
    } catch (error) {
      console.error('Error running tests:', error);
      reject(error);
    }
  });
}

function findTestFiles(dir: string): string[] {
  const files: string[] = [];

  function traverse(currentPath: string) {
    const entries = fs.readdirSync(currentPath, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = path.join(currentPath, entry.name);

      if (entry.isDirectory()) {
        traverse(fullPath);
      } else if (entry.isFile() && entry.name.endsWith('.test.js')) {
        files.push(fullPath);
      }
    }
  }

  traverse(dir);
  return files;
}
