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
}

// ---- Endpoints ----
export const api = {
  login: (organizationCode: string, email: string) =>
    request<{ data: LoginResponse }>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ organizationCode, email })
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

  projectStatuses: () => request<{ data: Lookup[] }>("/project-statuses").then((r) => r.data),
  projectPriorities: () => request<{ data: Lookup[] }>("/project-priorities").then((r) => r.data),
  taskStatuses: () => request<{ data: Lookup[] }>("/task-statuses").then((r) => r.data),
  taskPriorities: () => request<{ data: Lookup[] }>("/task-priorities").then((r) => r.data),
  clients: () => request<{ data: Lookup[] }>("/clients").then((r) => r.data),
  users: () => request<Paged<UserItem>>("/users?pageSize=100").then((r) => r.data)
};
