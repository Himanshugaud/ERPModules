import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";
import ts from "typescript";

function declaration(file, name) {
  const source = readFileSync(new URL(file, import.meta.url), "utf8");
  const parsed = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const found = parsed.statements.find(statement => ts.isFunctionDeclaration(statement) && statement.name?.text === name);
  assert.ok(found, `Missing ${name}`);
  return ts.transpileModule(found.getText(parsed), {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.React }
  }).outputText;
}

const shortageCode = declaration("../src/pages/InventoryRequests.tsx", "buildShortageRows");
const buildShortageRows = runInNewContext(`${shortageCode}\nbuildShortageRows;`);

test("complete inventory lookups follow every server-capped page", async () => {
  const paths = [];
  const requestAll = runInNewContext(`${declaration("../src/api/client.ts", "requestAll")}\nrequestAll;`, {
    qstr: params => new URLSearchParams(params).toString(),
    request: async path => {
      paths.push(path);
      const page = Number(new URL(path, "http://test.invalid").searchParams.get("page"));
      return { data: [page], pagination: { totalPages: 3 } };
    }
  });
  assert.deepEqual(Array.from(await requestAll("/purchase-orders", { status: "ORDERED" })), [1, 2, 3]);
  assert.equal(paths.length, 3);
  assert.ok(paths.every(path => path.includes("pageSize=100") && path.includes("status=ORDERED")));
});

test("failed stock pagination does not return misleading partial availability", async () => {
  const requestAll = runInNewContext(`${declaration("../src/api/client.ts", "requestAll")}\nrequestAll;`, {
    qstr: params => new URLSearchParams(params).toString(),
    request: async path => {
      if (path.includes("page=2")) throw new Error("Unavailable");
      return { data: [{ qtyAvailable: 10 }], pagination: { totalPages: 2 } };
    }
  });
  await assert.rejects(() => requestAll("/stock"), /Unavailable/);
});

const item = { id: "item", name: "Steel", code: "STEEL", baseUomId: "uom" };
const requirement = { id: "request", reqNumber: "REQ-1", status: "APPROVED", lines: [{ id: "line", itemId: item.id, qty: 10 }] };

test("PO draft selects only missing quantities", () => {
  const rows = buildShortageRows(requirement, [item], { item: 4 });
  assert.equal(rows[0].qty, "6");
  assert.equal(rows[0].include, true);
  assert.equal(rows[0].unitPrice, undefined);
});

test("in-stock materials are not selected for a draft PO", () => {
  const rows = buildShortageRows(requirement, [item], { item: 12 });
  assert.equal(rows[0].qty, "0");
  assert.equal(rows[0].include, false);
});

test("stock is allocated once across repeated materials in a requirement", () => {
  const rows = buildShortageRows({ ...requirement, lines: [...requirement.lines, { id: "second", itemId: "item", qty: 10 }] }, [item], { item: 15 });
  assert.equal(rows.reduce((sum, row) => sum + Number(row.qty), 0), 5);
});

test("fabricated and already drafted lines are excluded from procurement", () => {
  assert.equal(buildShortageRows(requirement, [{ ...item, isManufactured: true }], {}).length, 0);
  assert.equal(buildShortageRows({ ...requirement, lines: [{ ...requirement.lines[0], purchaseOrderLineId: "existing" }] }, [item], {}).length, 0);
});

function render(file, name, props, overrides, api, excel = {}) {
  let stateIndex = 0;
  const component = runInNewContext(`${declaration(file, name)}\n${name};`, {
    exports: {},
    React: { createElement: (type, props, ...children) => ({ type, props: { ...props, children } }) },
    useState: initial => {
      const index = stateIndex++;
      const value = overrides.has(index) ? overrides.get(index) : typeof initial === "function" ? initial() : initial;
      return [value, () => {}];
    },
    useEffect: () => {},
    useRef: () => ({ current: null }),
    buildShortageRows,
    ...excel,
    api,
    ApiError: Error
  });
  const nodes = [];
  function visit(node) {
    if (Array.isArray(node)) return node.forEach(visit);
    if (!node || typeof node !== "object") return;
    nodes.push(node);
    visit(node.props.children);
  }
  visit(component(props));
  return nodes;
}

