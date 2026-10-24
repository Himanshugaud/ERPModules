import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const source = readFileSync(new URL("../src/pages/Requirements.tsx", import.meta.url), "utf8");
const parsed = ts.createSourceFile("Requirements.tsx", source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
const declaration = parsed.statements.find((statement) => ts.isFunctionDeclaration(statement) && statement.name?.text === "pickStatusId");
assert.ok(declaration, "Requirement conversion must define its initial status selector.");
const compiled = ts.transpileModule(declaration.getText(parsed), { compilerOptions: { target: ts.ScriptTarget.ES2022 } });
const pickStatusId = runInNewContext(`${compiled.outputText}\npickStatusId;`);
const mergeDeclaration = parsed.statements.find((statement) => ts.isFunctionDeclaration(statement) && statement.name?.text === "mergeProjectRequirements");
assert.ok(mergeDeclaration, "Requirements must merge backend projects with saved drafts.");
const mergeCompiled = ts.transpileModule(mergeDeclaration.getText(parsed), { compilerOptions: { target: ts.ScriptTarget.ES2022 } });
const mergeProjectRequirements = runInNewContext(`${mergeCompiled.outputText}\nmergeProjectRequirements;`);
const filterDeclaration = parsed.statements.find((statement) => ts.isFunctionDeclaration(statement) && statement.name?.text === "filterRequirements");
assert.ok(filterDeclaration, "Requirements must define list filtering.");
const filterCompiled = ts.transpileModule(filterDeclaration.getText(parsed), { compilerOptions: { target: ts.ScriptTarget.ES2022 } });
const filterRequirements = runInNewContext(`${filterCompiled.outputText}\nfilterRequirements;`);

test("requirement conversion selects Planning even when Inventory Check is first", () => {
  assert.equal(pickStatusId([{ id: "inventory", code: "INVENTORY_CHECK" }, { id: "planning", code: "PLANNING" }]), "planning");
});

test("requirement conversion refuses to substitute Inventory Check for missing Planning", () => {
  assert.throws(() => pickStatusId([{ id: "inventory", code: "INVENTORY_CHECK" }]), /Planning status is not configured/);
});

test("requirement conversion refuses an empty workflow catalog", () => {
  assert.throws(() => pickStatusId([]), /Planning status is not configured/);
});

test("completed backend projects appear in requirements with their workflow status", () => {
  const rows = mergeProjectRequirements([], [{ id: "project-1", name: "Completed project", statusId: "completed", createdAt: "2026-10-20T00:00:00Z" }], [{ id: "completed", code: "COMPLETED", name: "Completed" }], [], []);
  assert.equal(rows.length, 1);
  assert.equal(rows[0].title, "Completed project");
  assert.equal(rows[0].status, "CONVERTED");
  assert.equal(rows[0].projectStatusName, "Completed");
  assert.equal(rows[0].isProjectRecord, true);
});

test("backend projects do not duplicate locally saved converted requirements", () => {
  const local = { id: "draft-1", title: "Original requirement", status: "CONVERTED", projectId: "project-1", createdAt: "2026-10-20T00:00:00Z" };
  const rows = mergeProjectRequirements([local], [{ id: "project-1", name: "Original requirement", statusId: "planning", createdAt: local.createdAt }], [{ id: "planning", code: "PLANNING", name: "Planning" }], [], []);
  assert.equal(rows.length, 1);
  assert.equal(rows[0].id, "draft-1");
  assert.equal(rows[0].projectStatusName, "Planning");
});

test("local requirement drafts remain in the merged list", () => {
  const draft = { id: "draft-1", title: "Unconverted requirement", status: "NEW", createdAt: "2026-10-20T00:00:00Z" };
  const rows = mergeProjectRequirements([draft], [], [], [], []);
  assert.equal(rows.length, 1);
  assert.equal(rows[0].title, draft.title);
  assert.equal(rows[0].status, "NEW");
});

function renderRequirements(requirements, selectedDocuments = null) {
  const component = parsed.statements.find((statement) => ts.isFunctionDeclaration(statement) && statement.name?.text === "Requirements");
  const output = ts.transpileModule(component.getText(parsed), {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.React }
  });
  const updates = new Map();
  let stateIndex = 0;
  const render = runInNewContext(`${output.outputText}\nRequirements;`, {
    exports: {},
    React: { Fragment: "Fragment", createElement: (type, props, ...children) => ({ type, props: { ...props, children } }) },
    useAuth: () => ({ user: { organizationId: "org", permissions: ["project.read"] } }),
    useNavigate: () => () => {},
    useEffect: () => {},
    useState: (initial) => {
      const index = stateIndex++;
      return [index === 8 ? selectedDocuments : typeof initial === "function" ? initial() : initial, (next) => updates.set(index, next)];
    },
    loadReqs: () => requirements,
    mergeProjectRequirements,
    filterRequirements,
    formatDate: (date) => date,
    AttachmentsPanel: "AttachmentsPanel"
  });
  const nodes = [];
  function visit(node) {
    if (Array.isArray(node)) { node.forEach(visit); return; }
    if (!node || typeof node !== "object") return;
    nodes.push(node);
    visit(node.props.children);
  }
  visit(render());
  return { nodes, updates };
}

