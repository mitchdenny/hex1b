export function defaultWorkerUrl(kind: "terminal" | "link-detection"): URL {
  const url = new URL(import.meta.url);
  url.hash = `hex1b-${kind}-worker`;
  return url;
}
