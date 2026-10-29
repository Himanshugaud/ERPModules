import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const source = readFileSync(new URL("../src/pages/Planning.tsx", import.meta.url), "utf8");
const parsed = ts.createSourceFile("Planning.tsx", source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
const component = parsed.statements.find(statement => ts.isFunctionDeclaration(statement) && statement.name?.text === "MaterialsEstimateModal");
assert.ok(component);
const compiled = ts.transpileModule(component.getText(parsed), {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.React }
}).outputText;

const saved = { id: "saved", reqNumber: "MREQ-NEW", status: "DRAFT", sheetType: "CIVIL", priority: "MEDIUM", destinationAddress: "Site", lines: [{ itemId: "item", qty: 2 }] };

function renderWorkspace({ existing = [], custom = false, api = {} } = {}) {
  const states = new Map([
    [6, existing], [7, false], [9, custom], [10, [{ code: "CIVIL", name: "Civil" }]], [11, custom ? null : { typeCode: "CIVIL" }]
  ]);
  let stateIndex = 0;
  const render = runInNewContext(`${compiled}\nMaterialsEstimateModal;`, {
    exports: {},
    React: { Fragment: "Fragment", createElement: (type, props, ...children) => ({ type, props: { ...props, children } }) },
    useAuth: () => ({ user: { permissions: ["materialrequirement.create"], organizationName: "Test" } }),
    useEffect: () => {},
    useRef: () => ({ current: null }),
    useState: initial => {
      const index = stateIndex++;
      if (!states.has(index)) states.set(index, typeof initial === "function" ? initial() : initial);
      return [states.get(index), next => states.set(index, typeof next === "function" ? next(states.get(index)) : next)];
    },
    api: {
      allItems: async () => ({ data: [] }),
      uploadAttachment: async () => {},
      projectAttachments: async () => [],
      deleteAttachment: async () => {},
      projectPlan: async () => ({ status: "DRAFT", requirementCount: 1 }),
      ...api
    },
    requirementXlsxFile: async () => ({ name: "sheet.xlsx" }),
    formatDate: date => date ?? "",
    SheetCreator: "SheetCreator",
    AddMaterialsModal: "AddMaterialsModal"
  });
  function nodes() {
    stateIndex = 0;
    const result = [];
    function visit(node) {
      if (Array.isArray(node)) return node.forEach(visit);
      if (!node || typeof node !== "object") return;
      result.push(node);
      visit(node.props.children);
    }
    visit(render({ project: { id: "project", name: "Project", code: "PRJ" }, warehouses: [], items: [], uoms: [], onClose() {} }));
    return result;
  }
  return { nodes, states };
}

test("first save displays the planning sheet before Excel generation finishes", async () => {
  let release;
  const pendingItems = new Promise(resolve => { release = resolve; });
  const workspace = renderWorkspace({ api: { allItems: () => pendingItems } });
  const onSaved = workspace.nodes().find(node => node.type === "SheetCreator").props.onSaved;
  const pendingSave = onSaved(saved);
  assert.ok(workspace.nodes().some(node => node.type === "div" && node.props.children.includes("MREQ-NEW")));
  assert.ok(!workspace.nodes().some(node => node.type === "SheetCreator"));
  release({ data: [] });
  await pendingSave;
});

test("custom-item sheet appears while attachment upload is still pending", async () => {
  let release;
  const pendingUpload = new Promise(resolve => { release = resolve; });
  const workspace = renderWorkspace({ custom: true, api: { uploadAttachment: () => pendingUpload } });
  const pendingSave = workspace.nodes().find(node => node.type === "AddMaterialsModal").props.onSaved({ ...saved, sheetType: undefined });
  assert.equal(workspace.states.get(6)[0].id, saved.id);
  assert.ok(!workspace.nodes().some(node => node.type === "AddMaterialsModal"));
  release();
  await pendingSave;
});

test("editing replaces only the previous sheet and keeps the remaining sheets", async () => {
  const old = { ...saved, id: "old", reqNumber: "MREQ-OLD" };
  const other = { ...saved, id: "other", reqNumber: "MREQ-OTHER" };
  const workspace = renderWorkspace({ existing: [old, other] });
  await workspace.nodes().find(node => node.type === "SheetCreator").props.onSaved(saved, old);
  assert.deepEqual(Array.from(workspace.states.get(6), sheet => sheet.id), ["saved", "other"]);
});

test("Excel upload failure leaves the saved sheet visible and shows its warning", async () => {
  const workspace = renderWorkspace({ api: { uploadAttachment: async () => { throw new Error("Unavailable"); } } });
  await workspace.nodes().find(node => node.type === "SheetCreator").props.onSaved(saved);
  assert.ok(workspace.nodes().some(node => node.type === "div" && node.props.children.includes("MREQ-NEW")));
  assert.match(workspace.states.get(8), /sheet was saved.*Excel copy/);
});

test("repeated save notifications do not duplicate the saved sheet", async () => {
  const workspace = renderWorkspace({ existing: [saved] });
  await workspace.nodes().find(node => node.type === "SheetCreator").props.onSaved(saved);
  assert.equal(workspace.states.get(6).length, 1);
});