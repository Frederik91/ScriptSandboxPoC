interface ScriptBoxBridge {
  /**
   * Invoke a host method directly.
   */
  hostCall(method: string, args: unknown[]): unknown;

  /**
   * Create a function that forwards calls to the specified host method.
   */
  createMethod(method: string): (...args: unknown[]) => unknown;
}

declare const __scriptbox: ScriptBoxBridge;

// Example scriptbox typings. Consumers are expected to define their
// own interfaces, but we keep these around for samples/tests.
interface FileSystemEntry {
  name: string;
  isDirectory: boolean;
  size: number;
}

interface FileSystemApi {
  readFile(path: string): string;
  writeFile(path: string, content: string): void;
  listFiles(path: string): FileSystemEntry[];
  exists(path: string): boolean;
  delete(path: string): void;
  createDirectory(path: string): void;
}

interface HttpRequestOptions {
  url: string;
  method: string;
  headers?: Record<string, string>;
  body?: string;
}

interface HttpResponse {
  status: number;
  headers: Record<string, string>;
  body: string;
}

interface HttpApi {
  get(url: string): string;
  post(url: string, data: any): string;
  request(options: HttpRequestOptions): HttpResponse;
}

interface ScriptBoxApi {
  add(a: number, b: number): number;
  subtract(a: number, b: number): number;
  fs: FileSystemApi;
  http: HttpApi;
}

declare const scriptbox: ScriptBoxApi;
