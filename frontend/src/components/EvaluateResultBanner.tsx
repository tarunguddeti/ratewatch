import type { EvaluateResult } from "../types/domain";
import styles from "./EvaluateResultBanner.module.css";

interface EvaluateResultBannerProps {
  result: EvaluateResult;
}

// FR-020 - renders per-row, for that row only (docs/architecture.md's Screens & API Calls:
// "result renders in EvaluateResultBanner for that row only"). FR-007 - triggered vs.
// not-triggered must be visually distinguishable at a glance, not just by reading the text.
export function EvaluateResultBanner({ result }: EvaluateResultBannerProps) {
  return (
    <p role="status" className={`${styles.banner} ${result.triggered ? styles.triggered : styles.notTriggered}`}>
      {result.triggered ? "Triggered: " : "Not triggered: "}
      {result.message}
    </p>
  );
}
