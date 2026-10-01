import { Button } from "../components/ui/button";
import { ui } from "./uiMessages";
import { useState } from "react";
import type { Block, Item, Answer } from "./contracts";
const safeUrl = (url?: string) => {
  try {
    return url && new URL(url).protocol === "https:" ? url : undefined;
  } catch {
    return undefined;
  }
};
function ImageBlock({ block }: { block: Block }) {
  const [failed, setFailed] = useState(false);
  return failed || !safeUrl(block.url) ? (
    <p role="status">
      {ui.imageUnavailable}
      {block.alt}
    </p>
  ) : (
    <img
      src={block.url}
      alt={block.alt ?? ui.materialIllustration}
      onError={() => setFailed(true)}
    />
  );
}
export function MaterialBlocks({ blocks }: { blocks: Block[] }) {
  return (
    <div className="material-blocks">
      {blocks.map((b, i) => {
        switch (b.kind) {
          case "heading":
            return <h3 key={i}>{b.text}</h3>;
          case "paragraph":
            return <p key={i}>{b.text}</p>;
          case "list":
            return (
              <ul key={i}>
                {b.items?.map((item, n) => (
                  <li key={n}>{item}</li>
                ))}
              </ul>
            );
          case "code":
            return (
              <div key={i}>
                <small>{b.language}</small>
                <pre tabIndex={0} aria-label={`${b.language ?? "Source"} code`}>
                  <code>{b.text}</code>
                </pre>
                <Button variant="secondary"
                  type="button"
                  onClick={() => void navigator.clipboard.writeText(b.text)}
                >
                  {ui.copyCode}
                </Button>
              </div>
            );
          case "image":
            return <ImageBlock key={i} block={b} />;
          case "link":
            return safeUrl(b.url) ? (
              <p key={i}>
                <a href={b.url} target="_blank" rel="noreferrer">
                  {b.text}
                </a>
              </p>
            ) : (
              <p key={i}>{ui.unavailableReference}</p>
            );
          default:
            return (
              <p key={i} role="status">
                {ui.unsupportedContentBlock}
              </p>
            );
        }
      })}
    </div>
  );
}
export function AnswerControl({
  item,
  answer,
  onChange,
  disabled,
}: {
  item: Item;
  answer: Answer;
  onChange: (value: Answer) => void;
  disabled: boolean;
}) {
  const choices = item.interaction.choices;
  const [announcement, announce] = useState("");
  if (item.type === "Flashcard") return null;
  if (item.type === "Ordering")
    return (
      <fieldset disabled={disabled}>
        <legend>{ui.arrangeEveryItemInOrder}</legend>
        <ol className="ordering-list">
          {answer.selected.map((id, index) => (
            <li key={id}>
              <span>{choices.find((c) => c.id === id)?.text}</span>
              {[-1, 1].map((direction) => (
                <Button variant="secondary"
                  type="button"
                  key={direction}
                  disabled={
                    index + direction < 0 ||
                    index + direction >= answer.selected.length
                  }
                  aria-label={ui.moveChoice(choices.find((c) => c.id === id)?.text, direction)}
                  onClick={() => {
                    const selected = [...answer.selected];
                    [selected[index], selected[index + direction]] = [
                      selected[index + direction],
                      selected[index],
                    ];
                    onChange({ selected });
                    announce(ui.movedTo(index + direction + 1));
                  }}
                >
                  {direction === -1 ? "↑" : "↓"}
                </Button>
              ))}
            </li>
          ))}
        </ol>
        <span role="status">{announcement}</span>
      </fieldset>
    );
  if (item.type === "Matching")
    return (
      <fieldset disabled={disabled}>
        <legend>{ui.matchEveryItem}</legend>
        {choices.map((c) => (
          <label key={c.id} className="matching-row">
            <span>{c.text}</span>
            <select
              aria-label={ui.matchChoice(c.text)}
              value={answer.pairs?.[c.id] ?? ""}
              onChange={(e) =>
                onChange({
                  selected: [],
                  pairs: { ...answer.pairs, [c.id]: e.target.value },
                })
              }
            >
              <option value="">{ui.chooseACounterpart}</option>
              {item.interaction.counterparts?.map((right) => (
                <option
                  key={right.id}
                  value={right.id}
                  disabled={
                    item.interaction.oneToOne &&
                    Object.entries(answer.pairs ?? {}).some(
                      ([left, selected]) =>
                        left !== c.id && selected === right.id,
                    )
                  }
                >
                  {right.text}
                </option>
              ))}
            </select>
          </label>
        ))}
      </fieldset>
    );
  const multiple = item.type === "BugSpotting" || item.interaction.multiple;
  return (
    <fieldset disabled={disabled}>
      <legend>{multiple ? ui.selectAllThatApply : ui.chooseOneAnswer}</legend>
      {choices.map((c) => (
        <label className="answer-choice" key={c.id}>
          <input
            type={multiple ? "checkbox" : "radio"}
            name={`answer-${item.id}`}
            checked={answer.selected.includes(c.id)}
            onChange={(e) =>
              onChange({
                selected: multiple
                  ? e.target.checked
                    ? [...answer.selected, c.id]
                    : answer.selected.filter((id) => id !== c.id)
                  : [c.id],
                noIssue: false,
              })
            }
          />
          <span>{c.text}</span>
        </label>
      ))}
      {item.type === "BugSpotting" && (
        <label className="answer-choice">
          <input
            type="checkbox"
            checked={answer.noIssue ?? false}
            onChange={(e) =>
              onChange({ selected: [], noIssue: e.target.checked })
            }
          />
          {ui.noIssue}
        </label>
      )}
    </fieldset>
  );
}
