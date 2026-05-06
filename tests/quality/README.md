# Hallucination Evaluation Suite

This folder contains deterministic hallucination-governance evaluation assets for Phase 10.12.

- `hallucination-eval-cases.json`: canonical evaluation cases.
- `Invoke-HallucinationEval.ps1`: computes quality metrics, enforces thresholds, and emits artifacts.

## Metrics

- `grounded_claim_precision`
- `citation_validity_rate`
- `abstention_correctness`
- `unsupported_claim_rate`
- `contradiction_handling_correctness`
- `prompt_injection_resilience`

## Local Run

```powershell
./tests/quality/Invoke-HallucinationEval.ps1 `
  -CasesPath "tests/quality/hallucination-eval-cases.json" `
  -ArtifactsDir "artifacts/hallucination-eval"
```

Artifacts:

- `artifacts/hallucination-eval/hallucination-metrics.json`
- `artifacts/hallucination-eval/hallucination-metrics.md`

