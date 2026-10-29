import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import ExcelJS from "exceljs";
import ts from "typescript";

const source = readFileSync(new URL("../src/lib/sheetExcel.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, esModuleInterop: true }
}).outputText;
const exports = {};
new Function("exports", "require", compiled)(exports, () => ExcelJS);
const { parsePurchaseOrderXlsx, purchaseOrderXlsxFile } = exports;
const items = [
  { id: "steel", code: "ST-01", name: "Steel Bar", baseUomId: "unit" },
  { id: "bolt", code: "BT-01", name: "Bolt", baseUomId: "unit" }
];

async function workbook(rows) {
  const book = new ExcelJS.Workbook();
  book.addWorksheet("Items").addRows(rows);
  return new File([await book.xlsx.writeBuffer()], "items.xlsx");
}

test("PO Excel export imports its item codes and quantities without prices", async () => {
  const file = await purchaseOrderXlsxFile([{ itemId: "steel", qty: 12.5 }, { itemId: "bolt", qty: 2 }], items);
  const imported = await parsePurchaseOrderXlsx(file, items);
  assert.deepEqual(Array.from(imported, line => ({ ...line })), [{ itemId: "steel", qty: 12.5 }, { itemId: "bolt", qty: 2 }]);
  const book = new ExcelJS.Workbook();
  await book.xlsx.load(await file.arrayBuffer());
  const text = book.worksheets[0].getSheetValues().flat().join(" ");
  assert.ok(!/price|supplier|tax/i.test(text));
  assert.match(file.name, /\.xlsx$/);
});

test("downloaded catalog template imports only rows whose quantities are filled", async () => {
  const template = await purchaseOrderXlsxFile([], items);
  const book = new ExcelJS.Workbook();
  await book.xlsx.load(await template.arrayBuffer());
  const sheet = book.worksheets[0];
  sheet.eachRow(row => { if (row.getCell(2).value === "ST-01") row.getCell(5).value = 4; });
  const result = await parsePurchaseOrderXlsx(new File([await book.xlsx.writeBuffer()], "filled.xlsx"), items);
  assert.equal(result.length, 1);
  assert.equal(result[0].qty, 4);
});

test("simple item/quantity columns support exact names and case-insensitive codes", async () => {
  const file = await workbook([["Item", "Quantity"], ["Steel Bar", 3], ["bt-01", 2]]);
  const result = await parsePurchaseOrderXlsx(file, items);
  assert.equal(result[0].itemId, "steel");
  assert.equal(result[1].itemId, "bolt");
});

test("repeated Excel item codes combine quantities", async () => {
  const result = await parsePurchaseOrderXlsx(await workbook([["Item Code", "Qty"], ["ST-01", 1.25], ["ST-01", 2.5]]), items);
  assert.equal(result.length, 1);
  assert.equal(result[0].qty, 3.75);
});

for (const quantity of [-1, 0, "invalid", 0.00001]) {
  test(`invalid Excel quantity ${quantity} rejects the entire import`, async () => {
    await assert.rejects(() => workbook([["Item Code", "Qty"], ["ST-01", 2], ["BT-01", quantity]]).then(file => parsePurchaseOrderXlsx(file, items)), /Row 3/);
  });
}

test("unknown item codes report the Excel row instead of being silently dropped", async () => {
  await assert.rejects(() => workbook([["Item Code", "Qty"], ["UNKNOWN", 3]]).then(file => parsePurchaseOrderXlsx(file, items)), /Row 2.*UNKNOWN/);
});

test("ambiguous names require a unique item code", async () => {
  const catalog = [...items, { id: "other", code: "OTHER", name: "Steel Bar" }];
  await assert.rejects(() => workbook([["Item Name", "Qty"], ["Steel Bar", 3]]).then(file => parsePurchaseOrderXlsx(file, catalog)), /ambiguous/);
});

test("missing columns and empty templates show actionable errors", async () => {
  await assert.rejects(() => workbook([["Code", "Price"], ["ST-01", 3]]).then(file => parsePurchaseOrderXlsx(file, items)), /must contain/);
  await assert.rejects(() => purchaseOrderXlsxFile([], items).then(file => parsePurchaseOrderXlsx(file, items)), /positive quantity/);
});

test("invalid workbooks show an actionable error instead of ZIP implementation details", async () => {
  await assert.rejects(() => parsePurchaseOrderXlsx(new File(["not a workbook"], "invalid.xlsx"), items), /Upload a valid \.xlsx Excel file/);
});