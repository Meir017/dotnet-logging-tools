import * as os from 'os';
import * as path from 'path';
import { runTests } from '@vscode/test-electron';

async function main() {
  try {
    // The folder containing the Extension Manifest package.json
    // __dirname is out/src/LoggerUsage.VSCode/test after compilation
    // Need to go up 4 levels to reach extension root (where package.json is)
    const extensionDevelopmentPath = path.resolve(__dirname, '../../../../');

    // The path to the extension test runner script
    const extensionTestsPath = path.resolve(__dirname, './suite/index');

    // Path to test workspace for integration tests
    // __dirname is out/src/LoggerUsage.VSCode/test after compilation
    // Need to go up to repo root, then to test/fixtures/sample-workspace
    const testWorkspacePath = path.resolve(__dirname, '../../../../../../test/fixtures/sample-workspace');

    // Use a short user-data-dir under os.tmpdir() to avoid socket path length
    // limits on macOS (Unix domain sockets are capped at 103 chars).
    const userDataDir = path.join(os.tmpdir(), 'vsc-test');

    // Download VS Code, unzip it and run the integration test
    await runTests({
      extensionDevelopmentPath,
      extensionTestsPath,
      launchArgs: [
        testWorkspacePath,  // Open test workspace on launch
        '--user-data-dir', userDataDir
      ]
    });
  } catch (err) {
    console.error('Failed to run tests:', err);
    process.exit(1);
  }
}

main();
