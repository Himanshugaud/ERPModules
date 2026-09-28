const fs = require("fs");
const path = require("path");

const schemaFiles = ["erp-schema.sql", "inventory-schema.sql", "procurement-schema.sql"];
const sql = schemaFiles
  .map((file) => fs.readFileSync(path.join(__dirname, file), "utf8"))
  .join("\n");

const tables = new Map();
const createTablePattern = /CREATE TABLE\s+(\w+)\.(\w+)\s*\(([\s\S]*?)\n\s*\);/gi;
for (const match of sql.matchAll(createTablePattern)) {
  const [, schema, name, body] = match;
  const key = `${schema}.${name}`;
  if (tables.has(key)) continue;

  const columns = [];
  for (const line of body.split(/\r?\n/)) {
    const column = line.match(/^\s{8}(\w+)\s+([A-Z][A-Z0-9]*(?:\([^)]*\))?)(.*)$/i);
    if (!column || column[1] === "CONSTRAINT") continue;
    columns.push({
      name: column[1],
      type: column[2].replace(/\s+/g, " "),
      primaryKey: /PRIMARY KEY/i.test(column[3]),
      foreignKey: false,
    });
  }

  const tablePrimaryKey = body.match(/PRIMARY KEY\s*\(([^)]+)\)/i);
  if (tablePrimaryKey) {
    const primaryKeys = tablePrimaryKey[1].split(",").map((value) => value.trim());
    columns.forEach((column) => {
      if (primaryKeys.includes(column.name)) column.primaryKey = true;
    });
  }

  tables.set(key, { schema, name, columns, foreignKeys: [] });
}

const foreignKeyPattern = /ALTER TABLE\s+(\w+)\.(\w+)\s+ADD CONSTRAINT\s+(\w+)\s+FOREIGN KEY\s*\((\w+)\)\s+REFERENCES\s+(\w+)\.(\w+)\s*\((\w+)\)/gi;
for (const match of sql.matchAll(foreignKeyPattern)) {
  const [, sourceSchema, sourceTable, constraint, sourceColumn, targetSchema, targetTable, targetColumn] = match;
  const sourceKey = `${sourceSchema}.${sourceTable}`;
  const targetKey = `${targetSchema}.${targetTable}`;
  const source = tables.get(sourceKey);
  if (!source || !tables.has(targetKey)) continue;
  source.foreignKeys.push({ constraint, sourceColumn, targetKey, targetColumn });
  const column = source.columns.find((candidate) => candidate.name === sourceColumn);
  if (column) column.foreignKey = true;
}

const palette = {
  core: { stroke: "#0078D4", fill: "#CFE4FA" },
  project: { stroke: "#107C10", fill: "#DFF6DD" },
  shared: { stroke: "#5C2D91", fill: "#E8DAEF" },
  inventory: { stroke: "#F7630C", fill: "#FFF4CE" },
};
const schemaOrder = ["core", "project", "shared", "inventory"];
const cardWidth = 340;
const cardGap = 40;
const rowGap = 50;
const schemaGap = 120;
const columnsPerSchema = 6;
const elements = [];
const positions = new Map();

function baseElement(id, type, x, y, width, height) {
  return {
    id,
    type,
    x,
    y,
    width,
    height,
    angle: 0,
    strokeWidth: 1,
    strokeStyle: "solid",
    roughness: 0,
    opacity: 100,
    groupIds: [],
    frameId: null,
    index: null,
    roundness: type === "rectangle" ? { type: 3 } : null,
    seed: Math.abs([...id].reduce((sum, char) => sum * 31 + char.charCodeAt(0), 7)) % 2147483647,
    version: 1,
    versionNonce: 1,
    isDeleted: false,
    boundElements: [],
    updated: 1,
    link: null,
    locked: false,
  };
}

function addText(id, x, y, width, height, text, fontSize = 16, fontFamily = 3) {
  elements.push({
    ...baseElement(id, "text", x, y, width, height),
    strokeColor: "#000000",
    backgroundColor: "transparent",
    fillStyle: "solid",
    text,
    fontSize,
    fontFamily,
    textAlign: "left",
    verticalAlign: "top",
    baseline: fontSize,
    lineHeight: 1.25,
    originalText: text,
    autoResize: false,
  });
}

addText("diagram-title", 40, 20, 1500, 60, "ERP Modules - SQL Table Relationship Diagram", 30, 2);
addText("diagram-note", 40, 80, 1800, 40, "PK = primary key   FK = declared foreign key   Generated from db/schema/*.sql", 16, 2);

