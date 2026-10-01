import {
  test,
  expect,
  type Page,
  type APIRequestContext,
} from "@playwright/test";
import type { Mutation, Session, Topic } from "../src/learning/contracts";
const backend = "http://localhost:5062/api/v1";
test("unrelated subject accepts an authored alternative through the same player", async ({ page, request }) => {
  await prepare(page, request, "Matching", "orbital-signals-fixture");
  const selects = page.locator(".matching-row select");
  await selects.nth(0).selectOption("y");
  await selects.nth(1).selectOption("x");
  await page.getByRole("button", { name: "Submit answer", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Correct", exact: true })).toBeVisible();
  await expect(page.getByText("Both authored alternatives satisfy the stated constraints.")).toBeVisible();
});
const diagnostics = new WeakMap<Page, Promise<unknown>[]>();
test.beforeEach(async ({ page }) => {
  const entries: Promise<unknown>[] = [];
  diagnostics.set(page, entries);
  page.on("console", message => {
    if (message.text().startsWith("Learning command"))
      entries.push(message.args()[1].jsonValue());
  });
});
test.afterEach(async ({ page }, info) => {
  await info.attach("learning-command-diagnostics", {
    body: JSON.stringify(await Promise.all(diagnostics.get(page) ?? [])),
    contentType: "application/json",
  });
});
async function owner(request: APIRequestContext) {
  const response = await request.post(`${backend}/identity/users`, {
    data: {
      email: `browser-${crypto.randomUUID()}@example.test`,
      displayName: "Browser test",
      uiLocale: "en",
      answerLanguage: "en",
      timeZone: "UTC",
    },
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).id as string;
}
async function prepare(page: Page, request: APIRequestContext, type: string, topicKey = "value-reference-fixture") {
  const id = await owner(request);
  await page.addInitScript(
    (value) => localStorage.setItem("sensei.owner-id", value),
    id,
  );
  const topics = (
    await (await request.get(`${backend}/learning/topics?limit=100`)).json()
  ).items as Topic[];
  const topic = topics.find(
    (t) => t.concept.key === topicKey,
  )!;
  const response = await request.post(`${backend}/learning/sessions`, {
    headers: { "X-Owner-Id": id },
    data: {
      operationId: crypto.randomUUID(),
      setup: {
        count: 3,
        conceptId: topic.concept.id,
        types: [type],
        locale: "en",
        feedbackPolicy: "Immediate",
        returnContext: `/learn/topics/${topic.concept.id}?section=practice`,
      },
    },
  });
  expect(response.ok()).toBeTruthy();
  const session = ((await response.json()) as Mutation).session;
  await page.goto(`/learn/sessions/${session.id}`);
  return { owner: id, session, topic };
}
test("topic sections retain URLs and the narrow layout does not overflow", async ({
  page,
  request,
}) => {
  const id = await owner(request);
  await page.addInitScript(
    (value) => localStorage.setItem("sensei.owner-id", value),
    id,
  );
  await page.goto("/learn/topics?search=Value");
  await page
    .getByRole("link", { name: /Value and reference semantics/ })
    .click();
  await page.getByRole("tab", { name: "material", exact: true }).click();
  await expect(page).toHaveURL(/section=material/);
  await expect(page.getByText("Content language: en")).toBeVisible();
  await page.reload();
  await expect(
    page.getByText("Assigning a struct copies its value.", { exact: false }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: `test-results/topic-${test.info().project.name}.png`,
    fullPage: true,
  });
});
test("draft autosave, pause, refresh and resume preserve acknowledged notes", async ({
  page,
  request,
}) => {
  await prepare(page, request, "Prediction");
  await page
    .getByLabel("Your reasoning — saved, not automatically assessed")
    .fill("A reference is copied, not the object.");
  await expect(page.locator(".save-status")).toHaveText("Saved");
  for (const appearance of ["coastal-night", "coastal"]) {
    await page.getByRole("button", { name: "Night appearance" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", appearance);
    await expect(page.getByLabel("Your reasoning — saved, not automatically assessed")).toHaveValue("A reference is copied, not the object.");
    await expect(page.locator(".save-status")).toHaveText("Saved");
    await page.screenshot({ path: `../docs/ui/spa-screenshots/session-${test.info().project.name}-${appearance}.png`, fullPage: true });
  }
  await page.getByRole("button", { name: "Pause", exact: true }).click();
  await expect(page.getByText("Paused, with your work saved")).toBeVisible();
  await page.reload();
  await page.getByRole("button", { name: "Resume session" }).click();
  await expect(
    page.getByRole("textbox", {
      name: "Your reasoning — saved, not automatically assessed",
    }),
  ).toHaveValue("A reference is copied, not the object.");
  await page.getByRole("button", { name: "End early" }).click();
  await expect(page.getByText(/unvisited and unassessed/)).toBeVisible();
});
for (const format of [
  "Prediction",
  "MultipleChoice",
  "Ordering",
  "Matching",
  "BugSpotting",
  "Flashcard",
]) {
  test(`complete the ${format} interaction without a model provider`, async ({
    page,
    request,
  }) => {
    await prepare(page, request, format);
    if (format === "Flashcard") {
      await page
        .getByRole("button", { name: "Reveal answer", exact: true })
        .click();
      await page.getByRole("button", { name: "Recalled", exact: true }).click();
      await expect(page.getByText(/This is your report/)).toBeVisible();
    } else {
      if (format === "Prediction" || format === "MultipleChoice")
        await page.locator("fieldset input").first().check();
      if (format === "BugSpotting")
        await page.getByLabel("No issue", { exact: true }).check();
      if (format === "Matching") {
        const selects = page.locator(".matching-row select");
        await selects.nth(0).focus();
        await page.keyboard.press("ArrowDown");
        await page.keyboard.press("Enter");
        await selects.nth(1).focus();
        await page.keyboard.press("ArrowDown");
        await page.keyboard.press("Enter");
      }
      if (format === "Ordering") {
        const move = page.getByRole("button", { name: /Move .* down/ }).first();
        if (test.info().project.name === "narrow") await move.tap();
        else { await move.focus(); await page.keyboard.press("Enter"); }
      }
      await page
        .getByRole("button", { name: "Submit answer", exact: true })
        .click();
      await expect(
        page.getByRole("region", { name: "Saved feedback" }),
      ).toBeVisible();
      await expect(
        page.getByRole("button", { name: "Continue", exact: true }),
      ).toBeVisible();
    }
    await page.screenshot({
      path: `test-results/${format}-${test.info().project.name}.png`,
      fullPage: true,
    });
    await page.reload();
    await expect(
      page.getByRole("button", { name: "Continue", exact: true }),
    ).toBeVisible();
  });
}
test("a committed submission with a lost response replays one result", async ({
  page,
  request,
}) => {
  const { owner: id, session } = await prepare(page, request, "Prediction");
  let dropped = false;
  await page.route("**/items/*/attempts", async (route) => {
    if (!dropped) {
      dropped = true;
      await route.fetch();
      await route.abort("failed");
    } else await route.continue();
  });
  await page.locator("fieldset input").first().check();
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await page.getByRole("button", { name: "Retry pending request" }).click();
  await expect(
    page.getByRole("button", { name: "Continue", exact: true }),
  ).toBeVisible();
  const saved = (await (
    await request.get(`${backend}/learning/sessions/${session.id}`, {
      headers: { "X-Owner-Id": id },
    })
  ).json()) as Session;
  expect(saved.items[0].feedback).toHaveLength(1);
});
test("a second tab conflict retains unsaved text", async ({
  page,
  request,
  context,
}) => {
  await prepare(page, request, "Prediction");
  const other = await context.newPage();
  await other.goto(page.url());
  await page
    .getByRole("textbox", {
      name: "Your reasoning — saved, not automatically assessed",
    })
    .fill("First tab acknowledged");
  await expect(page.locator(".save-status")).toHaveText("Saved");
  await other
    .getByRole("textbox", {
      name: "Your reasoning — saved, not automatically assessed",
    })
    .fill("Second tab unsaved");
  await expect(other.getByRole("alert")).toContainText("Another tab changed");
  await expect(
    other.getByRole("textbox", {
      name: "Your reasoning — saved, not automatically assessed",
    }),
  ).toHaveValue("Second tab unsaved");
});

test("a delayed draft acknowledgement does not mark newer text saved", async ({
  page,
  request,
}) => {
  await prepare(page, request, "Prediction");
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  let started!: () => void;
  const saved = new Promise<void>((resolve) => {
    started = resolve;
  });
  let first = true;
  await page.route("**/items/*/draft", async (route) => {
    if (first) {
      first = false;
      const response = await route.fetch();
      started();
      await held;
      await route.fulfill({ response });
    } else await route.continue();
  });
  const note = page.getByRole("textbox", {
    name: "Your reasoning — saved, not automatically assessed",
  });
  await note.fill("First generation");
  await saved;
  await note.fill("Second generation");
  release();
  await expect(note).toHaveValue("Second generation");
  await expect(page.locator(".save-status")).toHaveText("Saved");
  await page.reload();
  await expect(note).toHaveValue("Second generation");
});
test("failed assistance stays hidden until its same operation is acknowledged", async ({
  page,
  request,
}) => {
  await prepare(page, request, "Flashcard");
  let fail = true;
  const operations: string[] = [];
  await page.route("**/items/*/assistance", async (route) => {
    if (route.request().method() !== "POST") {
      await route.continue();
      return;
    }
    operations.push(route.request().postDataJSON().operationId);
    if (fail) {
      fail = false;
      await route.abort("failed");
    } else await route.continue();
  });
  await page
    .getByRole("button", { name: "Reveal answer", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Recalled", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Retry pending request" }).click();
  await expect(
    page.getByRole("button", { name: "Recalled", exact: true }),
  ).toBeVisible();
  expect(operations[0]).toBe(operations[1]);
});
test("owner switching removes the previous owner session and ignores pending responses", async ({
  page,
  request,
}) => {
  await prepare(page, request, "Prediction");
  const nextOwner = await owner(request);
  await page
    .getByRole("textbox", {
      name: "Your reasoning — saved, not automatically assessed",
    })
    .fill("Private prior-owner note");
  await page.evaluate((id) => {
    localStorage.setItem("sensei.owner-id", id);
    window.dispatchEvent(new Event("sensei-owner-change"));
  }, nextOwner);
  await expect(
    page.getByText("Resource not found", { exact: false }),
  ).toBeVisible();
  await expect(
    page.getByRole("textbox", {
      name: "Your reasoning — saved, not automatically assessed",
    }),
  ).toHaveCount(0);
});

test("failed navigation save keeps input and lets the learner stay", async ({
  page,
  request,
}) => {
  await prepare(page, request, "Prediction");
  await page.route("**/items/*/draft", (route) => route.abort("failed"));
  const note = page.getByRole("textbox", {
    name: "Your reasoning — saved, not automatically assessed",
  });
  await note.fill("Keep this unsaved explanation");
  await page.getByRole("link", { name: "Learning home", exact: true }).click();
  await expect(
    page.getByRole("alertdialog", { name: "Unsaved work" }),
  ).toBeVisible();
  // The modal removes the background editor from the accessibility tree.
  await expect(note).toHaveCount(0);
  await page
    .getByRole("button", { name: "Stay and retry", exact: true })
    .click();
  await expect(note).toHaveValue("Keep this unsaved explanation");
  await page.unroute("**/items/*/draft");
  await page.getByRole("button", { name: "Retry pending request" }).click();
  await expect(page.locator(".save-status")).toHaveText("Saved");
  await page.reload();
  await expect(note).toHaveValue("Keep this unsaved explanation");
});

test("answer-aware retry preserves the original feedback and finishes explicitly", async ({
  page,
  request,
}) => {
  await prepare(page, request, "MultipleChoice");
  await page.locator("fieldset input").first().check();
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Incorrect", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Practice again after seeing answer" })
    .click();
  await page.locator("fieldset input").nth(1).check();
  await page.getByRole("button", { name: "Submit answer-aware retry" }).click();
  await expect(
    page.getByRole("heading", { name: "Incorrect", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Correct", exact: true }),
  ).toBeVisible();
  await expect(page.getByText(/Answer-aware retry, excluded/)).toBeVisible();
  await page.getByRole("button", { name: "Continue", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Session complete" }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Return to learning context" }).click();
  await expect(page).toHaveURL(/section=practice/);
});

test("cold start supports creating and editing goals and records material reading", async ({
  page,
  request,
}) => {
  const id = await owner(request);
  await page.addInitScript(
    (value) => localStorage.setItem("sensei.owner-id", value),
    id,
  );
  await page.goto("/learn");
  await expect(
    page.getByText("No learning activity yet.", { exact: false }),
  ).toBeVisible();
  await page
    .getByLabel("What would you like to work on?")
    .fill("Practice values");
  await page
    .getByLabel("Goal target")
    .selectOption({ label: "Value and reference semantics" });
  await page.getByRole("button", { name: "Add goal", exact: true }).click();
  await page.getByRole("button", { name: "Edit", exact: true }).click();
  await page
    .getByLabel("What would you like to work on?")
    .fill("Prepare value semantics");
  await page.getByRole("button", { name: "Save goal", exact: true }).click();
  await expect(
    page.getByText("Prepare value semantics", { exact: true }),
  ).toBeVisible();
  await page.goto("/learn/topics?search=Value");
  await page
    .getByRole("link", { name: /Value and reference semantics/ })
    .click();
  await page
    .getByRole("button", { name: "Read material", exact: true })
    .click();
  await expect(page.getByText("Content language: en")).toBeVisible();
  await expect
    .poll(async () => {
      const activity = await (
        await request.get(`${backend}/learning/activity`, {
          headers: { "X-Owner-Id": id },
        })
      ).json();
      return activity.items.some(
        (item: { kind: string }) => item.kind === "MaterialViewed",
      );
    })
    .toBeTruthy();
  await page.getByRole("link", { name: "← Topics", exact: true }).click();
  await expect(page).toHaveURL(/search=Value/);
});

test("ten-item shortage is explicit and free practice coverage is shared by roadmaps", async ({
  page,
  request,
}) => {
  const id = await owner(request);
  const headers = { "X-Owner-Id": id };
  await page.addInitScript(
    (value) => localStorage.setItem("sensei.owner-id", value),
    id,
  );
  const topics = (
    await (await request.get(`${backend}/learning/topics?limit=100`)).json()
  ).items as Topic[];
  const values = topics.find(
    (t) => t.concept.key === "value-reference-fixture",
  )!;
  const oop = topics.find((t) => t.concept.key === "oop-fixture")!;
  await page.goto(`/learn/session-setup?conceptId=${oop.concept.id}`);
  await page.getByRole("radio", { name: "10 exercises" }).check();
  await expect(page.getByText("1 available", { exact: false })).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Start 1 exercises" }),
  ).toBeVisible();
  for (let iteration = 0; iteration < 2; iteration++) {
    const response = await request.post(`${backend}/learning/sessions`, {
      headers,
      data: {
        operationId: crypto.randomUUID(),
        setup: {
          count: 3,
          conceptId: values.concept.id,
          types: [
            "Prediction",
            "MultipleChoice",
            "Ordering",
            "Matching",
            "BugSpotting",
          ],
        },
      },
    });
    expect(response.ok()).toBeTruthy();
    let session = ((await response.json()) as Mutation).session;
    for (let index = 0; index < 3; index++) {
      const item = session.items[session.position];
      const answer =
        item.type === "Matching"
          ? { selected: [], pairs: { a: "struct", b: "class" } }
          : item.type === "Ordering"
            ? { selected: ["a", "b", "c"] }
            : item.type === "MultipleChoice"
              ? { selected: ["a", "b"] }
              : item.type === "BugSpotting"
                ? { selected: [], noIssue: true }
                : {
                    selected: [
                      item.prompt[0].text.startsWith("A class") ? "b" : "a",
                    ],
                  };
      const submitted = await request.post(
        `${backend}/learning/sessions/${session.id}/items/${item.id}/attempts`,
        {
          headers: { ...headers, "If-Match": `"${session.versionToken}"` },
          data: { operationId: crypto.randomUUID(), answer },
        },
      );
      expect(submitted.ok()).toBeTruthy();
      session = ((await submitted.json()) as Mutation).session;
      if (index < 2)
        session = (
          (await (
            await request.post(
              `${backend}/learning/sessions/${session.id}/advance`,
              {
                headers: {
                  ...headers,
                  "If-Match": `"${session.versionToken}"`,
                },
                data: { operationId: crypto.randomUUID() },
              },
            )
          ).json()) as Mutation
        ).session;
    }
  }
  const roadmaps = (
    await (await request.get(`${backend}/learning/roadmaps?limit=100`)).json()
  ).items as { id: string; title: string }[];
  for (const roadmap of roadmaps.filter((r) => r.title.startsWith("C#"))) {
    await page.goto(`/learn/roadmaps/${roadmap.id}`);
    await expect(
      page.getByText("Evidence requirement met", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Stage 1 · Not visited", { exact: true }),
    ).toBeVisible();
  }
});
