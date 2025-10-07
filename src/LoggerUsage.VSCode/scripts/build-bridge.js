const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

console.log('Building C# Bridge executable...');

// Paths
const bridgeProjectPath = path.resolve(__dirname, '../../LoggerUsage.VSCode.Bridge/LoggerUsage.VSCode.Bridge.csproj');
const bridgeOutputPath = path.resolve(__dirname, '../../LoggerUsage.VSCode.Bridge/bin/Release/net10.0');
const extensionBridgePath = path.resolve(__dirname, '../bridge');

// Clean and create bridge directory
if (fs.existsSync(extensionBridgePath)) {
  fs.rmSync(extensionBridgePath, { recursive: true, force: true });
}
fs.mkdirSync(extensionBridgePath, { recursive: true });

console.log('  Building Bridge in Release configuration...');

try {
  // Build the bridge in Release mode
  execSync(
    `dotnet build "${bridgeProjectPath}" -c Release`,
    { stdio: 'inherit' }
  );

  console.log('  ✓ Bridge built successfully');

  // Copy the build output to the extension's bridge folder
  console.log('  Copying binaries to extension folder...');

  if (!fs.existsSync(bridgeOutputPath)) {
    throw new Error(`Build output not found at: ${bridgeOutputPath}`);
  }

  // Recursively copy all files and directories from the build output to the bridge folder
  const copyRecursive = (src, dest) => {
    const entries = fs.readdirSync(src, { withFileTypes: true });
    let fileCount = 0;

    for (const entry of entries) {
      const srcPath = path.join(src, entry.name);
      const destPath = path.join(dest, entry.name);

      if (entry.isDirectory()) {
        fs.mkdirSync(destPath, { recursive: true });
        fileCount += copyRecursive(srcPath, destPath);
      } else {
        fs.copyFileSync(srcPath, destPath);
        fileCount++;
      }
    }

    return fileCount;
  };

  const copiedCount = copyRecursive(bridgeOutputPath, extensionBridgePath);
  console.log(`  ✓ Copied ${copiedCount} files and directories to bridge folder`);
  console.log('✓ C# Bridge bundled successfully');

} catch (error) {
  console.error('  ✗ Failed to build Bridge:', error.message);
  process.exit(1);
}
