import * as vscode from 'vscode';

/**
 * Event listener utilities for capturing and waiting for analysis events during tests.
 */

export interface CapturedEvent {
  eventName: string;
  timestamp: number;
  data?: any;
}

/**
 * Captures analysis lifecycle events for testing purposes.
 * Provides promise-based waiting for specific events.
 */
export class AnalysisEventCapture {
  private capturedEvents: CapturedEvent[] = [];
  private progressMessages: string[] = [];
  private eventEmitters: Map<string, vscode.EventEmitter<any>> = new Map();
  private disposables: vscode.Disposable[] = [];

  constructor() {
    // Will be initialized when analysis events module is available (T004)
  }

  /**
   * Starts listening to analysis events from the extension.
   * This requires the extension to expose its event emitters.
   */
  public startListening(): void {
    // Placeholder: will be implemented when analysisEvents module exists (T004)
    // For now, we capture events manually
  }

    /**
     * Waits for a specific event to occur.
     * @param eventName - Name of the event to wait for (e.g., 'analysisStarted', 'analysisComplete')
     * @param timeout - Maximum time to wait in milliseconds (default: 30000)
     * @returns Promise that resolves with event data when event occurs
     */
    public async waitForEvent(eventName: string, timeout: number = 30000): Promise<any> {
        return new Promise((resolve, reject) => {
            const timeoutId = setTimeout(() => {
                reject(new Error(`Event '${eventName}' did not occur within ${timeout}ms`));
            }, timeout);

            // Check if event already captured
            const existingEvent = this.capturedEvents.find(e => e.eventName === eventName);
            if (existingEvent) {
                clearTimeout(timeoutId);
                resolve(existingEvent.data);
                return;
            }

            // Poll for event (will be replaced with proper event subscription in T004)
            const checkInterval = setInterval(() => {
                const event = this.capturedEvents.find(e => e.eventName === eventName);
                if (event) {
                    clearInterval(checkInterval);
                    clearTimeout(timeoutId);
                    resolve(event.data);
                }
            }, 100);
        });
    }  /**
   * Manually captures an event (for testing before event system is implemented).
   * @param eventName - Name of the event
   * @param data - Event data
   */
  public captureEvent(eventName: string, data?: any): void {
    this.capturedEvents.push({
      eventName,
      timestamp: Date.now(),
      data
    });
  }

  /**
   * Captures a progress message.
   * @param message - The progress message
   */
  public captureProgressMessage(message: string): void {
    this.progressMessages.push(message);
  }

  /**
   * Gets all captured progress messages.
   * @returns Array of progress messages
   */
  public getProgressMessages(): string[] {
    return [...this.progressMessages];
  }

  /**
   * Gets all captured events.
   * @returns Array of captured events
   */
  public getCapturedEvents(): CapturedEvent[] {
    return [...this.capturedEvents];
  }

  /**
   * Gets events of a specific type.
   * @param eventName - Name of the event type
   * @returns Array of events matching the name
   */
  public getEventsByName(eventName: string): CapturedEvent[] {
    return this.capturedEvents.filter(e => e.eventName === eventName);
  }

  /**
   * Checks if a specific event has been captured.
   * @param eventName - Name of the event to check
   * @returns True if event was captured
   */
  public hasEvent(eventName: string): boolean {
    return this.capturedEvents.some(e => e.eventName === eventName);
  }

  /**
   * Waits for a sequence of events to occur in order.
   * @param eventNames - Array of event names in expected order
   * @param timeout - Maximum time to wait for all events (default: 60000)
   * @returns Promise that resolves with array of event data
   */
  public async waitForEventSequence(eventNames: string[], timeout: number = 60000): Promise<any[]> {
    const startTime = Date.now();
    const results: any[] = [];

    for (const eventName of eventNames) {
      const remainingTime = timeout - (Date.now() - startTime);
      if (remainingTime <= 0) {
        throw new Error(`Timeout waiting for event sequence. Got: ${results.length}/${eventNames.length} events`);
      }

      const data = await this.waitForEvent(eventName, remainingTime);
      results.push(data);
    }

    return results;
  }

