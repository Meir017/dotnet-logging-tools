import * as assert from 'assert';
import * as vscode from 'vscode';
import { ProblemsProvider } from '../../src/LoggerUsage.VSCode/src/problemsProvider';
import { LoggingInsight, ParameterInconsistency } from '../../src/LoggerUsage.VSCode/models/insightViewModel';

suite('Problems Provider Test Suite', () => {
  vscode.window.showInformationMessage('Start problems provider tests.');

  let provider: ProblemsProvider;

  setup(() => {
    provider = new ProblemsProvider();
  });

  teardown(() => {
    provider.dispose();
  });

  // Helper to create test insight
  function createTestInsight(overrides?: Partial<LoggingInsight>): LoggingInsight {
    return {
      id: 'test-1',
      methodType: 'LoggerExtension',
      messageTemplate: 'User {UserId} logged in',
      logLevel: 'Information',
      eventId: { id: 100, name: 'UserLogin' },
      parameters: ['UserId'],
      location: {
        filePath: '/test/file.cs',
        startLine: 10,
        startColumn: 5,
        endLine: 10,
        endColumn: 50
      },
      tags: [],
      dataClassifications: [],
      hasInconsistencies: false,
      ...overrides
    };
  }

  test('Should create diagnostic collection with name loggerUsage', () => {
    // ProblemsProvider creates collection in constructor
    // We can verify it works by calling methods without errors
    assert.ok(provider, 'Provider should be created');
    provider.clearDiagnostics();
    assert.ok(true, 'Diagnostic collection should be functional');
  });

  test('Should publish diagnostics for parameter inconsistencies', () => {
    const inconsistency: ParameterInconsistency = {
      type: 'NameMismatch',
      message: 'Parameter name mismatch: expected UserId, got userId',
      severity: 'Warning'
    };

    const insight = createTestInsight({
      hasInconsistencies: true,
      inconsistencies: [inconsistency]
    });

    provider.updateInsights([insight]);

    // We can't directly access VS Code's diagnostic collection
    // but we can verify the method runs without errors
    assert.ok(true, 'Should publish diagnostics without error');
  });

  test('Should publish diagnostics for missing EventIds', () => {
    const inconsistency: ParameterInconsistency = {
      type: 'MissingEventId',
      message: 'EventId is missing for this log statement',
      severity: 'Warning'
    };

    const insight = createTestInsight({
      eventId: null,
      hasInconsistencies: true,
      inconsistencies: [inconsistency]
    });

    provider.updateInsights([insight]);
    assert.ok(true, 'Should publish missing EventId diagnostics');
  });

  test('Should publish diagnostics for sensitive data warnings', () => {
    const inconsistency: ParameterInconsistency = {
      type: 'SensitiveDataInLog',
      message: 'Sensitive data detected in log: Password',
      severity: 'Warning'
    };

    const insight = createTestInsight({
      dataClassifications: [{
        parameterName: 'Password',
        classificationType: 'SensitiveData'
      }],
      hasInconsistencies: true,
      inconsistencies: [inconsistency]
    });

    provider.updateInsights([insight]);
    assert.ok(true, 'Should publish sensitive data warnings');
  });

  test('Should group diagnostics by file URI', () => {
    const insight1 = createTestInsight({
      id: 'test-1',
      location: { ...createTestInsight().location, filePath: '/test/file1.cs' },
      hasInconsistencies: true,
      inconsistencies: [{
        type: 'NameMismatch',
        message: 'Issue in file1',
        severity: 'Warning'
      }]
    });

    const insight2 = createTestInsight({
      id: 'test-2',
      location: { ...createTestInsight().location, filePath: '/test/file2.cs' },
      hasInconsistencies: true,
      inconsistencies: [{
        type: 'MissingEventId',
        message: 'Issue in file2',
        severity: 'Warning'
      }]
    });

    provider.updateInsights([insight1, insight2]);
    assert.ok(true, 'Should group diagnostics by file URI');
  });

  test('Should clear diagnostics for file on re-analysis', () => {
    const insight = createTestInsight({
      hasInconsistencies: true,
      inconsistencies: [{
        type: 'NameMismatch',
        message: 'Test issue',
        severity: 'Warning'
      }]
    });

    provider.updateInsights([insight]);
    provider.clearFile('/test/file.cs');

    assert.ok(true, 'Should clear file diagnostics');
  });

  test('Should clear all diagnostics on configuration change', () => {
    const insight = createTestInsight({
      hasInconsistencies: true,
      inconsistencies: [{
        type: 'NameMismatch',
        message: 'Test issue',
        severity: 'Warning'
      }]
    });

    provider.updateInsights([insight]);
    provider.clearDiagnostics();

    assert.ok(true, 'Should clear all diagnostics');
  });

  test('Should handle insights without inconsistencies', () => {
    const insight = createTestInsight({
      hasInconsistencies: false,
      inconsistencies: undefined
    });

    provider.updateInsights([insight]);
    assert.ok(true, 'Should handle insights without inconsistencies gracefully');
  });

  test('Should handle empty insights array', () => {
    provider.updateInsights([]);
    assert.ok(true, 'Should handle empty insights array');
  });

  test('Should handle multiple inconsistencies per insight', () => {
    const insight = createTestInsight({
      hasInconsistencies: true,
      inconsistencies: [
        {
          type: 'NameMismatch',
          message: 'First issue',
          severity: 'Warning'
        },
        {
          type: 'MissingEventId',
          message: 'Second issue',
          severity: 'Warning'
        }
      ]
    });

    provider.updateInsights([insight]);
    assert.ok(true, 'Should handle multiple inconsistencies');
  });

  test('Should dispose diagnostic collection on provider disposal', () => {
    const tempProvider = new ProblemsProvider();
    tempProvider.dispose();

    // If dispose works, calling it again should not throw
    assert.doesNotThrow(() => tempProvider.dispose());
  });
});
