# Contracts Reference

This folder contains contract documentation used by worker materialization.

## What It Is

- Internal NDJSON contracts emitted by underlying parser packages.
- Public output JSON contracts emitted by TrueParser workers.

## Why It Exists

Workers consume package NDJSON streams and materialize them into the final pretty JSON output.
These files define that translation boundary and expected shape stability.

## Scope

- `Internal*NdJson*` or similar files: package-facing/internal transport contracts.
- `Output*Json*` files: client-facing worker output contracts.

## Location

Canonical location: `docs/contracts/`