export interface AssistantApi {
  add(a: number, b: number): Promise<number>;
  subtract(a: number, b: number): Promise<number>;
}

declare const assistantApi: AssistantApi;