import { useEffect, useMemo, useState } from "react";
import type { User } from "oidc-client-ts";
import { userManager } from "./auth";
import { PantryApp } from "./PantryApp";
import { PantryClient } from "./pantryClient";

let callback: Promise<User> | undefined;

export function AuthenticatedPantry() {
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const developmentAuthEnabled = import.meta.env.DEV && import.meta.env.VITE_DEV_AUTH === "true";
  const client = useMemo(
    () => accessToken
      ? new PantryClient(import.meta.env.VITE_API_URL ?? "", accessToken)
      : null,
    [accessToken],
  );

  useEffect(() => {
    if (developmentAuthEnabled) {
      setAccessToken("development-mode");
      setReady(true);
      return;
    }

    let active = true;
    const loaded = (next: User) => {
      if (active) {
        setAccessToken(!next.expired && next.access_token ? next.access_token : null);
        setError("");
      }
    };
    const unloaded = () => { if (active) setAccessToken(null); };
    const expired = () => {
      if (active) {
        setAccessToken(null);
        setError("Your session has expired. Sign in again.");
      }
    };
    const renewalFailed = () => {
      if (active) setError("Session renewal failed. Please sign in again if your session expires.");
    };
    userManager.events.addUserLoaded(loaded);
    userManager.events.addUserUnloaded(unloaded);
    userManager.events.addAccessTokenExpired(expired);
    userManager.events.addSilentRenewError(renewalFailed);

    const load = window.location.pathname === "/auth/callback"
      ? (callback ??= userManager.signinRedirectCallback())
      : userManager.getUser();
    void load.then((next) => {
      if (!active) return;
      if (window.location.pathname === "/auth/callback") {
        window.history.replaceState(null, "", "/");
        callback = undefined;
      }
      if (next) loaded(next);
      setReady(true);
    }).catch(() => {
      if (active) {
        setError("Unable to complete sign-in. Please try again.");
        setReady(true);
      }
      callback = undefined;
    });

    return () => {
      active = false;
      userManager.events.removeUserLoaded(loaded);
      userManager.events.removeUserUnloaded(unloaded);
      userManager.events.removeAccessTokenExpired(expired);
      userManager.events.removeSilentRenewError(renewalFailed);
    };
  }, [developmentAuthEnabled]);

  const signIn = () => {
    setError("");
    void userManager.signinRedirect().catch(() => setError("Unable to start sign-in. Please try again."));
  };
  const signOut = () => {
    setError("");
    void userManager.signoutRedirect().catch(() => setError("Unable to sign out. Please try again."));
  };

  if (!ready) return <p role="status">Loading session…</p>;
  return <>
    {error && <p className="alert" role="alert">{error}</p>}
    {client
      ? <>{developmentAuthEnabled
          ? <p className="alert">Development mode is enabled.</p>
          : <button onClick={signOut}>Sign out</button>}<PantryApp client={client} /></>
      : <main className="container"><h1>My pantry</h1><p>Sign in to manage your pantry.</p><button onClick={signIn}>Sign in</button></main>}
  </>;
}
