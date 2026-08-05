"use client";

import { useState } from "react";
import { Sparkles, Loader2, SendHorizontal } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useAskInsights } from "@/hooks/use-insights";

const SUGGESTIONS = [
  "Which greenhouse was hottest in the last 24 hours?",
  "Are there any active alerts right now?",
  "What was the average humidity today?",
];

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
    <Card className="space-y-4 p-5">
      <div className="flex items-center gap-2">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10">
          <Sparkles className="h-4 w-4 text-primary" />
        </div>
        <div>
          <p className="font-semibold leading-tight">Ask SensSera</p>
          <p className="text-xs text-muted-foreground">
            Natural-language questions over your own greenhouse data.
          </p>
        </div>
      </div>

      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          submit(question);
        }}
      >
        <Input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="e.g. Which greenhouse was hottest today?"
          maxLength={500}
          aria-label="Ask a question about your greenhouses"
        />
        <Button
          type="submit"
          disabled={ask.isPending || !question.trim()}
          aria-label="Ask"
        >
          {ask.isPending ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <SendHorizontal className="h-4 w-4" />
          )}
        </Button>
      </form>

      {!ask.data && !ask.isPending && (
        <div className="flex flex-wrap gap-1.5">
          {SUGGESTIONS.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => submit(s)}
              className="cursor-pointer rounded-full border px-2.5 py-1 text-xs text-muted-foreground transition-colors hover:border-primary/40 hover:text-foreground"
            >
              {s}
            </button>
          ))}
        </div>
      )}

      {ask.isPending && (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Thinking&hellip;
        </div>
      )}

      {ask.isError && (
        <p className="text-sm text-destructive">
          Couldn&rsquo;t answer that. Please try again.
        </p>
      )}

      {ask.data && !ask.isPending && (
        <div className="space-y-2 rounded-lg border bg-muted/30 p-3">
          <p className="text-sm leading-relaxed">{ask.data.answer}</p>
          <p className="text-[11px] text-muted-foreground">
            {ask.data.model}
            {ask.data.usedFunctions.length > 0 &&
              ` · used ${ask.data.usedFunctions.join(", ")}`}
          </p>
        </div>
      )}
    </Card>
  );
}