const convertedRequirement = { id: "draft", title: "Converted requirement", status: "CONVERTED", projectId: "linked-project" };

test("converted requirements retain Documents and Open Project without another conversion action", () => {
  const { nodes, updates } = renderRequirements([convertedRequirement]);
  const buttons = nodes.filter((node) => node.type === "button");
  const documents = buttons.find((node) => node.props.children[0] === "Documents");
  assert.ok(documents);
  assert.ok(buttons.some((node) => node.props.children[0] === "Open Project"));
  assert.ok(!buttons.some((node) => String(node.props.children[0]).startsWith("Convert")));
  documents.props.onClick();
  assert.equal(updates.get(8), convertedRequirement);
});

test("converted requirement documents use the linked project and only the requirement category", () => {
  const { nodes } = renderRequirements([convertedRequirement], convertedRequirement);
  const panel = nodes.find((node) => node.type === "AttachmentsPanel");
  assert.equal(panel.props.projectId, "linked-project");
  assert.deepEqual(Array.from(panel.props.categories), ["REQUIREMENT"]);
  assert.equal(panel.props.defaultCategory, "REQUIREMENT");
  const dialog = nodes.find((node) => node.props.role === "dialog");
  assert.ok(dialog);
  assert.equal(dialog.props.style.width, "min(640px, calc(100vw - 40px))");
});

test("unconverted requirements keep draft editing and conversion actions", () => {
  const { nodes } = renderRequirements([{ ...convertedRequirement, status: "NEW", projectId: undefined }]);
  const labels = nodes.filter((node) => node.type === "button").map((node) => node.props.children[0]);
  assert.ok(labels.includes("Edit / Documents"));
  assert.ok(labels.some((label) => String(label).startsWith("Convert")));
  assert.ok(!labels.includes("Documents"));
  assert.ok(!labels.includes("Open Project"));
});

const filterRows = [
  { id: "draft", title: "House extension", description: "New estimate", clientName: "North Client", clientId: "north", priority: "HIGH", status: "NEW" },
  { id: "planning", title: "North renovation", description: "", clientName: "North Client", clientId: "north", priority: "HIGH", status: "CONVERTED", projectStatusCode: "PLANNING" },
  { id: "completed", title: "Warehouse fit-out", description: "Final delivery", clientName: "South Client", clientId: "south", priority: "LOW", status: "CONVERTED", projectStatusCode: "COMPLETED" }
];

test("requirement filters combine text, client and priority", () => {
  const rows = filterRequirements(filterRows, "renovation", "north", "HIGH", "");
  assert.deepEqual(Array.from(rows, (row) => row.id), ["planning"]);
});

test("requirement status filter includes completed converted projects", () => {
  const rows = filterRequirements(filterRows, "", "", "", "PROJECT:COMPLETED");
  assert.deepEqual(Array.from(rows, (row) => row.id), ["completed"]);
});

test("converted filter includes projects across workflow phases", () => {
  const rows = filterRequirements(filterRows, "", "", "", "CONVERTED");
  assert.deepEqual(Array.from(rows, (row) => row.id), ["planning", "completed"]);
});