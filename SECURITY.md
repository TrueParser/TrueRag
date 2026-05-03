# Security Policy

## Reporting a Vulnerability

If you believe you have found a security issue in TrueRAG, please report it privately to the maintainers rather than opening a public issue or pull request.

Include as much detail as possible, such as:

- a short description of the issue
- the affected area or endpoint
- steps to reproduce
- any relevant logs, traces, or sample payloads

## What Not to Do

- Do not publicly disclose the issue before the maintainers have had a chance to review it.
- Do not submit exploit code in a public issue.
- Do not open a PR that contains a security fix without prior coordination.

## Scope

This policy applies to anything that could affect:

- tenant isolation
- namespace isolation
- ACL enforcement
- WAL durability or replay safety
- ingestion or retrieval authorization
- conversation or memory leakage
