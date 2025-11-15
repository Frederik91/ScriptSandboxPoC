export interface AssistantApi {
  log(message: string): Promise<void>;
  add(a: number, b: number): Promise<number>;
}

declare const assistantApi: AssistantApi;