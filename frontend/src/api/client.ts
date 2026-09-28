const BASE_URL: string =
  (import.meta as any).env?.VITE_API_BASE_URL ?? "http://localhost:7072/api/v1";

const TOKEN_KEY = "erp_token";

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}
export function setToken(token: string) {
  localStorage.setItem(TOKEN_KEY, token);
}
export function clearToken() {
  localStorage.removeItem(TOKEN_KEY);
}

export class ApiError extends Error {
  status: number;
  code?: string;
  constructor(status: number, message: string, code?: string) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>)
  };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${BASE_URL}${path}`, { ...options, headers });

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const body = text ? JSON.parse(text) : null;

  if (!res.ok) {
    const err = body?.error;
    throw new ApiError(res.status, err?.message ?? "Request failed", err?.code);
  }
  return body as T;
}

// ---- Session-scoped cache for rarely-changing reference data ----
// Lives for the life of the SPA session (module singleton). Prevents every page
// navigation from re-fetching the same lookups over the (slow) remote SQL round trip.
const refCache = new Map<string, Promise<unknown>>();

function cached<T>(key: string, loader: () => Promise<T>): Promise<T> {
  const hit = refCache.get(key) as Promise<T> | undefined;
  if (hit) return hit;
  const p = loader().catch((e) => {
    refCache.delete(key); // don't cache failures
    throw e;
  });
  refCache.set(key, p);
  return p;
}

function invalidate(...keys: string[]) {
  keys.forEach((k) => refCache.delete(k));
}

export function clearRefCache() {
  refCache.clear();
}

// ---- Types ----
export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: {
    userId: string;
    organizationId: string;
    organizationName?: string;
    email?: string;
    username?: string;
    displayName?: string;
    roles: string[];
    permissions: string[];
  };
}

export interface Project {
  id: string;
  code: string;
  name: string;
  description?: string;
  clientId?: string;
  managerId?: string;
  statusId?: string;
  priorityId?: string;
  startDate?: string;
  plannedEndDate?: string;
  completionPercentage: number;
  budget?: number;
  currencyCode?: string;
  isArchived: boolean;
  createdAt: string;
  rowVersion: string;
}

export interface Paged<T> {
  data: T[];
  pagination: { page: number; pageSize: number; totalItems: number; totalPages: number };
}

export interface Task {
  id: string;
  projectId: string;
  parentTaskId?: string;
  title: string;
  description?: string;
  statusId?: string;
  priorityId?: string;
  assigneeId?: string;
  reporterId?: string;
  milestoneId?: string;
  sprintId?: string;
  startDate?: string;
  dueDate?: string;
  estimatedHours?: number;
  actualHours?: number;
  completionPercentage: number;
  isArchived: boolean;
  createdAt: string;
  rowVersion: string;
}

export interface Lookup {
  id: string;
  code: string;
  name: string;
}

export interface UserItem {
  id: string;
  displayName?: string;
  email: string;
  username?: string;
  firstName?: string;
  lastName?: string;
  phone?: string;
  jobTitle?: string;
  departmentId?: string;
  status?: string;
  createdAt?: string;
}

export interface Role {
  id: string;
  name: string;
  description?: string;
  isSystemRole: boolean;
  isActive: boolean;
}

export interface Permission {
  id: string;
  code: string;
  name: string;
  module?: string;
  resource?: string;
  action?: string;
}

export interface Client {
  id: string;
  code: string;
  name: string;
  email?: string;
  phone?: string;
  status?: string;
  createdAt?: string;
  updatedAt?: string;
}

// ---- Inventory types ----
export interface InventoryItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  itemType: string;
  categoryId?: string;
  baseUomId?: string;
  barcode?: string;
  trackBatches: boolean;
  trackSerials: boolean;
  trackExpiry: boolean;
  standardCost?: number;
  reorderLevel?: number;
  safetyStock?: number;
  minStock?: number;
  maxStock?: number;
  reorderQty?: number;
  isPurchasable: boolean;
  isManufactured: boolean;
  isSellable: boolean;
  isActive: boolean;
  createdAt: string;
  rowVersion: string;
}

export interface Warehouse {
  id: string;
  code: string;
  name: string;
  warehouseType: string;
  projectId?: string;
  address?: string;
  isActive: boolean;
  createdAt: string;
}

export interface Supplier {
  id: string;
  code: string;
  name: string;
  email?: string;
  phone?: string;
  address?: string;
  status: string;
  createdAt: string;
}

export interface ItemCategory {
  id: string;
  code: string;
  name: string;
  parentCategoryId?: string;
  isActive: boolean;
}

export interface Uom {
  id: string;
  code: string;
  name: string;
  isBaseUnit: boolean;
}

export interface StockLevel {
  id: string;
  itemId: string;
  warehouseId: string;
  batchId?: string;
  qtyOnHand: number;
  qtyReserved: number;
  qtyAvailable: number;
  qtyInTransit: number;
  avgUnitCost: number;
  stockValue: number;
}

export interface StockMovement {
  id: string;
  itemId: string;
  warehouseId: string;
  batchId?: string;
  movementType: string;
  direction: string;
  qty: number;
  unitCost: number;
  totalCost: number;
  refDocType?: string;
  refDocId?: string;
  projectId?: string;
  occurredAt: string;
}

export interface InventoryDocument {
  id: string;
  number: string;
  documentType: string;
  warehouseId?: string;
  sourceAddress?: string;
  toWarehouseId?: string;
  projectId?: string;
  materialRequirementId?: string;
  supplierId?: string;
  status: string;
  documentDate?: string;
  lineCount: number;
  totalValue: number;
  createdAt: string;
  lines?: { itemId: string; qty: number; uomId?: string }[];
  requestedBy?: string;
  approvedBy?: string;
  approvedAt?: string;
  dispatchedBy?: string;
  dispatchedAt?: string;
  receivedBy?: string;
  receivedAt?: string;
}

// ---- Procurement / Planning types ----
export interface MaterialRequirementLine {
  id: string;
  itemId: string;
  qty: number;
  uomId?: string;
  notes?: string;
  purchaseOrderLineId?: string;
}

export interface MaterialRequirement {
  id: string;
  reqNumber: string;
  projectId: string;
  warehouseId?: string;
  destinationAddress?: string;
  departmentId?: string;
  priority: string;
  status: string;
  requiredDate?: string;
  requestedBy?: string;
  approvedBy?: string;
  approvedAt?: string;
  rejectedBy?: string;
  rejectedAt?: string;
  rejectionReason?: string;
  purchaseOrderId?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
  rowVersion: string;
  lines: MaterialRequirementLine[];
}

export interface PurchaseOrderLine {
  id: string;
  itemId: string;
  qty: number;
  uomId?: string;
  unitPrice: number;
  taxRatePercent: number;
  taxAmount: number;
  lineTotal: number;
  qtyReceived: number;
  materialRequirementLineId?: string;
}

export interface PurchaseOrder {
  id: string;
  poNumber: string;
  supplierId: string;
  projectId?: string;
  warehouseId?: string;
  materialRequirementId?: string;
  status: string;
  orderDate?: string;
  expectedDate?: string;
  currencyCode?: string;
  subTotal: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
  rowVersion: string;
  lines: PurchaseOrderLine[];
}

export interface Bom {
  id: string;
  code: string;
  outputItemId: string;
  outputQty: number;
  uomId?: string;
  version: number;
  isActive: boolean;
  createdAt: string;
  lines: { id: string; componentItemId: string; qty: number; uomId?: string; scrapPercent: number; operation?: string }[];
}

export interface WorkOrder {
  id: string;
  woNumber: string;
  outputItemId: string;
  bomId?: string;
  plannedQty: number;
  producedQty: number;
  scrapQty: number;
  warehouseId: string;
  projectId?: string;
  status: string;
  startDate?: string;
  endDate?: string;
  createdAt: string;
  rowVersion: string;
  components: { id: string; componentItemId: string; plannedQty: number; consumedQty: number; uomId?: string }[];
}

function qstr(params: Record<string, string | number | boolean | undefined>) {
  const qs = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => v != null && v !== "" && qs.append(k, String(v)));
  return qs.toString();
}

// ---- Endpoints ----
export const api = {
  login: (organizationCode: string, email: string, password?: string) =>
    request<{ data: LoginResponse }>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ organizationCode, email, password: password || undefined })
    }).then((r) => r.data),

  projects: (params: Record<string, string | number | undefined> = {}) => {
    const qs = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => v != null && v !== "" && qs.append(k, String(v)));
    return request<Paged<Project>>(`/projects?${qs.toString()}`);
  },

  createProject: (payload: Record<string, unknown>) =>
    request<{ data: Project }>("/projects", {
      method: "POST",
      body: JSON.stringify(payload)
    }).then((r) => r.data),

  updateProject: (id: string, payload: Record<string, unknown>) =>
    request<{ data: Project }>(`/projects/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload)
    }).then((r) => r.data),

  deleteProject: (id: string) =>
    request<void>(`/projects/${id}`, { method: "DELETE" }),

  project: (id: string) => request<{ data: Project }>(`/projects/${id}`).then((r) => r.data),

  projectTasks: (projectId: string) =>
    request<Paged<Task>>(`/projects/${projectId}/tasks?pageSize=100`),

  createTask: (projectId: string, payload: Record<string, unknown>) =>
    request<{ data: Task }>(`/projects/${projectId}/tasks`, { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),

  createSubtask: (taskId: string, payload: Record<string, unknown>) =>
    request<{ data: Task }>(`/tasks/${taskId}/subtasks`, { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),

  updateTask: (taskId: string, payload: Record<string, unknown>) =>
    request<{ data: Task }>(`/tasks/${taskId}`, { method: "PUT", body: JSON.stringify(payload) }).then((r) => r.data),

  changeTaskStatus: (taskId: string, statusId: string) =>
    request<{ data: Task }>(`/tasks/${taskId}/status`, { method: "PUT", body: JSON.stringify({ statusId }) }).then((r) => r.data),

  assignTask: (taskId: string, assigneeId: string | null) =>
    request<{ data: Task }>(`/tasks/${taskId}/assignee`, { method: "PUT", body: JSON.stringify({ assigneeId }) }).then((r) => r.data),

  projectStatuses: () => cached("projectStatuses", () => request<{ data: Lookup[] }>("/project-statuses").then((r) => r.data)),
  projectPriorities: () => cached("projectPriorities", () => request<{ data: Lookup[] }>("/project-priorities").then((r) => r.data)),
  taskStatuses: () => cached("taskStatuses", () => request<{ data: Lookup[] }>("/task-statuses").then((r) => r.data)),
  taskPriorities: () => cached("taskPriorities", () => request<{ data: Lookup[] }>("/task-priorities").then((r) => r.data)),
  departments: () => cached("departments", () => request<{ data: Lookup[] }>("/departments").then((r) => r.data)),
  clients: () => cached("clients", () => request<{ data: Client[] }>("/clients").then((r) => r.data)),
  client: (id: string) => request<{ data: Client }>(`/clients/${id}`).then((r) => r.data),
  createClient: (payload: Record<string, unknown>) =>
    request<{ data: Client }>("/clients", { method: "POST", body: JSON.stringify(payload) }).then((r) => {
      invalidate("clients");
      return r.data;
    }),
  updateClient: (id: string, payload: Record<string, unknown>) =>
    request<{ data: Client }>(`/clients/${id}`, { method: "PUT", body: JSON.stringify(payload) }).then((r) => {
      invalidate("clients");
      return r.data;
    }),

  users: () => cached("users", () => request<Paged<UserItem>>("/users?pageSize=100").then((r) => r.data)),
  user: (id: string) => request<{ data: UserItem }>(`/users/${id}`).then((r) => r.data),
  createUser: (payload: Record<string, unknown>) =>
    request<{ data: UserItem }>("/users", { method: "POST", body: JSON.stringify(payload) }).then((r) => {
      invalidate("users");
      return r.data;
    }),
  updateUser: (id: string, payload: Record<string, unknown>) =>
    request<{ data: UserItem }>(`/users/${id}`, { method: "PUT", body: JSON.stringify(payload) }).then((r) => {
      invalidate("users");
      return r.data;
    }),
  roles: () => cached("roles", () => request<{ data: Role[] }>("/roles").then((r) => r.data)),
  rolePermissions: (roleId: string) =>
    cached(`role-permissions:${roleId}`, () => request<{ data: Permission[] }>(`/roles/${roleId}/permissions`).then((r) => r.data)),
  userRoles: (userId: string) => request<{ data: Role[] }>(`/users/${userId}/roles`).then((r) => r.data),
  assignUserRole: (userId: string, roleId: string) =>
    request<void>(`/users/${userId}/roles/${roleId}`, { method: "POST" }),
  removeUserRole: (userId: string, roleId: string) =>
    request<void>(`/users/${userId}/roles/${roleId}`, { method: "DELETE" }),
  setUserPassword: (userId: string, password: string) =>
    request<void>(`/users/${userId}/password`, { method: "PUT", body: JSON.stringify({ password }) }),
  changeMyPassword: (currentPassword: string | undefined, newPassword: string) =>
    request<void>("/me/password", { method: "PUT", body: JSON.stringify({ currentPassword: currentPassword || undefined, newPassword }) }),

  // ---- Inventory ----
  items: (params: Record<string, string | number | boolean | undefined> = {}) =>
    request<Paged<InventoryItem>>(`/items?${qstr(params)}`),
  item: (id: string) => request<{ data: InventoryItem }>(`/items/${id}`).then((r) => r.data),
  createItem: (payload: Record<string, unknown>) =>
    request<{ data: InventoryItem }>("/items", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),
  updateItem: (id: string, payload: Record<string, unknown>) =>
    request<{ data: InventoryItem }>(`/items/${id}`, { method: "PUT", body: JSON.stringify(payload) }).then((r) => r.data),
  deleteItem: (id: string) => request<void>(`/items/${id}`, { method: "DELETE" }),

  warehouses: () => cached("warehouses", () => request<{ data: Warehouse[] }>("/warehouses").then((r) => r.data)),
  createWarehouse: (payload: Record<string, unknown>) =>
    request<{ data: Warehouse }>("/warehouses", { method: "POST", body: JSON.stringify(payload) }).then((r) => {
      invalidate("warehouses");
      return r.data;
    }),
  updateWarehouse: (id: string, payload: Record<string, unknown>) =>
    request<{ data: Warehouse }>(`/warehouses/${id}`, { method: "PUT", body: JSON.stringify(payload) }).then((r) => {
      invalidate("warehouses");
      return r.data;
    }),

  suppliers: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<Supplier>>(`/suppliers?${qstr(params)}`),
  createSupplier: (payload: Record<string, unknown>) =>
    request<{ data: Supplier }>("/suppliers", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),
  updateSupplier: (id: string, payload: Record<string, unknown>) =>
    request<{ data: Supplier }>(`/suppliers/${id}`, { method: "PUT", body: JSON.stringify(payload) }).then((r) => r.data),

  itemCategories: () => cached("itemCategories", () => request<{ data: ItemCategory[] }>("/item-categories").then((r) => r.data)),
  createItemCategory: (payload: Record<string, unknown>) =>
    request<{ data: ItemCategory }>("/item-categories", { method: "POST", body: JSON.stringify(payload) }).then((r) => {
      invalidate("itemCategories");
      return r.data;
    }),
  uoms: () => cached("uoms", () => request<{ data: Uom[] }>("/units-of-measure").then((r) => r.data)),

  stock: (params: Record<string, string | number | boolean | undefined> = {}) =>
    request<Paged<StockLevel>>(`/stock?${qstr(params)}`),
  lowStock: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<StockLevel>>(`/stock/low?${qstr(params)}`),
  stockMovements: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<StockMovement>>(`/stock/movements?${qstr(params)}`),

  goodsReceipts: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<InventoryDocument>>(`/goods-receipts?${qstr(params)}`),
  createGoodsReceipt: (payload: Record<string, unknown>) =>
    request<{ data: InventoryDocument }>("/goods-receipts", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),

  purchaseOrders: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<PurchaseOrder>>(`/purchase-orders?${qstr(params)}`),
  purchaseOrder: (id: string) => request<{ data: PurchaseOrder }>(`/purchase-orders/${id}`).then((r) => r.data),

  materialIssues: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<InventoryDocument>>(`/material-issues?${qstr(params)}`),
  createMaterialIssue: (payload: Record<string, unknown>) =>
    request<{ data: InventoryDocument }>("/material-issues", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),

  stockTransfers: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<InventoryDocument>>(`/stock-transfers?${qstr(params)}`),
  stockTransfer: (id: string) => request<{ data: InventoryDocument }>(`/stock-transfers/${id}`).then((r) => r.data),
  createStockTransfer: (payload: Record<string, unknown>) =>
    request<{ data: InventoryDocument }>("/stock-transfers", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),
  approveStockTransfer: (id: string) =>
    request<{ data: InventoryDocument }>(`/stock-transfers/${id}/approve`, { method: "POST" }).then((r) => r.data),
  dispatchStockTransfer: (id: string) =>
    request<{ data: InventoryDocument }>(`/stock-transfers/${id}/dispatch`, { method: "POST" }).then((r) => r.data),
  receiveStockTransfer: (id: string) =>
    request<{ data: InventoryDocument }>(`/stock-transfers/${id}/receive`, { method: "POST" }).then((r) => r.data),

  // ---- Planning / Material Requirements ----
  materialRequirements: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<MaterialRequirement>>(`/material-requirements?${qstr(params)}`),
  materialRequirement: (id: string) =>
    request<{ data: MaterialRequirement }>(`/material-requirements/${id}`).then((r) => r.data),
  createMaterialRequirement: (payload: Record<string, unknown>) =>
    request<{ data: MaterialRequirement }>("/material-requirements", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),
  approveMaterialRequirement: (id: string) =>
    request<{ data: MaterialRequirement }>(`/material-requirements/${id}/approve`, { method: "POST" }).then((r) => r.data),
  rejectMaterialRequirement: (id: string, reason: string) =>
    request<{ data: MaterialRequirement }>(`/material-requirements/${id}/reject`, { method: "POST", body: JSON.stringify({ reason }) }).then((r) => r.data),

  stockAdjustments: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<InventoryDocument>>(`/stock-adjustments?${qstr(params)}`),
  createStockAdjustment: (payload: Record<string, unknown>) =>
    request<{ data: InventoryDocument }>("/stock-adjustments", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),

  boms: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<Bom>>(`/boms?${qstr(params)}`),
  bom: (id: string) => request<{ data: Bom }>(`/boms/${id}`).then((r) => r.data),
  createBom: (payload: Record<string, unknown>) =>
    request<{ data: Bom }>("/boms", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),

  workOrders: (params: Record<string, string | number | undefined> = {}) =>
    request<Paged<WorkOrder>>(`/work-orders?${qstr(params)}`),
  workOrder: (id: string) => request<{ data: WorkOrder }>(`/work-orders/${id}`).then((r) => r.data),
  createWorkOrder: (payload: Record<string, unknown>) =>
    request<{ data: WorkOrder }>("/work-orders", { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data),
  releaseWorkOrder: (id: string) =>
    request<{ data: WorkOrder }>(`/work-orders/${id}/release`, { method: "POST" }).then((r) => r.data),
  completeWorkOrder: (id: string, payload: Record<string, unknown>) =>
    request<{ data: WorkOrder }>(`/work-orders/${id}/complete`, { method: "POST", body: JSON.stringify(payload) }).then((r) => r.data)
};

// Warm the reference-data cache so pages don't fetch these lookups on first visit.
// Fire-and-forget; per-call failures (e.g. 403 on /users) are isolated and self-heal.
export function prefetchReferenceData(permissions: string[] = []) {
  const loaders: Promise<unknown>[] = [api.departments(), api.users()];
  if (permissions.some((permission) => permission === "project.read" || permission === "project.create")) {
    loaders.push(api.projectStatuses(), api.projectPriorities(), api.clients());
  }
  if (permissions.includes("task.read")) {
    loaders.push(api.taskStatuses(), api.taskPriorities());
  }
  void Promise.allSettled(loaders);
}
