const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

console.log('Building C# Bridge executable...');

// Paths
const bridgeProjectPath = path.resolve(__dirname, '../../LoggerUsage.VSCode.Bridge/LoggerUsage.VSCode.Bridge.csproj');
const extensionBridgePath = path.resolve(__dirname, '../bridge');

// Platforms to build for
const platforms = [
  { rid: 'win-x64', outputDir: 'win-x64' },
  { rid: 'linux-x64', outputDir: 'linux-x64' },
  { rid: 'osx-x64', outputDir: 'osx-x64' },
  { rid: 'osx-arm64', outputDir: 'osx-arm64' }
];

// Clean and create bridge directory
if (fs.existsSync(extensionBridgePath)) {
  fs.rmSync(extensionBridgePath, { recursive: true, force: true });
}
fs.mkdirSync(extensionBridgePath, { recursive: true });

// Build for each platform
for (const platform of platforms) {
  console.log(`  Building for ${platform.rid}...`);
  
  const outputPath = path.join(extensionBridgePath, platform.outputDir);
  
  try {
    // Build the bridge with dotnet publish
    execSync(
      `dotnet publish "${bridgeProjectPath}" -c Release -r ${platform.rid} --self-contained false -o "${outputPath}"`,
      { stdio: 'inherit' }
    );
    
    console.log(`  ✓ Built ${platform.rid} successfully`);
  } catch (error) {
    console.error(`  ✗ Failed to build ${platform.rid}:`, error.message);
    process.exit(1);
  }
}

console.log('✓ C# Bridge built successfully for all platforms');
