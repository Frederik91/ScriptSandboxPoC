// scriptbox.js
// Minimal bridge helper that hides the __host.bridge JSON plumbing.
(function (root) {
  if (typeof root.__scriptbox !== 'undefined') {
    return;
  }

  if (typeof __host === 'undefined' || typeof __host.bridge !== 'function') {
    throw new Error('Missing __host.bridge. Ensure the WASM bridge is installed.');
  }

  function toArgsArray(value) {
    if (!value || typeof value.length === 'undefined') {
      return [];
    }

    var length = value.length >>> 0;
    var result = new Array(length);
    for (var i = 0; i < length; i++) {
      result[i] = value[i];
    }
    return result;
  }

  function callHost(method, args) {
    var payload = JSON.stringify({ method: method, args: toArgsArray(args) });
    var response = __host.bridge(payload);

    if (response === null || typeof response === 'undefined') {
      throw new Error('Host returned null response for method ' + method);
    }

    var parsed = JSON.parse(response);
    if (parsed && typeof parsed === 'object' && parsed.error) {
      throw new Error(parsed.error);
    }

    return parsed ? parsed.result : null;
  }

  function createMethod(methodName) {
    if (typeof methodName !== 'string' || methodName.length === 0) {
      throw new Error('Method name must be a non-empty string');
    }

    return function () {
      return callHost(methodName, arguments);
    };
  }

  root.__scriptbox = {
    hostCall: callHost,
    createMethod: createMethod
  };
})(typeof globalThis !== 'undefined'
  ? globalThis
  : typeof global !== 'undefined'
    ? global
    : typeof self !== 'undefined'
      ? self
      : this);
