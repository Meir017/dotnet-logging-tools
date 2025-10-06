import * as path from 'path';
import Mocha from 'mocha';
import * as fs from 'fs';

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
