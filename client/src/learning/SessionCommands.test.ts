import { describe, expect, it } from "vitest";
import { ApiError } from "../api";
import { SessionCommands } from "./SessionCommands";
import type { Mutation, Session } from "./contracts";
const state = (versionToken = "v1") =>
  ({ id: "session", versionToken }) as Session;
const result = (versionToken: string): Mutation => ({
  session: state(versionToken),
  outcomeId: "result",
  receiptVersionToken: "historical",
  replayed: false,
});
describe("session command ordering and receipts", () => {
  it("serializes commands and supplies the last acknowledged version", async () => {
    const seen: string[] = [];
    const queue = new SessionCommands(state(), async (c) => {
      seen.push(c.version);
      return result(`v${seen.length + 1}`);
    });
    await Promise.all([
      queue.execute("draft", {}),
      queue.execute("assistance", {}),
      queue.execute("attempts", {}),
    ]);
    expect(seen).toEqual(["v1", "v2", "v3"]);
  });
  it("retries the exact pending operation after a lost response and does not use historical ETag", async () => {
    const calls: unknown[] = [];
    const queue = new SessionCommands(state(), async (c) => {
      calls.push(c);
      if (calls.length === 1) throw new TypeError("Network failed");
      return { ...result("current"), replayed: true };
    });
    await expect(queue.execute("attempts", { answer: "a" })).rejects.toThrow(
      "Network failed",
    );
    await expect(queue.execute("advance", {})).rejects.toThrow(
      "pending command",
    );
    await queue.retry();
    expect(calls[1]).toEqual(calls[0]);
    expect(queue.current.versionToken).toBe("current");
    expect(queue.pending).toBeNull();
  });
  it("does not automatically resubmit a stale mutation", async () => {
    let calls = 0;
    const queue = new SessionCommands(state(), async () => {
      calls++;
      throw new ApiError("Conflict", 412);
    });
    await expect(queue.execute("draft", {})).rejects.toThrow("Conflict");
    expect(calls).toBe(1);
    expect(queue.pending).toBeNull();
    expect(queue.current.versionToken).toBe("v1");
  });
});
