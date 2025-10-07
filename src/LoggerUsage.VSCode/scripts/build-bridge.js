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
  
  // Copy all files from the build output to the bridge folder
  const files = fs.readdirSync(bridgeOutputPath);
  let copiedCount = 0;
  files.forEach(file => {
    const srcPath = path.join(bridgeOutputPath, file);
    const stats = fs.statSync(srcPath);
    if (stats.isFile()) {
      const destPath = path.join(extensionBridgePath, file);
      fs.copyFileSync(srcPath, destPath);
      copiedCount++;
    }
  });
  
  console.log(`  ✓ Copied ${copiedCount} files to bridge folder`);
  console.log('✓ C# Bridge bundled successfully');
  
} catch (error) {
  console.error('  ✗ Failed to build Bridge:', error.message);
  process.exit(1);
}
