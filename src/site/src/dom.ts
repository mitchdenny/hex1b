export function element<T extends HTMLElement>(id: string, kind: new () => T): T {
  const value = document.getElementById(id);
  if (!(value instanceof kind)) throw new Error(`Missing page element: ${id}`);
  return value;
}
