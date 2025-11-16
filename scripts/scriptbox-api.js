// scriptbox-api.js
// Example API surface for sandboxed scripts.
(function (root) {
  if (typeof __scriptbox === 'undefined') {
    throw new Error('Missing scriptbox helper. Load scripts/sdk/scriptbox.js first.');
  }

  var make = __scriptbox.createMethod;

  var api = {
    add: make('Add'),
    subtract: make('Subtract'),
    fs: {
      readFile: make('FileSystemReadFile'),
      writeFile: make('FileSystemWriteFile'),
      listFiles: make('FileSystemListFiles'),
      exists: make('FileSystemExists'),
      delete: make('FileSystemDelete'),
      createDirectory: make('FileSystemCreateDirectory')
    },
    http: {
      get: make('HttpGet'),
      post: function (url, data) {
        var dataJson = typeof data === 'string' ? data : JSON.stringify(data);
        return __scriptbox.hostCall('HttpPost', [url, dataJson]);
      },
      request: function (options) {
        return __scriptbox.hostCall('HttpRequest', [JSON.stringify(options)]);
      }
    }
  };

  root.scriptbox = api;

  Object.freeze(root.scriptbox.fs);
  Object.freeze(root.scriptbox.http);
})(typeof globalThis !== 'undefined'
  ? globalThis
  : typeof global !== 'undefined'
    ? global
    : typeof self !== 'undefined'
      ? self
      : this);
