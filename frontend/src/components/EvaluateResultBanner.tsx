import type { EvaluateResult } from "../types/domain";

interface EvaluateResultBannerProps {
  result: EvaluateResult;
}

// FR-020 - renders per-row, for that row only (docs/architecture.md's Screens & API Calls:
// "result renders in EvaluateResultBanner for that row only").
export function EvaluateResultBanner({ result }: EvaluateResultBannerProps) {
  return (
    <p role="status" style={{ fontWeight: result.triggered ? "bold" : "normal" }}>
      {result.triggered ? "Triggered: " : "Not triggered: "}
      {result.message}
    </p>
  );
}
