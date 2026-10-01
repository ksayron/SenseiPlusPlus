import { ApiError } from "../api";
import type { Mutation, Session } from "./contracts";
import { recordRecovery } from "./diagnostics";
export type PendingCommand = {
  action: string;
  body: Record<string, unknown>;
  version: string;
  method: string;
};
export class SessionCommands {
  private tail: Promise<unknown> = Promise.resolve();
  pending: PendingCommand | null = null;
  constructor(
    public current: Session,
    private transport: (command: PendingCommand) => Promise<Mutation>,
  ) {}
  execute(
    action: string,
    body: Record<string, unknown>,
    method = "POST",
  ): Promise<Mutation> {
    const task = this.tail.then(async () => {
      if (this.pending)
        throw new Error("Retry or resolve the pending command first.");
      const command = {
        action,
        body: { ...body, operationId: crypto.randomUUID() },
        version: this.current.versionToken,
        method,
      };
      return this.perform(command);
    });
    this.tail = task.catch(() => undefined);
    return task;
  }
  retry(): Promise<Mutation> {
    const task = this.tail.then(() => {
      if (!this.pending) throw new Error("No pending command.");
      return this.perform(this.pending);
    });
    this.tail = task.catch(() => undefined);
    return task;
  }
  private async perform(command: PendingCommand) {
    this.pending = command;
    const started = performance.now();
    try {
      const result = await this.transport(command);
      this.current = result.session;
      this.pending = null;
      recordRecovery(
        result.replayed ? "replayed" : "acknowledged",
        performance.now() - started,
      );
      return result;
    } catch (error) {
      recordRecovery(
        error instanceof ApiError && error.status === 412
          ? "conflict"
          : "failed",
        performance.now() - started,
      );
      if (
        error instanceof ApiError &&
        error.status >= 400 &&
        error.status < 500
      )
        this.pending = null;
      throw error;
    }
  }
}
