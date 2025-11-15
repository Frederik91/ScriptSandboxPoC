// scripts/sample-script.ts
import { assistantApi } from "./sdk/index";

export async function run() {
  await assistantApi.log("Hello from TypeScript");
  const sum = await assistantApi.add(40, 2);
  await assistantApi.log(`The answer is ${sum}`);
}

// Option 1: call run() at top level:
run();
