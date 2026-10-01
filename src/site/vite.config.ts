import { defineConfig } from "vite";

export default defineConfig({
  base: "./",
  appType: "mpa",
  build: {
    manifest: true,
    rollupOptions: { input: "src/client.ts" },
  },
});
