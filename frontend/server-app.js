import { createServer } from "node:http";
import sirv from "sirv";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const directory = dirname(fileURLToPath(import.meta.url));

export function createApp(dist = join(directory, "dist")) {
  const serve = sirv(dist, { single: true, etag: true });
  return createServer((request, response) => {
    if (request.method !== "GET" && request.method !== "HEAD") {
      response.statusCode = 404;
      response.end("Not found");
      return;
    }
    serve(request, response, () => {
      response.statusCode = 404;
      response.end("Not found");
    });
  });
}