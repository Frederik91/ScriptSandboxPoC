const make = __scriptbox.createMethod;

const scriptboxApi = {
  math: {
    add: make('Add'),
    subtract: make('Subtract'),
  },
  files: {
    readAll: make('FileSystemReadFile'),
    writeAll: make('FileSystemWriteFile'),
  },
};

(globalThis as any).scriptbox = scriptboxApi;