test("new standalone PO form saves quantities without project, supplier or prices", async () => {
  let payload;
  const nodes = render("../src/pages/PurchaseOrders.tsx", "DraftPoModal", {
    po: null, warehouses: [], items: [item], onClose() {}, onSaved() {}
  }, new Map([[3, [{ itemId: item.id, qty: "3", uomId: "uom" }]]]), {
    createPurchaseOrder: async body => { payload = body; }
  });
  await nodes.find(node => node.type === "form").props.onSubmit({ preventDefault() {} });
  assert.equal(payload.lines[0].qty, 3);
  assert.equal(payload.projectId, undefined);
  assert.equal(payload.supplierId, undefined);
  assert.equal(payload.lines[0].unitPrice, undefined);
});

test("editing a draft sends line IDs and its concurrency token", async () => {
  let payload;
  const po = { id: "po", poNumber: "PO-1", rowVersion: "version", lines: [{ id: "po-line", itemId: item.id, qty: 3, materialRequirementLineId: "req-line" }] };
  const nodes = render("../src/pages/PurchaseOrders.tsx", "DraftPoModal", {
    po, warehouses: [], items: [item], onClose() {}, onSaved() {}
  }, new Map(), { updatePurchaseOrder: async (_id, body) => { payload = body; } });
  await nodes.find(node => node.type === "form").props.onSubmit({ preventDefault() {} });
  assert.equal(payload.lines[0].id, "po-line");
  assert.equal(payload.rowVersion, "version");
});

test("new PO supports Excel upload, template download and quantity export", async () => {
  const downloads = [];
  let imported;
  const nodes = render("../src/pages/PurchaseOrders.tsx", "DraftPoModal", {
    po: null, warehouses: [], items: [item], onClose() {}, onSaved() {}
  }, new Map([[3, [{ itemId: item.id, qty: "3" }]]]), {}, {
    parsePurchaseOrderXlsx: async file => { imported = file; return [{ itemId: item.id, qty: 6 }]; },
    downloadPurchaseOrderXlsx: async lines => { downloads.push(lines); }
  });
  const upload = nodes.find(node => node.type === "input" && node.props.type === "file");
  assert.ok(upload);
  const file = { name: "materials.xlsx" };
  await upload.props.onChange({ target: { files: [file] } });
  assert.equal(imported, file);
  const buttons = nodes.filter(node => node.type === "button");
  await buttons.find(node => node.props.children.includes("⭳ Download template")).props.onClick();
  await buttons.find(node => node.props.children.includes("⭳ Download Excel")).props.onClick();
  assert.equal(downloads[0].length, 0);
  assert.equal(downloads[1][0].itemId, item.id);
  assert.equal(downloads[1][0].qty, 3);
});

test("mark ordered sends supplier and per-line prices only at ordering time", async () => {
  let payload;
  const nodes = render("../src/pages/PurchaseOrders.tsx", "OrderPoModal", {
    po: { id: "po", poNumber: "PO-1", rowVersion: "version", lines: [] }, suppliers: [], items: [item], onClose() {}, onSaved() {}
  }, new Map([[0, "supplier"], [4, [{ lineId: "line", itemId: item.id, qty: 2, unitPrice: "100", taxRatePercent: "18" }]]]), {
    markPurchaseOrderOrdered: async (_id, body) => { payload = body; }
  });
  await nodes.find(node => node.type === "form").props.onSubmit({ preventDefault() {} });
  assert.equal(payload.supplierId, "supplier");
  assert.equal(payload.lines[0].unitPrice, 100);
  assert.equal(payload.lines[0].taxRatePercent, 18);
  assert.equal(payload.rowVersion, "version");
});

test("material request creates an editable draft with no supplier or price", async () => {
  let payload;
  const nodes = render("../src/pages/InventoryRequests.tsx", "CreatePoModal", {
    requirement, items: [item], warehouses: [{ id: "warehouse", warehouseType: "MAIN_STORE" }], uoms: [],
    stockByItem: {}, stockByItemWarehouse: { "item|warehouse": 4 }, onClose() {}, onCreated() {}
  }, new Map(), { convertMaterialRequirementToPo: async (_id, body) => { payload = body; return { poNumber: "PO-1" }; } });
  await nodes.find(node => node.type === "form").props.onSubmit({ preventDefault() {} });
  assert.equal(payload.lines[0].qty, 6);
  assert.equal(payload.supplierId, undefined);
  assert.equal(payload.lines[0].unitPrice, undefined);
  assert.ok(!nodes.some(node => node.type === "label" && node.props.children.includes("Supplier *")));
});