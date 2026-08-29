// Minimal static server for Azure App Service (Linux/Windows Node).
import express from "express";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const app = express();
const dist = join(__dirname, "dist");

app.use(express.static(dist));

// SPA fallback: send index.html for any non-file route.
app.get("*", (_req, res) => {
  res.sendFile(join(dist, "index.html"));
});

const port = process.env.PORT || 8080;
app.listen(port, () => console.log(`ERP UI listening on ${port}`));
