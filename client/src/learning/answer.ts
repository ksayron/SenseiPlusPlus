import type { Item, Answer } from "./contracts";
export function initialAnswer(item: Item): Answer {
  return (
    item.draft.answer ?? {
      selected:
        item.type === "Ordering"
          ? item.interaction.choices.map((c) => c.id)
          : [],
      pairs: item.type === "Matching" ? {} : undefined,
      noIssue: false,
    }
  );
}
