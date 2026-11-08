import React from "react";
import { createRoot } from "react-dom/client";
import { AuthenticatedPantry } from "./AuthenticatedPantry";
import { userManager } from "./auth";
import "./styles.css";

const root = createRoot(document.getElementById("root")!);
if (window.location.pathname === "/auth/silent-callback") {
  void userManager.signinSilentCallback().catch(() => {
    root.render(<p role="alert">Unable to renew the Pantry session.</p>);
  });
} else {
  root.render(<React.StrictMode><AuthenticatedPantry /></React.StrictMode>);
}
