// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { PantryClient } from "./pantryClient";

const auth = vi.hoisted(() => {
  const handlers: Record<string, (...args: never[]) => void> = {};
  return {
    handlers,
    client: null as PantryClient | null,
    getUser: vi.fn(),
    signinRedirect: vi.fn(),
    signinRedirectCallback: vi.fn(),
    signoutRedirect: vi.fn(),
    events: {
      addUserLoaded: vi.fn((handler) => { handlers.loaded = handler; }),
      removeUserLoaded: vi.fn(),
      addUserUnloaded: vi.fn((handler) => { handlers.unloaded = handler; }),
      removeUserUnloaded: vi.fn(),
      addAccessTokenExpired: vi.fn((handler) => { handlers.expired = handler; }),
      removeAccessTokenExpired: vi.fn(),
      addSilentRenewError: vi.fn((handler) => { handlers.renewalFailed = handler; }),
      removeSilentRenewError: vi.fn(),
    },
  };
});

vi.mock("./auth", () => ({ userManager: auth }));
vi.mock("./PantryApp", () => ({
  PantryApp: ({ client }: { client: PantryClient }) => {
    auth.client = client;
    return <div>Pantry content</div>;
  },
}));

import { AuthenticatedPantry } from "./AuthenticatedPantry";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

describe("AuthenticatedPantry", () => {
  let container: HTMLDivElement;
  let root: Root;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubEnv("VITE_DEV_AUTH", "false");
    auth.client = null;
    window.history.replaceState(null, "", "/");
    auth.getUser.mockResolvedValue(null);
    auth.signinRedirect.mockResolvedValue(undefined);
    auth.signoutRedirect.mockResolvedValue(undefined);
    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);
  });

  afterEach(async () => {
    await act(async () => root.unmount());
    container.remove();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it("requires sign-in and starts the OIDC redirect", async () => {
    await act(async () => root.render(<AuthenticatedPantry />));
    expect(container.textContent).toContain("Sign in to manage your pantry");
    await act(async () => container.querySelector("button")?.click());
    expect(auth.signinRedirect).toHaveBeenCalledOnce();
  });

  it("opens the pantry without OIDC when local development auth is enabled", async () => {
    vi.stubEnv("VITE_DEV_AUTH", "true");
    const fetcher = vi.fn().mockResolvedValue(new Response("[]", { status: 200 }));
    vi.stubGlobal("fetch", fetcher);

    await act(async () => root.render(<AuthenticatedPantry />));

    expect(container.textContent).toContain("Development mode is enabled.");
    expect(container.textContent).toContain("Pantry content");
    expect(auth.getUser).not.toHaveBeenCalled();
    await auth.client?.list();
    expect(fetcher.mock.calls[0][1].headers.get("Authorization")).toBe("Bearer development-mode");
  });

  it("processes the callback and displays the authenticated app", async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response("[]", { status: 200 }));
    vi.stubGlobal("fetch", fetcher);
    window.history.replaceState(null, "", "/auth/callback?code=example");
    auth.signinRedirectCallback.mockResolvedValue({ access_token: "test-token", expired: false });
    await act(async () => root.render(<AuthenticatedPantry />));
    expect(auth.signinRedirectCallback).toHaveBeenCalledOnce();
    expect(window.location.pathname).toBe("/");
    expect(container.textContent).toContain("Pantry content");
    await auth.client?.list();
    expect(fetcher.mock.calls[0][1].headers.get("Authorization")).toBe("Bearer test-token");
    await act(async () => container.querySelector("button")?.click());
    expect(auth.signoutRedirect).toHaveBeenCalledOnce();
  });

  it("hides pantry content when the access token expires", async () => {
    auth.getUser.mockResolvedValue({ access_token: "test-token", expired: false });
    await act(async () => root.render(<AuthenticatedPantry />));
    expect(container.textContent).toContain("Pantry content");
    await act(async () => auth.handlers.expired());
    expect(container.textContent).not.toContain("Pantry content");
    expect(container.textContent).toContain("session has expired");
  });

  it("surfaces authentication failures", async () => {
    auth.signinRedirect.mockRejectedValue(new Error("unavailable"));
    await act(async () => root.render(<AuthenticatedPantry />));
    await act(async () => container.querySelector("button")?.click());
    expect(container.querySelector("[role=alert]")?.textContent).toContain("Unable to start sign-in");
  });
});
