# ADR 0006: Deferred review photo uploads

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

Interactive Review owns photo metadata/storage boundaries, but image upload
would add malware/content validation and storage deployment work beyond the
approved assigned slice.

## Decision

Defer photo upload and serving endpoints in this implementation. Keep the
service contract and documentation explicit that uploads require a file-storage
interface, generated safe names, content-type and size validation, storage
outside executable paths, authorization on every read, and scanning when
available. Do not persist or accept a client-supplied photo URL.

## Consequences

- The initial Review client/API supports text, ratings, and moderation but no
  photo upload.
- Enabling uploads requires a separate reviewed implementation and tests.
