import type { InventoryItem, MaterialRequirement, SheetCatalogItem, SheetCatalogType } from "../api/client";

// exceljs is large, so it is only loaded when someone exports or imports a sheet.
async function excel() {
  return (await import("exceljs")).default;
}

const COLS = [
  { header: "Section", width: 28 },
  { header: "Item Code", width: 16 },
  { header: "Description", width: 56 },
  { header: "Unit", width: 8 },
  { header: "Qty", width: 10 },
  { header: "Remark", width: 32 }
];

type Meta = { label: string; value: string }[];

type ExportRow = { section: string; code: string; name: string; unit: string; qty?: number; remark?: string };

async function buildWorkbook(title: string, orgName: string, meta: Meta, rows: ExportRow[]) {
  const ExcelJS = await excel();
  const wb = new ExcelJS.Workbook();
  const ws = wb.addWorksheet("Requirement Sheet");
  ws.columns = COLS.map((c) => ({ width: c.width }));

  const top = ws.addRow([orgName.toUpperCase()]);
  top.font = { bold: true, size: 14 };
  ws.mergeCells(top.number, 1, top.number, 6);
  const sub = ws.addRow([title]);
  sub.font = { bold: true, size: 11 };
  ws.mergeCells(sub.number, 1, sub.number, 6);
  ws.addRow([]);

  for (const m of meta) {
    const r = ws.addRow([m.label, m.value]);
    r.getCell(1).font = { bold: true };
    ws.mergeCells(r.number, 2, r.number, 6);
  }
  ws.addRow([]);

  const head = ws.addRow(COLS.map((c) => c.header));
  head.eachCell((cell) => {
    cell.font = { bold: true };
    cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFE5E7EB" } };
    cell.border = { bottom: { style: "thin" } };
  });
  ws.views = [{ state: "frozen", ySplit: head.number }];

  let current = "";
  for (const row of rows) {
    if (row.section !== current) {
      current = row.section;
      const band = ws.addRow([current]);
      band.font = { bold: true };
      band.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF3F4F6" } };
      ws.mergeCells(band.number, 1, band.number, 6);
    }
    const r = ws.addRow([row.section, row.code, row.name, row.unit, row.qty ?? null, row.remark ?? ""]);
    r.getCell(1).font = { color: { argb: "FF9CA3AF" } };
    r.getCell(5).alignment = { horizontal: "right" };
  }
  return wb;
}

