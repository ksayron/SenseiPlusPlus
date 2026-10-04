import { describe, expect, it } from "vitest";
import { safeExternalUrl } from "./safeExternalUrl";

describe("external content URLs", () => {
  it("rejects executable, relative, malformed and credential-bearing URLs", () => {
    for (const url of ["javascript:alert(1)", "data:text/html,<script>alert(1)</script>", "http://example.test", "/private", "https://user:password@example.test", "https://user@example.test", "not a URL", undefined])
      expect(safeExternalUrl(url)).toBeUndefined();
  });
  it("allows an ordinary HTTPS reference unchanged", () => {
    expect(safeExternalUrl("https://example.test/reference#part")).toBe("https://example.test/reference#part");
  });
});
