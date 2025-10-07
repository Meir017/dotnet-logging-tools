import * as path from 'path';
import Mocha from 'mocha';
import * as fs from 'fs';
import * as vscode from 'vscode';

export async function run(): Promise<void> {
  // Wait for extension activation BEFORE creating test runner
  console.log('Waiting for extension to activate...');
  await waitForExtensionActivation();
  console.log('Extension activation check complete. Starting tests...');

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

async function waitForExtensionActivation(): Promise<void> {
  const maxWaitTime = 30000; // 30 seconds max
  const startTime = Date.now();

  while (Date.now() - startTime < maxWaitTime) {
    try {
      const commands = await vscode.commands.getCommands(true);
      const extensionCommands = commands.filter(cmd => cmd.startsWith('loggerUsage.'));

      if (extensionCommands.length > 0) {
        console.log(`✓ Extension activated successfully. Found ${extensionCommands.length} commands: ${extensionCommands.join(', ')}`);
        return; // Extension is ready
      }
    } catch (error) {
      console.error('Error checking for extension commands:', error);
    }

    // Wait 1 second before checking again
    await new Promise(resolve => setTimeout(resolve, 1000));
    console.log(`  Still waiting for extension... (${Math.floor((Date.now() - startTime) / 1000)}s elapsed)`);
  }

  // If we get here, extension didn't activate in time
  console.error('✗ WARNING: Extension did not activate within 30 seconds');
  console.error('  Tests may fail due to missing extension functionality');
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
