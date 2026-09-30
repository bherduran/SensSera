"use client";

import { useState } from "react";
import { ArrowRight } from "lucide-react";
import { insightErrorMessage, useAskInsights } from "@/hooks/use-insights";
import { FieldNote } from "@/components/notebook/field-note";

const SUGGESTIONS = [
  "Which greenhouse was hottest in the last 24 hours?",
  "Are there any active alerts right now?",
  "What was the average humidity today?",
];

/** A question line at the top of the notebook; answers come back as field notes. */
export function AskPanel() {
  const [question, setQuestion] = useState("");
  const ask = useAskInsights();

  function submit(q: string) {
    const trimmed = q.trim();
    if (!trimmed || ask.isPending) return;
    setQuestion(trimmed);
    ask.mutate(trimmed);
  }

  return (
    <section className="rounded-md border bg-card px-5 pt-4 pb-5 paper-shadow">
      <p className="label-caps">Ask the notebook</p>

      <form
        className="mt-2 flex items-end gap-3 border-b border-foreground/70 focus-within:border-foreground"
        onSubmit={(e) => {
          e.preventDefault();
          submit(question);
        }}
      >
        <input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Which greenhouse ran hottest today?"
          maxLength={500}
          aria-label="Ask a question about your greenhouses"
          className="h-11 min-w-0 flex-1 bg-transparent font-serif text-xl outline-none placeholder:text-muted-foreground/70 placeholder:italic"
        />
        <button
          type="submit"
          disabled={ask.isPending || !question.trim()}
          aria-label="Ask"
          className="mb-2 inline-flex items-center gap-1 font-mono text-xs uppercase tracking-wider disabled:opacity-40"
        >
          Ask <ArrowRight className="size-3.5" />
        </button>
      </form>

      {!ask.data && !ask.isPending && !ask.isError && (
        <ul className="mt-3 flex flex-wrap gap-x-5 gap-y-1.5">
          {SUGGESTIONS.map((s) => (
            <li key={s}>
              <button
                type="button"
                onClick={() => submit(s)}
                className="font-mono text-xs text-muted-foreground underline decoration-dotted underline-offset-4 hover:text-foreground"
              >
                {s}
              </button>
            </li>
          ))}
        </ul>
      )}

      {ask.isPending && <FieldNote pending className="mt-4" />}

      {ask.isError && (
        <p className="mt-3 text-sm text-alert-text">
          {insightErrorMessage(ask.error, "Couldn’t answer that. Please try again.")}
        </p>
      )}

      {ask.data && !ask.isPending && (
        <div className="mt-4">
          <FieldNote>{ask.data.answer}</FieldNote>
          <p className="mt-2 font-mono text-[11px] text-muted-foreground">
            {ask.data.model}
            {ask.data.usedFunctions.length > 0 && ` · used ${ask.data.usedFunctions.join(", ")}`}
          </p>
        </div>
      )}
    </section>
  );
}
