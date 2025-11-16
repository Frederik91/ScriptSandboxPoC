var make = __scriptbox.createMethod;
var scriptboxApi = {
    math: {
        add: make('Add'),
        subtract: make('Subtract'),
    },
    files: {
        readAll: make('FileSystemReadFile'),
        writeAll: make('FileSystemWriteFile'),
    },
};
globalThis.scriptbox = scriptboxApi;