  /**
   * Waits for any one of multiple events to occur.
   * @param eventNames - Array of event names
   * @param timeout - Maximum time to wait (default: 30000)
   * @returns Promise that resolves with the first event that occurs and its data
   */
  public async waitForAnyEvent(eventNames: string[], timeout: number = 30000): Promise<{ eventName: string; data: any }> {
    return new Promise((resolve, reject) => {
      const timeoutId = setTimeout(() => {
        reject(new Error(`None of the events [${eventNames.join(', ')}] occurred within ${timeout}ms`));
      }, timeout);

      const checkInterval = setInterval(() => {
        for (const eventName of eventNames) {
          const event = this.capturedEvents.find(e => e.eventName === eventName);
          if (event) {
            clearInterval(checkInterval);
            clearTimeout(timeoutId);
            resolve({ eventName: event.eventName, data: event.data });
            return;
          }
        }
      }, 100);
    });
  }

  /**
   * Resets all captured events and messages.
   * Should be called between test cases.
   */
  public reset(): void {
    this.capturedEvents = [];
    this.progressMessages = [];
  }

  /**
   * Gets the most recent event of a specific type.
   * @param eventName - Name of the event
   * @returns The most recent event or undefined
   */
  public getLatestEvent(eventName: string): CapturedEvent | undefined {
    const events = this.getEventsByName(eventName);
    return events.length > 0 ? events[events.length - 1] : undefined;
  }

  /**
   * Gets the count of events of a specific type.
   * @param eventName - Name of the event
   * @returns Count of events
   */
  public getEventCount(eventName: string): number {
    return this.getEventsByName(eventName).length;
  }

  /**
   * Waits for an event with specific data matching a predicate.
   * @param eventName - Name of the event
   * @param predicate - Function to test event data
   * @param timeout - Maximum time to wait (default: 30000)
   * @returns Promise that resolves with matching event data
   */
  public async waitForEventWithData(
    eventName: string,
    predicate: (data: any) => boolean,
    timeout: number = 30000
  ): Promise<any> {
    return new Promise((resolve, reject) => {
      const timeoutId = setTimeout(() => {
        reject(new Error(`Event '${eventName}' with matching data did not occur within ${timeout}ms`));
      }, timeout);

      const checkInterval = setInterval(() => {
        const events = this.getEventsByName(eventName);
        const matchingEvent = events.find(e => predicate(e.data));
        if (matchingEvent) {
          clearInterval(checkInterval);
          clearTimeout(timeoutId);
          resolve(matchingEvent.data);
        }
      }, 100);
    });
  }

  /**
   * Disposes all event listeners and cleans up resources.
   */
  public dispose(): void {
    this.disposables.forEach(d => d.dispose());
    this.disposables = [];
    this.eventEmitters.clear();
    this.reset();
  }
}

/**
 * Creates a new analysis event capture instance for testing.
 * @returns A new AnalysisEventCapture instance
 */
export function createEventCapture(): AnalysisEventCapture {
  return new AnalysisEventCapture();
}

/**
 * Waits for multiple parallel events to complete (all must occur).
 * @param eventCapture - The event capture instance
 * @param eventNames - Array of event names to wait for
 * @param timeout - Maximum time to wait for all events (default: 30000)
 * @returns Promise that resolves with map of event names to data
 */
export async function waitForParallelEvents(
  eventCapture: AnalysisEventCapture,
  eventNames: string[],
  timeout: number = 30000
): Promise<Map<string, any>> {
  const startTime = Date.now();
  const results = new Map<string, any>();

  while (results.size < eventNames.length) {
    const elapsedTime = Date.now() - startTime;
    if (elapsedTime >= timeout) {
      const missing = eventNames.filter(name => !results.has(name));
      throw new Error(`Timeout waiting for events. Missing: [${missing.join(', ')}]`);
    }

    for (const eventName of eventNames) {
      if (!results.has(eventName)) {
        const event = eventCapture.getLatestEvent(eventName);
        if (event) {
          results.set(eventName, event.data);
        }
      }
    }

    if (results.size < eventNames.length) {
      await new Promise(resolve => setTimeout(resolve, 100));
    }
  }

  return results;
}
