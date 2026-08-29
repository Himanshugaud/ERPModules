# ERP Modules Backend

TypeScript backend built with Azure Functions v4 and the Node.js programming model.

## Prerequisites

- Node.js 18 or newer
- Azure Functions Core Tools 4
- Azure CLI (for deployment)
- Azurite when adding functions that require Azure Storage locally

## Local development

```powershell
npm install
npm start
```

The Functions host starts at `http://localhost:7071` by default.

## Endpoints

### Health check

```http
GET /api/health
```

Example response:

```json
{
  "status": "ok",
  "timestamp": "2026-08-28T00:00:00.000Z"
}
```

## Scripts

- `npm run build` compiles TypeScript into `dist`.
- `npm run watch` recompiles when source files change.
- `npm start` builds and starts the local Functions host.
- `npm run clean` removes compiled output.

## Configuration

Local settings belong in `local.settings.json`, which is excluded from source control. Add matching application settings to the Azure Function App before deployment.

## Deploy to Azure

After signing in with `az login` and creating an Azure Function App, publish with:

```powershell
func azure functionapp publish <FUNCTION_APP_NAME>
```