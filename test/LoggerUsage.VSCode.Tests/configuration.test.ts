import * as assert from 'assert';
import * as vscode from 'vscode';
import { Configuration } from '../../src/LoggerUsage.VSCode/src/configuration';

suite('Configuration Test Suite', () => {
  vscode.window.showInformationMessage('Start configuration tests.');

  // Clean up after each test
  teardown(async () => {
    try {
      await Configuration.resetToDefaults();
    } catch (error) {
      // Ignore errors if no workspace is open
      console.log('Skipping configuration reset (no workspace)');
    }
  });

  test('getAutoAnalyzeOnSave should return default value', () => {
    const value = Configuration.getAutoAnalyzeOnSave();
    assert.strictEqual(typeof value, 'boolean', 'Should return a boolean');
    assert.strictEqual(value, true, 'Default should be true');
  });

  test('getExcludePatterns should return default patterns', () => {
    const patterns = Configuration.getExcludePatterns();
    assert.ok(Array.isArray(patterns), 'Should return an array');
    assert.ok(patterns.length > 0, 'Should have at least one pattern');
    assert.ok(patterns.includes('**/obj/**'), 'Should include obj pattern');
    assert.ok(patterns.includes('**/bin/**'), 'Should include bin pattern');
  });

  test('getMaxFilesPerAnalysis should return default value', () => {
    const value = Configuration.getMaxFilesPerAnalysis();
    assert.strictEqual(typeof value, 'number', 'Should return a number');
    assert.ok(value > 0, 'Should be positive');
    assert.strictEqual(value, 1000, 'Default should be 1000');
  });

  test('getAnalysisTimeoutMs should return default value', () => {
    const value = Configuration.getAnalysisTimeoutMs();
    assert.strictEqual(typeof value, 'number', 'Should return a number');
    assert.ok(value > 0, 'Should be positive');
    assert.strictEqual(value, 300000, 'Default should be 300000 (5 minutes)');
  });

  test('getEnableProblemsIntegration should return default value', () => {
    const value = Configuration.getEnableProblemsIntegration();
    assert.strictEqual(typeof value, 'boolean', 'Should return a boolean');
    assert.strictEqual(value, true, 'Default should be true');
  });

  test('getDefaultLogLevels should return default levels', () => {
    const levels = Configuration.getDefaultLogLevels();
    assert.ok(Array.isArray(levels), 'Should return an array');
    assert.ok(levels.length > 0, 'Should have at least one level');
    assert.ok(levels.includes('Information'), 'Should include Information');
    assert.ok(levels.includes('Warning'), 'Should include Warning');
    assert.ok(levels.includes('Error'), 'Should include Error');
  });

  test('getShowInconsistenciesOnly should return default value', () => {
    const value = Configuration.getShowInconsistenciesOnly();
    assert.strictEqual(typeof value, 'boolean', 'Should return a boolean');
    assert.strictEqual(value, false, 'Default should be false');
  });

  test('updateConfig should update configuration value', async function() {
    // Skip if no workspace is open
    if (!vscode.workspace.workspaceFolders) {
      this.skip();
      return;
    }

    const newValue = false;
    await Configuration.updateConfig('autoAnalyzeOnSave', newValue);

    // Wait a bit for VS Code to apply the change
    await new Promise(resolve => setTimeout(resolve, 100));

    const value = Configuration.getAutoAnalyzeOnSave();
    assert.strictEqual(value, newValue, 'Should update the configuration value');
  });

  test('updateConfig should handle nested configuration keys', async function() {
    // Skip if no workspace is open
    if (!vscode.workspace.workspaceFolders) {
      this.skip();
      return;
    }

    const newValue = 2000;
    await Configuration.updateConfig('performanceThresholds.maxFilesPerAnalysis', newValue);

    await new Promise(resolve => setTimeout(resolve, 100));

    const value = Configuration.getMaxFilesPerAnalysis();
    assert.strictEqual(value, newValue, 'Should update nested configuration');
  });

  test('resetToDefaults should restore all default values', async function() {
    // Skip if no workspace is open
    if (!vscode.workspace.workspaceFolders) {
      this.skip();
      return;
    }

    // Change some values
    await Configuration.updateConfig('autoAnalyzeOnSave', false);
    await Configuration.updateConfig('enableProblemsIntegration', false);

    await new Promise(resolve => setTimeout(resolve, 100));

    // Reset to defaults
    await Configuration.resetToDefaults();

    await new Promise(resolve => setTimeout(resolve, 100));

    // Verify defaults are restored
    const autoAnalyze = Configuration.getAutoAnalyzeOnSave();
    const problemsIntegration = Configuration.getEnableProblemsIntegration();

    assert.strictEqual(autoAnalyze, true, 'autoAnalyzeOnSave should be reset to true');
    assert.strictEqual(problemsIntegration, true, 'enableProblemsIntegration should be reset to true');
  });

  test('onDidChangeConfiguration should fire when configuration changes', function(done) {
    // Skip if no workspace is open
    if (!vscode.workspace.workspaceFolders) {
      this.skip();
      return;
    }

    const disposable = Configuration.onDidChangeConfiguration((e) => {
      assert.ok(e.affectsConfiguration('loggerUsage'), 'Event should affect loggerUsage section');
      disposable.dispose();
      done();
    });

    // Trigger configuration change
    Configuration.updateConfig('autoAnalyzeOnSave', false).catch(err => {
      disposable.dispose();
      done(err);
    });
  });

  test('Configuration should handle invalid keys gracefully', () => {
    // This should return the default value even if key doesn't exist
    const config = vscode.workspace.getConfiguration('loggerUsage');
    const value = config.get('nonExistentKey', 'defaultValue');
    assert.strictEqual(value, 'defaultValue', 'Should return default for non-existent key');
  });

  test('Configuration should validate numeric ranges', () => {
    const maxFiles = Configuration.getMaxFilesPerAnalysis();
    const timeout = Configuration.getAnalysisTimeoutMs();

    assert.ok(maxFiles >= 0, 'maxFilesPerAnalysis should be non-negative');
    assert.ok(timeout >= 0, 'analysisTimeoutMs should be non-negative');
  });

  test('Configuration should handle empty arrays', async function() {
    // Skip if no workspace is open
    if (!vscode.workspace.workspaceFolders) {
      this.skip();
      return;
    }

    await Configuration.updateConfig('excludePatterns', []);

    await new Promise(resolve => setTimeout(resolve, 100));

    const patterns = Configuration.getExcludePatterns();
    assert.ok(Array.isArray(patterns), 'Should still return an array');
  });
});
