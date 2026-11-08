export const PANTRY_TYPES = ["Meat", "Grains", "Fruits", "Vegetables", "Spices"] as const;
export type PantryType = (typeof PANTRY_TYPES)[number] | (string & {});
export type Unit = "Count" | "Gram" | "Kilogram" | "Milliliter" | "Liter" | "Ounce" | "Pound" | "Cup" | "Tablespoon" | "Teaspoon";
export type Availability = "Available" | "Consumed" | "Unavailable";

export interface PantryItem {
  id: string;
  name: string;
  type?: PantryType;
  customCategory?: string;
  notes?: string;
  quantity: number;
  unit: Unit;
  expiresAt?: string;
  availability?: Availability;
  state?: Availability;
  version?: string;
}
export interface PantryListFilters {
  search?: string;
  type?: string;
  availability?: Availability | "";
  expiresBefore?: string;
  page?: number;
  pageSize?: number;
}
export interface PantryItemRequest {
  name: string;
  type: string;
  customCategory?: string;
  quantity: number;
  unit: Unit;
  expiresAt?: string;
  notes?: string;
}

function requestBody(item: PantryItemRequest): string {
  const expiresAt = item.expiresAt && /^\d{4}-\d{2}-\d{2}$/.test(item.expiresAt)
    ? `${item.expiresAt}T00:00:00Z`
    : item.expiresAt || null;
  return JSON.stringify({ ...item, expiresAt });
}

export class PantryApiError extends Error {
  constructor(public readonly status: number, message: string) { super(message); }
}

export class PantryClient {
  constructor(private readonly baseUrl = "", private readonly token?: string, private readonly fetcher: typeof fetch = fetch) {}

  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const headers = new Headers(init?.headers);
    headers.set("Accept", "application/json");
    if (this.token) headers.set("Authorization", `Bearer ${this.token}`);
    if (init?.body) headers.set("Content-Type", "application/json");
    const response = await this.fetcher(`${this.baseUrl.replace(/\/$/, "")}${path}`, { ...init, headers });
    if (!response.ok) {
      let message = `Pantry request failed (${response.status})`;
      try { message = (await response.json()).detail ?? message; } catch { /* non-json problem response */ }
      throw new PantryApiError(response.status, message);
    }
    if (response.status === 204) return undefined as T;
    return response.json() as Promise<T>;
  }

  list(filters: PantryListFilters = {}): Promise<PantryItem[]> {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries({ page: filters.page ?? 1, pageSize: filters.pageSize ?? 20, search: filters.search, type: filters.type, availability: filters.availability, expiresBefore: filters.expiresBefore })) {
      if (value !== undefined && value !== "") params.set(key, String(value));
    }
    return this.request<PantryItem[]>(`/api/v1/pantry?${params}`);
  }
  add(item: PantryItemRequest) { return this.request<PantryItem>("/api/v1/pantry", { method: "POST", body: requestBody(item) }); }
  update(id: string, item: PantryItemRequest, version?: string) {
    return this.request<void>(`/api/v1/pantry/${encodeURIComponent(id)}`, {
      method: "PUT",
      headers: version ? { "If-Match": version } : undefined,
      body: requestBody(item)
    });
  }
  consume(id: string, version?: string) { return this.action(id, "consume", version); }
  unavailable(id: string, version?: string) { return this.action(id, "unavailable", version); }
  delete(id: string) { return this.request<void>(`/api/v1/pantry/${encodeURIComponent(id)}`, { method: "DELETE" }); }
  private action(id: string, action: string, version?: string) {
    return this.request<void>(`/api/v1/pantry/${encodeURIComponent(id)}/${action}`, { method: "POST", headers: version ? { "If-Match": version } : undefined });
  }
}
