/**
 * Debounce utility for delaying function execution
 */

/**
 * Creates a debounced function that delays invoking func until after wait milliseconds
 * have elapsed since the last time the debounced function was invoked.
 * 
 * @param func - The function to debounce
 * @param delay - The number of milliseconds to delay
 * @returns A new debounced function
 */
export function debounce<T extends (...args: any[]) => any>(
    func: T,
    delay: number
): (...args: Parameters<T>) => void {
    let timeoutId: NodeJS.Timeout | null = null;

    return function (this: any, ...args: Parameters<T>): void {
        // Clear the previous timeout if it exists
        if (timeoutId !== null) {
            clearTimeout(timeoutId);
        }

        // Set a new timeout
        timeoutId = setTimeout(() => {
            func.apply(this, args);
            timeoutId = null;
        }, delay);
    };
}

/**
 * Creates a debounced function that also returns a promise
 * Useful for async functions where you need to await the result
 * 
 * @param func - The async function to debounce
 * @param delay - The number of milliseconds to delay
 * @returns A new debounced async function
 */
export function debounceAsync<T extends (...args: any[]) => Promise<any>>(
    func: T,
    delay: number
): (...args: Parameters<T>) => Promise<ReturnType<T>> {
    let timeoutId: NodeJS.Timeout | null = null;
    let pendingPromise: Promise<ReturnType<T>> | null = null;

    return function (this: any, ...args: Parameters<T>): Promise<ReturnType<T>> {
        // If there's already a pending operation, return it
        if (pendingPromise) {
            return pendingPromise;
        }

        // Clear the previous timeout if it exists
        if (timeoutId !== null) {
            clearTimeout(timeoutId);
        }

        // Create a new promise that will resolve after the delay
        pendingPromise = new Promise<ReturnType<T>>((resolve, reject) => {
            timeoutId = setTimeout(async () => {
                try {
                    const result = await func.apply(this, args);
                    resolve(result);
                } catch (error) {
                    reject(error);
                } finally {
                    timeoutId = null;
                    pendingPromise = null;
                }
            }, delay);
        });

        return pendingPromise;
    };
}

/**
 * Creates a debounced function with a leading edge option
 * 
 * @param func - The function to debounce
 * @param delay - The number of milliseconds to delay
 * @param leading - If true, invoke on the leading edge instead of the trailing edge
 * @returns A new debounced function
 */
export function debounceLeading<T extends (...args: any[]) => any>(
    func: T,
    delay: number,
    leading: boolean = false
): (...args: Parameters<T>) => void {
    let timeoutId: NodeJS.Timeout | null = null;
    let lastInvokeTime = 0;

    return function (this: any, ...args: Parameters<T>): void {
        const now = Date.now();
        const shouldInvokeNow = leading && (now - lastInvokeTime > delay);

        // Clear the previous timeout if it exists
        if (timeoutId !== null) {
            clearTimeout(timeoutId);
        }

        if (shouldInvokeNow) {
            // Invoke immediately on leading edge
            func.apply(this, args);
            lastInvokeTime = now;
        } else {
            // Set a new timeout for trailing edge
            timeoutId = setTimeout(() => {
                func.apply(this, args);
                lastInvokeTime = Date.now();
                timeoutId = null;
            }, delay);
        }
    };
}
