import { describe, expect, it, vi } from "vitest";
import { PantryClient } from "./pantryClient";

const response = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

describe("PantryClient", () => {
  it("encodes list filters and pagination", async () => {
    const fetcher = vi.fn().mockResolvedValue(response([]));
    await new PantryClient("https://api.example", "token", fetcher).list({ search: "red apple", type: "Fruits", availability: "Available", expiresBefore: "2026-12-01", page: 2, pageSize: 10 });
    expect(fetcher).toHaveBeenCalledWith(expect.stringContaining("/api/v1/pantry?"), expect.anything());
    expect(fetcher.mock.calls[0][0]).toContain("search=red+apple");
    expect(fetcher.mock.calls[0][0]).toContain("page=2");
    expect(fetcher.mock.calls[0][1].headers.get("Authorization")).toBe("Bearer token");
  });

  it("sends public DTOs for create and actions", async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(response({ id: "1" }, 201))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const client = new PantryClient("", undefined, fetcher);
    await client.add({ name: "Rice", type: "Grains", quantity: 2, unit: "Cup", expiresAt: "" });
    await client.consume("1", "v1");
    expect(fetcher.mock.calls[0][1].body).toContain('"type":"Grains"');
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toMatchObject({ unit: "Cup", expiresAt: null });
    expect(fetcher.mock.calls[1][0]).toBe("/api/v1/pantry/1/consume");
    expect(fetcher.mock.calls[1][1].headers.get("If-Match")).toBe("v1");
  });

  it("sends the update version as If-Match rather than a query parameter", async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    await new PantryClient("", "token", fetcher).update("1", {
      name: "Rice", type: "Grains", quantity: 2, unit: "Cup", expiresAt: ""
    }, "v1");

    expect(fetcher.mock.calls[0][0]).toBe("/api/v1/pantry/1");
    expect(fetcher.mock.calls[0][1].headers.get("If-Match")).toBe("v1");
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toMatchObject({ type: "Grains", unit: "Cup", expiresAt: null });
    expect(fetcher.mock.calls[0][1].headers.get("Authorization")).toBe("Bearer token");
  });

  it("sends date-only expiration as an ISO timestamp", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({ id: "1" }, 201));
    await new PantryClient("", "token", fetcher).add({
      name: "Rice", type: "Grains", quantity: 2, unit: "Cup", expiresAt: "2026-12-01"
    });
    expect(JSON.parse(fetcher.mock.calls[0][1].body).expiresAt).toBe("2026-12-01T00:00:00Z");
  });

  it("surfaces API errors", async () => {
    const client = new PantryClient("", undefined, vi.fn().mockResolvedValue(response({ detail: "Invalid item" }, 400)));
    await expect(client.delete("1")).rejects.toMatchObject({ status: 400, message: "Invalid item" });
  });
});
