import React from "react";
import { createRoot } from "react-dom/client";
import { PantryClient } from "./pantryClient";
import { PantryApp } from "./PantryApp";
import "./styles.css";

createRoot(document.getElementById("root")!).render(<React.StrictMode><PantryApp client={new PantryClient(import.meta.env.VITE_API_URL ?? "")} /></React.StrictMode>);
