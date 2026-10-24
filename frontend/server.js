// Minimal static server for Azure App Service (Linux/Windows Node).
import { createApp } from "./server-app.js";

const port = process.env.PORT || 8080;
createApp().listen(port, () => console.log(`ERP UI listening on ${port}`));
