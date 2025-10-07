import * as path from 'path';
import { runTests } from '@vscode/test-electron';

async function main() {
  try {
    // The folder containing the Extension Manifest package.json
    const extensionDevelopmentPath = path.resolve(__dirname, '../../');

    // The path to the extension test runner script
    const extensionTestsPath = path.resolve(__dirname, './suite/index');

    // Path to test workspace for integration tests
    // __dirname is out/src/LoggerUsage.VSCode/test after compilation
    // Need to go up to repo root, then to test/fixtures/sample-workspace
    const testWorkspacePath = path.resolve(__dirname, '../../../../../../test/fixtures/sample-workspace');

    // Download VS Code, unzip it and run the integration test
    await runTests({
      extensionDevelopmentPath,
      extensionTestsPath,
      launchArgs: [
        '--disable-extensions',
        testWorkspacePath  // Open test workspace on launch
      ]
    });
  } catch (err) {
    console.error('Failed to run tests:', err);
    process.exit(1);
  }
}

main();
