// External content is plain text. Only HTTPS links without embedded credentials may navigate.
export function safeExternalUrl(url?: string): string | undefined {
  try {
    if (!url) return undefined;
    const parsed = new URL(url);
    return parsed.protocol === "https:" && !parsed.username && !parsed.password ? url : undefined;
  } catch {
    return undefined;
  }
}
