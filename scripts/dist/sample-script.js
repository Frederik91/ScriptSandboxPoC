"use strict";
// scripts/sample-script.ts
//import { assistantApi } from "./sdk/index";
Object.defineProperty(exports, "__esModule", { value: true });
exports.run = run;
async function run() {
    await assistantApi.log("Hello from TypeScript");
    const sum = await assistantApi.add(40, 2);
    await assistantApi.log(`The answer is ${sum}`);
}
// Option 1: call run() at top level:
run();
