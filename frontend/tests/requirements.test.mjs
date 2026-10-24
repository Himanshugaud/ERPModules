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

test("requirement conversion selects Planning even when Inventory Check is first", () => {
  assert.equal(pickStatusId([{ id: "inventory", code: "INVENTORY_CHECK" }, { id: "planning", code: "PLANNING" }]), "planning");
});

test("requirement conversion refuses to substitute Inventory Check for missing Planning", () => {
  assert.throws(() => pickStatusId([{ id: "inventory", code: "INVENTORY_CHECK" }]), /Planning status is not configured/);
});

test("requirement conversion refuses an empty workflow catalog", () => {
  assert.throws(() => pickStatusId([]), /Planning status is not configured/);
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
    useAuth: () => ({ user: { organizationId: "org" } }),
    useNavigate: () => () => {},
    useEffect: () => {},
    useState: (initial) => {
      const index = stateIndex++;
      return [index === 7 ? selectedDocuments : typeof initial === "function" ? initial() : initial, (next) => updates.set(index, next)];
    },
    loadReqs: () => requirements,
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
  assert.equal(updates.get(7), convertedRequirement);
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