async function toBlob(wb: Awaited<ReturnType<typeof buildWorkbook>>) {
  const buffer = await wb.xlsx.writeBuffer();
  return new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

async function save(wb: Awaited<ReturnType<typeof buildWorkbook>>, fileName: string) {
  downloadBlob(await toBlob(wb), fileName);
}

const safe = (s: string) => s.replace(/[\\/:*?"<>|]+/g, "").trim() || "Requirement Sheet";

export interface ExportContext {
  orgName: string;
  projectName: string;
  projectCode: string;
  items: InventoryItem[];
  uoms: { id: string; code: string }[];
  sheetTypeName?: string;
}

/** Downloads a saved requirement (sheet) as an .xlsx that can be edited and imported back. */
export async function downloadRequirementXlsx(req: MaterialRequirement, ctx: ExportContext) {
  const wb = await requirementWorkbook(req, ctx);
  await save(wb, `${safe(ctx.projectName)}_${safe(ctx.sheetTypeName ?? "Material")}_${req.reqNumber}.xlsx`);
}

/** The sheet as a named .xlsx file, e.g. "Material Sheet - Structure & Civil - Site Office - MREQ-…xlsx", ready to store with the project. */
export async function requirementXlsxFile(req: MaterialRequirement, ctx: ExportContext): Promise<File> {
  const wb = await requirementWorkbook(req, ctx);
  const parts = ["Material Sheet", safe(ctx.sheetTypeName ?? "Custom Items"), req.blockName ? safe(req.blockName) : "", req.reqNumber].filter(Boolean);
  return new File([await toBlob(wb)], `${parts.join(" - ")}.xlsx`, { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
}

async function requirementWorkbook(req: MaterialRequirement, ctx: ExportContext) {
  const itemMap = Object.fromEntries(ctx.items.map((i) => [i.id, i]));
  const uomMap = Object.fromEntries(ctx.uoms.map((u) => [u.id, u]));
  const rows: ExportRow[] = req.lines
    .map((l) => ({
      section: l.sectionName || "Other",
      code: itemMap[l.itemId]?.code ?? "",
      name: itemMap[l.itemId]?.name ?? l.itemId,
      unit: uomMap[l.uomId ?? ""]?.code ?? "",
      qty: l.qty,
      remark: l.notes ?? ""
    }))
    .sort((a, b) => a.section.localeCompare(b.section) || a.name.localeCompare(b.name));

  const meta: Meta = [
    { label: "Project", value: ctx.projectName },
    { label: "Project Code", value: ctx.projectCode },
    { label: "Requirement #", value: req.reqNumber },
    { label: "Sheet Type Code", value: req.sheetType ?? "" },
    { label: "Block Name", value: req.blockName ?? "" },
    { label: "Block Size", value: req.blockSize ?? "" },
    { label: "Ship To", value: req.destinationAddress ?? "" },
    { label: "Priority", value: req.priority },
    { label: "Required Date", value: req.requiredDate ?? "" },
    { label: "Status", value: req.status },
    { label: "Notes", value: req.notes ?? "" }
  ];
  const wb = await buildWorkbook(`MATERIAL REQUIREMENT PLANNING${ctx.sheetTypeName ? ` — ${ctx.sheetTypeName}` : ""}`, ctx.orgName, meta, rows);
  return wb;
}

/** Blank sheet with every item of the sheet type, ready to fill in and import. */
export async function downloadTemplateXlsx(type: SheetCatalogType, ctx: { orgName: string; projectName: string; projectCode: string }) {
  const rows: ExportRow[] = type.sections.flatMap((s) =>
    s.items.map((i) => ({ section: s.name, code: i.code, name: i.name, unit: i.uomCode ?? "" }))
  );
  const meta: Meta = [
    { label: "Project", value: ctx.projectName },
    { label: "Project Code", value: ctx.projectCode },
    { label: "Sheet Type Code", value: type.code },
    { label: "Block Name", value: "" },
    { label: "Block Size", value: "" },
    { label: "Ship To", value: "" },
    { label: "Priority", value: "MEDIUM" },
    { label: "Required Date", value: "" },
    { label: "Notes", value: "" }
  ];
  const wb = await buildWorkbook(`MATERIAL REQUIREMENT PLANNING — ${type.name}`, ctx.orgName, meta, rows);
  await save(wb, `${safe(ctx.projectName)}_${safe(type.name)}_Template.xlsx`);
}

// ---------------------------------------------------------------------------------------------
// Import
// ---------------------------------------------------------------------------------------------

export interface ParsedSheet {
  sheetTypeCode?: string;
  header: { blockName?: string; blockSize?: string; destination?: string; priority?: string; requiredDate?: string; notes?: string };
  lines: { itemId: string; qty: number; remark?: string }[];
  unmatched: { label: string; qty: number }[];
  mode: "template" | "layout";
}

type Cell = unknown;

function cellText(v: Cell): string {
  if (v == null) return "";
  if (typeof v === "object") {
    const o = v as { text?: string; richText?: { text: string }[]; result?: unknown; hyperlink?: string };
    if (o.richText) return o.richText.map((r) => r.text).join("").trim();
    if (o.text != null) return String(o.text).trim();
    if (o.result != null) return cellText(o.result);
    if (v instanceof Date) return v.toISOString().slice(0, 10);
    return "";
  }
  return String(v).trim();
}

function cellNumber(v: Cell): number | null {
  const t = cellText(v).replace(/,/g, "");
  if (!t) return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

const tokens = (s: string) =>
  s.toLowerCase().replace(/(\d)\s*["\u201d\u2033]/g, "$1 inch ").replace(/(\d)\s*['\u2019]/g, "$1 ft ").replace(/["'\u201d\u2033\u2019]/g, " ")
    .split(/[^a-z0-9]+/).filter(Boolean)
    .map((t) => (t.length > 3 && t.endsWith("s") && !/\d/.test(t) ? t.slice(0, -1) : t));

const NOISE = new Set(["nos", "no", "mt", "mtr", "pcs", "m"]);

// A wanted token matches an item token exactly, or as an abbreviation prefix ("stru" ~ "structure").
const tokenHit = (want: string, have: Set<string>) =>
  have.has(want) || (want.length >= 3 && !/\d/.test(want) && [...have].some((h) => h.startsWith(want)));

function matchItem(label: string, items: SheetCatalogItem[]): SheetCatalogItem | null {
  const want = tokens(label).filter((t) => !NOISE.has(t) && (t.length > 1 || /\d/.test(t)));
  if (want.length === 0) return null;
  let best: SheetCatalogItem | null = null;
  let bestScore = 0;
  let tie = false;
  for (const it of items) {
    const have = new Set(tokens(it.name));
    const hit = want.filter((t) => tokenHit(t, have)).length;
    const score = hit / want.length - (have.size - hit) * 0.001; // prefer the tightest name on equal coverage
    if (hit / want.length < 0.8) continue;
    if (score > bestScore + 1e-9) { best = it; bestScore = score; tie = false; }
    else if (Math.abs(score - bestScore) < 1e-9) tie = true;
  }
  return tie ? null : best;
}

export async function parseRequirementXlsx(file: File, catalog: SheetCatalogType[]): Promise<ParsedSheet> {
  const ExcelJS = await excel();
  const wb = new ExcelJS.Workbook();
  await wb.xlsx.load(await file.arrayBuffer());
  const ws = wb.worksheets[0];
  if (!ws) throw new Error("The workbook has no sheets.");

  const grid: string[][] = [];
  const nums: (number | null)[][] = [];
  ws.eachRow({ includeEmpty: true }, (row, r) => {
    const texts: string[] = [];
    const ns: (number | null)[] = [];
    for (let c = 1; c <= Math.max(row.cellCount, 12); c++) {
      const v = row.getCell(c).value;
      texts[c - 1] = cellText(v);
      ns[c - 1] = cellNumber(v);
    }
    grid[r - 1] = texts;
    nums[r - 1] = ns;
  });

  const result: ParsedSheet = { header: {}, lines: [], unmatched: [], mode: "template" };
  const allItems = catalog.flatMap((t) => t.sections.flatMap((s) => s.items));
  const byCode = new Map(allItems.map((i) => [i.code.toLowerCase(), i]));

  // Header facts: "Label :-  value" in any of the first rows.
  const labels: [RegExp, keyof ParsedSheet["header"] | "sheetType"][] = [
    [/^block name\s*[:\-]*$/i, "blockName"], [/^block size\s*[:\-]*$/i, "blockSize"], [/^ship to\s*[:\-]*$/i, "destination"],
    [/^priority\s*[:\-]*$/i, "priority"], [/^required date\s*[:\-]*$/i, "requiredDate"], [/^notes?\s*[:\-]*$/i, "notes"],
    [/^sheet type code\s*[:\-]*$/i, "sheetType"]
  ];
  for (let r = 0; r < Math.min(grid.length, 30); r++) {
    const row = grid[r] ?? [];
    row.forEach((text, c) => {
      const hit = labels.find(([re]) => re.test(text));
      if (!hit) return;
      const value = row.slice(c + 1).find((t) => t && !/^[:\-]+$/.test(t));
      if (!value) return;
      if (hit[1] === "sheetType") result.sheetTypeCode = value;
      else (result.header as Record<string, string>)[hit[1]] = value;
    });
  }

  const add = (item: SheetCatalogItem, qty: number, remark?: string) => {
    const existing = result.lines.find((l) => l.itemId === item.id);
    if (existing) existing.qty += qty;
    else result.lines.push({ itemId: item.id, qty, remark: remark || undefined });
  };

  // Template layout (exported by this app): has an "Item Code" column.
  const codeHeader = grid.findIndex((row) => row?.some((t) => /^item code$/i.test(t)) && row.some((t) => /qty/i.test(t)));
  if (codeHeader >= 0) {
    const h = grid[codeHeader];
    const ci = h.findIndex((t) => /^item code$/i.test(t));
    const di = h.findIndex((t) => /^description$/i.test(t));
    const qi = h.findIndex((t) => /qty/i.test(t));
    const ri = h.findIndex((t) => /^remark/i.test(t));
    for (let r = codeHeader + 1; r < grid.length; r++) {
      const row = grid[r];
      const code = row?.[ci];
      const qty = nums[r]?.[qi];
      if (!code || qty == null || qty <= 0) continue;
      const item = byCode.get(code.toLowerCase());
      if (item) add(item, qty, ri >= 0 ? row[ri] : undefined);
      else result.unmatched.push({ label: `${code} ${di >= 0 ? row[di] : ""}`.trim(), qty });
    }
  } else {
    // The planning team's own layout: side-by-side blocks of Description | Size | Qty | Remark.
    result.mode = "layout";
    for (let hr = 0; hr < grid.length; hr++) {
      const row = grid[hr] ?? [];
      const descCols = row.map((t, i) => (/^description$/i.test(t) ? i : -1)).filter((i) => i >= 0);
      if (descCols.length === 0) continue;
      for (const d of descCols) {
        const find = (re: RegExp, from: number, to: number) => {
          for (let i = from; i <= to && i < row.length; i++) if (re.test(row[i])) return i;
          return -1;
        };
        const s = find(/^size$/i, d + 1, d + 2);
        const q = find(/qty/i, d + 1, d + 4);
        if (q < 0) continue;
        const rm = find(/^remark/i, q + 1, q + 2);
        const next = descCols.find((x) => x > d) ?? Infinity;
        let lastDesc = "";
        for (let r = hr + 1; r < grid.length; r++) {
          const line = grid[r] ?? [];
          if (line.some((t, i) => i >= d && i < next && /^items?$/i.test(t))) break; // footer table (steel quantity)
          const desc = line[d] ?? "";
          const size = s >= 0 ? line[s] ?? "" : "";
          const qty = nums[r]?.[q] ?? null;
          if (desc && !size && qty == null) { lastDesc = ""; continue; } // section heading
          if (desc) lastDesc = desc;
          if (qty == null || qty <= 0) continue;
          const label = `${lastDesc} ${size}`.trim();
          const item = matchItem(label, allItems);
          if (item) add(item, qty, rm >= 0 ? line[rm] : undefined);
          else result.unmatched.push({ label, qty });
        }
      }
      break; // the header row is shared by every block, parse it once
    }
  }

  if (!result.sheetTypeCode) {
    const counts = catalog.map((t) => ({
      code: t.code,
      n: result.lines.filter((l) => t.sections.some((s) => s.items.some((i) => i.id === l.itemId))).length
    }));
    const top = counts.sort((a, b) => b.n - a.n)[0];
    if (top && top.n > 0) result.sheetTypeCode = top.code;
  }
  return result;
}
