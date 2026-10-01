import "./site.css";

const base = new URL(document.documentElement.dataset.siteBase ?? "./", document.baseURI);
const theme = document.getElementById("theme-toggle");
theme?.addEventListener("click", () => {
  const value = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  document.documentElement.dataset.theme = value;
  try { localStorage.setItem("hex1b-theme", value); }
  catch (error) { console.warn("Could not save theme preference", error); }
});
document.getElementById("menu-toggle")?.addEventListener("click", event => {
  const open = document.body.classList.toggle("menu-open");
  if (event.currentTarget instanceof HTMLButtonElement)
    event.currentTarget.setAttribute("aria-expanded", String(open));
});

interface SearchEntry { route: string; title: string; text: string }
const dialog = document.getElementById("search-dialog");
const input = document.getElementById("search-input");
const results = document.getElementById("search-results");
const status = document.getElementById("search-status");
if (!(dialog instanceof HTMLDialogElement) || !(input instanceof HTMLInputElement) || !results || !status)
  throw new Error("Missing static search elements");
const searchDialog = dialog;
const searchInput = input;
const searchResults = results;
const searchStatus = status;
let entries: SearchEntry[] | undefined;
let pending: Promise<void> | undefined;
function search(): void {
  if (!entries) return;
  const terms = searchInput.value.toLocaleLowerCase().trim().split(/\s+/u).filter(Boolean);
  const matches = !terms.length ? [] : entries.map(entry => ({
    entry,
    score: terms.every(term => entry.title.toLocaleLowerCase().includes(term)) ? 2 :
      terms.every(term => `${entry.title} ${entry.text}`.toLocaleLowerCase().includes(term)) ? 1 : 0,
  })).filter(match => match.score > 0).sort((a, b) => b.score - a.score).slice(0, 25);
  searchResults.replaceChildren(...matches.map(({ entry }) => {
    const item = document.createElement("li");
    const link = document.createElement("a");
    link.href = new URL(entry.route, base).href;
    const title = document.createElement("strong");
    title.textContent = entry.title;
    const excerpt = document.createElement("span");
    excerpt.textContent = entry.text.slice(0, 140);
    link.append(title, excerpt);
    item.append(link);
    return item;
  }));
  searchStatus.textContent = !terms.length ? "Type to search." : `${matches.length} results${matches.length === 25 ? " (first 25)" : ""}.`;
}
async function openSearch(): Promise<void> {
  if (!searchDialog.open) searchDialog.showModal();
  searchInput.focus();
  if (!entries) {
    pending ??= (async () => {
      searchStatus.textContent = "Loading search index...";
      const response = await fetch(new URL("search.json", base));
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const value: unknown = await response.json();
      if (!Array.isArray(value) || !value.every(entry => typeof entry === "object" && entry !== null &&
          typeof entry.route === "string" && typeof entry.title === "string" && typeof entry.text === "string"))
        throw new Error("Invalid search index");
      entries = value;
    })();
    try { await pending; }
    catch (error) {
      pending = undefined;
      searchStatus.textContent = `Search unavailable: ${error instanceof Error ? error.message : String(error)}`;
      return;
    }
  }
  search();
}
document.getElementById("open-search")?.addEventListener("click", () => { void openSearch(); });
document.getElementById("close-search")?.addEventListener("click", () => searchDialog.close());
searchInput.addEventListener("input", search);
document.addEventListener("keydown", event => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
    event.preventDefault();
    void openSearch();
  }
});

if (document.querySelector("[data-sample-manifest]")) {
  void import("./main").catch(error => {
    const target = document.getElementById("sample-error");
    if (target) {
      target.hidden = false;
      target.textContent = `Sample viewer unavailable: ${error instanceof Error ? error.message : String(error)}`;
      target.dataset.level = "error";
    }
    console.error(error);
  });
}
