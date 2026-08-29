import * as sql from "mssql";
import { DefaultAzureCredential } from "@azure/identity";

let pool: sql.ConnectionPool | undefined;

function baseConfig(): sql.config {
    const server = process.env.SQL_SERVER ?? "erpmodulesqlserver.database.windows.net";
    const database = process.env.SQL_DATABASE ?? "erpmodulessqldb";

    return {
        server,
        database,
        options: {
            encrypt: true,
            trustServerCertificate: false
        },
        pool: {
            max: 10,
            min: 0,
            idleTimeoutMillis: 30000
        }
    };
}

async function buildConfig(): Promise<sql.config> {
    const config = baseConfig();
    const user = process.env.SQL_USER;
    const password = process.env.SQL_PASSWORD;

    if (user && password) {
        return { ...config, user, password };
    }

    const credential = new DefaultAzureCredential();
    const token = await credential.getToken("https://database.windows.net/.default");

    return {
        ...config,
        authentication: {
            type: "azure-active-directory-access-token",
            options: {
                token: token.token
            }
        }
    };
}

export async function getPool(): Promise<sql.ConnectionPool> {
    if (pool && pool.connected) {
        return pool;
    }

    const config = await buildConfig();
    pool = await new sql.ConnectionPool(config).connect();
    return pool;
}
