// assistantApi.js
// Runtime-injected API bridge for ScriptSandboxPoC
// 
// This file is prepended to user scripts before evaluation in QuickJS.
// It uses the low-level __host.bridge (provided by WASM C layer)
// to expose friendly JavaScript APIs.
//
// Benefits:
// - No WASM rebuild needed when changing API surface
// - TypeScript definitions stay in sync with implementation
// - Easy to extend with new methods or entire new API namespaces

// Verify the bridge is installed
if (typeof __host === 'undefined') {
  throw new Error('FATAL: __host object is not defined! WASM bridge not installed correctly.');
}

if (typeof __host.bridge !== 'function') {
  throw new Error('FATAL: __host.bridge is not a function! WASM bridge not installed correctly.');
}

// Helper to make synchronous RPC calls to the host
function hostCall(method, args) {
  const request = JSON.stringify({ method, args });
  const response = __host.bridge(request);
  
  // Handle null response (host returned empty)
  if (response === null) {
    throw new Error('Host returned null response');
  }
  
  const result = JSON.parse(response);
  
  if (result.error) {
    throw new Error(result.error);
  }
  
  return result.result;
}

// Define the assistantApi namespace
globalThis.assistantApi = {
  /**
   * Add two numbers
   * @param {number} a - First number
   * @param {number} b - Second number
   * @returns {number} Sum of a and b
   */
  add: function(a, b) {
    return hostCall('Add', [a, b]);
  },

  /**
   * Subtract two numbers
   * @param {number} a - First number
   * @param {number} b - Second number
   * @returns {number} Difference of a and b
   */
  subtract: function(a, b) {
    return hostCall('Subtract', [a, b]);
  }
};

// Freeze the API to prevent user scripts from tampering
Object.freeze(globalThis.assistantApi);
