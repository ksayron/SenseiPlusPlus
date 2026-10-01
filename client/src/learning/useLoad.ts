import { useEffect, useState } from "react";
export function useLoad<T>(loader: () => Promise<T>, key: string) {
  const [result, setResult] = useState<{ key: string; data: T }>();
  const [failure, setFailure] = useState<{ key: string; message: string }>();
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let alive = true;
    loader()
      .then((v) => {
        if (alive) {
          setResult({ key, data: v });
          setFailure(undefined);
        }
      })
      .catch((e) => {
        if (alive) setFailure({ key, message: e.message });
      });
    return () => {
      alive = false;
    };
    // The explicit key defines request identity; loader closures do not restart requests.
  }, [key, revision]);
  return {
    data: result?.key === key ? result.data : undefined,
    error: failure?.key === key ? failure.message : "",
    reload: () => setRevision((v) => v + 1),
  };
}