let schemaY = 150;
for (const schema of schemaOrder) {
  const schemaTables = [...tables.values()]
    .filter((table) => table.schema === schema)
    .sort((left, right) => left.name.localeCompare(right.name));
  if (!schemaTables.length) continue;

  const rows = Math.ceil(schemaTables.length / columnsPerSchema);
  const cards = schemaTables.map((table) => {
    const displayedColumns = table.columns.slice(0, 10);
    const hiddenCount = Math.max(0, table.columns.length - displayedColumns.length);
    const lines = [
      table.name,
      "────────────────────────",
      ...displayedColumns.map((column) => {
        const flags = `${column.primaryKey ? "PK " : ""}${column.foreignKey ? "FK " : ""}`;
        return `${flags.padEnd(6)}${column.name}: ${column.type}`;
      }),
      ...(hiddenCount ? [`      ... ${hiddenCount} more columns`] : []),
    ];
    return { table, text: lines.join("\n"), height: Math.max(150, lines.length * 22 + 24) };
  });

  const rowHeights = Array.from({ length: rows }, (_, row) =>
    Math.max(...cards.slice(row * columnsPerSchema, (row + 1) * columnsPerSchema).map((card) => card.height))
  );
  const rowOffsets = [];
  rowHeights.reduce((offset, height, row) => {
    rowOffsets[row] = offset;
    return offset + height + rowGap;
  }, 0);
  const contentHeight = rowHeights.reduce((sum, height) => sum + height, 0) + (rows - 1) * rowGap;
  const containerWidth = columnsPerSchema * cardWidth + (columnsPerSchema - 1) * cardGap + 80;
  const containerHeight = contentHeight + 110;
  const colors = palette[schema];

  elements.push({
    ...baseElement(`schema-${schema}`, "rectangle", 20, schemaY, containerWidth, containerHeight),
    strokeColor: colors.stroke,
    backgroundColor: "transparent",
    fillStyle: "solid",
    strokeWidth: 3,
  });
  addText(`schema-${schema}-label`, 50, schemaY + 20, 500, 50, `${schema.toUpperCase()} SCHEMA (${schemaTables.length} tables)`, 22, 2);

  cards.forEach((card, index) => {
    const row = Math.floor(index / columnsPerSchema);
    const column = index % columnsPerSchema;
    const x = 60 + column * (cardWidth + cardGap);
    const y = schemaY + 80 + rowOffsets[row];
    const tableId = `table-${card.table.schema}-${card.table.name}`;
    positions.set(`${card.table.schema}.${card.table.name}`, { id: tableId, x, y, width: cardWidth, height: card.height });
    elements.push({
      ...baseElement(tableId, "rectangle", x, y, cardWidth, card.height),
      strokeColor: colors.stroke,
      backgroundColor: colors.fill,
      fillStyle: "solid",
      strokeWidth: 2,
    });
    addText(`${tableId}-text`, x + 14, y + 12, cardWidth - 28, card.height - 24, card.text, 13, 3);
  });

  schemaY += containerHeight + schemaGap;
}

const arrows = [];
for (const [sourceKey, source] of tables) {
  for (const foreignKey of source.foreignKeys) {
    const from = positions.get(sourceKey);
    const to = positions.get(foreignKey.targetKey);
    if (!from || !to) continue;
    const startX = from.x + from.width / 2;
    const startY = from.y + from.height / 2;
    const endX = to.x + to.width / 2;
    const endY = to.y + to.height / 2;
    const id = `fk-${foreignKey.constraint}`;
    arrows.push({
      ...baseElement(id, "arrow", startX, startY, endX - startX, endY - startY),
      strokeColor: "#6b7280",
      backgroundColor: "transparent",
      fillStyle: "solid",
      strokeWidth: 1,
      opacity: 35,
      points: [[0, 0], [endX - startX, endY - startY]],
      lastCommittedPoint: null,
      startBinding: { elementId: from.id, focus: 0, gap: 4 },
      endBinding: { elementId: to.id, focus: 0, gap: 4 },
      startArrowhead: null,
      endArrowhead: "arrow",
      elbowed: false,
    });
  }
}

const diagram = {
  type: "excalidraw",
  version: 2,
  source: "GitHub Copilot",
  elements: [...arrows, ...elements],
  appState: {
    viewBackgroundColor: "#ffffff",
    gridSize: 20,
  },
  files: {},
};

const output = path.join(__dirname, "erp-table-relationships.excalidraw");
fs.writeFileSync(output, `${JSON.stringify(diagram, null, 2)}\n`);

const invalidForeignKeys = arrows.filter((arrow) =>
  !positions.has([...tables.keys()].find((key) => positions.get(key)?.id === arrow.endBinding.elementId))
);
if (tables.size !== 64 || invalidForeignKeys.length) {
  throw new Error(`Validation failed: ${tables.size} tables, ${invalidForeignKeys.length} invalid relationship(s)`);
}
console.log(`Created ${path.basename(output)} with ${tables.size} tables and ${arrows.length} relationships.`);