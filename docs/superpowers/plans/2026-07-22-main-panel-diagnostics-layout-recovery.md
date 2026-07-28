# Main Panel Diagnostics Layout Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the screenshot-matched conditional two-pane main panel while preserving the narrow layout when diagnostics are absent.

**Architecture:** Keep the existing C# payload and React control rendering. Restructure only the normal main-panel content so controls occupy an 18em pane and an optional diagnostics payload renders in a separate 30em pane with its own scroller; retain existing CSS variables for legacy UI opacity and blur.

**Tech Stack:** React 18, TypeScript 4.8, SCSS modules, Node test runner, webpack

## Global Constraints

- Diagnostics absent: one narrow 18em controls pane.
- Diagnostics present: adjacent 18em controls and 30em diagnostics panes.
- Controls and diagnostics scroll independently.
- Reuse `--panelColorNormal`, `--panelColorDark`, `--panelBlur`, and existing text-color variables.
- Do not change C# bindings, settings, localization, simulation, saves, or traffic-group UI.
- Do not copy build artifacts to the installed mod directory or launch the game.

---

### Task 1: Protect the recovered conditional layout with a failing UI test

**Files:**
- Modify: `TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs`
- Test: `TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs`

**Interfaces:**
- Consumes: `content.tsx` SCSS class references and `mainPanel.module.scss` class definitions.
- Produces: a source-level regression contract for `controlsPane` and `diagnosticsPane`.

- [ ] **Step 1: Write the failing test**

Add this focused test after the existing diagnostics rendering test:

```js
test("diagnostics expand the narrow main panel into a dedicated second pane", async () => {
  const content = await source("src/mods/components/main-panel/content.tsx");
  const styles = await source("src/mods/components/main-panel/mainPanel.module.scss");
  const controlsPane = content.indexOf("styles.controlsPane");
  const diagnosticsCondition = content.indexOf("{transitSignalPriorityDiagnostics && (");
  const diagnosticsPane = content.indexOf("styles.diagnosticsPane", diagnosticsCondition);
  const diagnosticsTitle = content.indexOf('title="TransitSignalPriorityDiagnostics"', diagnosticsPane);

  assert.notEqual(controlsPane, -1);
  assert.notEqual(diagnosticsCondition, -1);
  assert.notEqual(diagnosticsPane, -1);
  assert.notEqual(diagnosticsTitle, -1);
  assert.ok(controlsPane < diagnosticsCondition);
  assert.ok(diagnosticsCondition < diagnosticsPane);
  assert.ok(diagnosticsPane < diagnosticsTitle);
  assert.match(styles, /\.controlsPane\s*\{[^}]*width:\s*18em;/s);
  assert.match(styles, /\.diagnosticsPane\s*\{[^}]*width:\s*30em;/s);
  assert.match(styles, /\.controlsPane\s*\{[^}]*background-color:\s*var\(--panelColorNormal\);/s);
  assert.match(styles, /\.diagnosticsPane\s*\{[^}]*background-color:\s*var\(--panelColorDark\);/s);
  assert.match(styles, /\.diagnosticsPane\s*\{[^}]*backdrop-filter:\s*var\(--panelBlur\);/s);
});
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `node --test --test-name-pattern="dedicated second pane" tests/transit-signal-priority-panel.test.mjs`

Expected: FAIL because `styles.controlsPane` and `styles.diagnosticsPane` do not exist.

- [ ] **Step 3: Commit the failing regression test**

```powershell
git add -- TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs
git commit -m "test: cover conditional diagnostics panel layout"
```

### Task 2: Restore the screenshot-matched React and SCSS structure

**Files:**
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/main-panel/content.tsx`
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/main-panel/mainPanel.module.scss`
- Test: `TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs`

**Interfaces:**
- Consumes: optional `mainData.transitSignalPriority.diagnostics` and existing `Scrollable`, `Title`, `Row`, `Divider`, and diagnostic event styles.
- Produces: conditional `controlsPane` and `diagnosticsPane` elements inside `contentContainer`.

- [ ] **Step 1: Split the current container styling into an outer flex shell and two panes**

Replace the current `.contentContainer` declaration with:

```scss
.contentContainer {
  color: var(--textColor);
  display: flex;
  flex: 1;
  overflow: hidden;
  position: relative;
}

.controlsPane {
  width: 18em;
  background-color: var(--panelColorNormal);
  backdrop-filter: var(--panelBlur);
  display: flex;
  flex-shrink: 0;
  overflow: hidden;
  padding: 0.25em;
}

.diagnosticsPane {
  width: 30em;
  background-color: var(--panelColorDark);
  backdrop-filter: var(--panelBlur);
  display: flex;
  flex-shrink: 0;
  min-width: 0;
  overflow: hidden;
  padding: 0.25em;
}
```

- [ ] **Step 2: Put controls in the narrow pane and diagnostics in the conditional right pane**

In the `mainData` return, replace the current opening:

```tsx
<div className={styles.contentContainer}>
    <Scrollable style={{flex: 1}} contentStyle={{flex: 1}} trackStyle={{marginLeft: "0.25em"}}>
```

with:

```tsx
<div className={styles.contentContainer}>
    <div className={styles.controlsPane}>
        <Scrollable style={{flex: 1}} contentStyle={{flex: 1}} trackStyle={{marginLeft: "0.25em"}}>
```

Remove the complete `transitSignalPriorityDiagnostics` conditional currently nested inside the controls `Scrollable`, from its opening line:

```tsx
{transitSignalPriorityDiagnostics && (
```

through its matching closing line immediately before the lane-direction divider. Then replace the final closing tags of the `mainData` return:

```tsx
    </Scrollable>
</div>
```

with this complete controls-pane close and conditional diagnostics pane:

```tsx
        </Scrollable>
    </div>
    {transitSignalPriorityDiagnostics && (
        <div className={styles.diagnosticsPane}>
            <Scrollable style={{flex: 1}} contentStyle={{flex: 1}} trackStyle={{marginLeft: "0.25em"}}>
                <Title itemType="title" title="TransitSignalPriorityDiagnostics" />
                {transitSignalPriorityDiagnostics.rows.map((row) => (
                    <Row key={row.label} hoverEffect={false}>
                        <div className={styles.contentLabel}>
                            {translate(`UI.LABEL[C2VM.TrafficLightsEnhancement.${row.label}]`) ?? row.label}: {row.value}
                        </div>
                    </Row>
                ))}
                {transitSignalPriorityDiagnostics.events && transitSignalPriorityDiagnostics.events.length > 0 && (
                    <>
                        <Divider />
                        <Title itemType="title" title="TSPDiagnosticsEvents" />
                        {transitSignalPriorityDiagnostics.events.map((event) => (
                            <Row key={`${event.sequence}-${event.title}`} hoverEffect={false}>
                                <div className={styles.diagnosticEvent}>
                                    <div className={styles.diagnosticEventTitle}>{event.title}</div>
                                    {event.detail && (
                                        <div className={styles.diagnosticEventDetail}>{event.detail}</div>
                                    )}
                                </div>
                            </Row>
                        ))}
                    </>
                )}
            </Scrollable>
        </div>
    )}
</div>
```

In the `emptyData` return, replace its opening:

```tsx
<div className={styles.contentContainer}>
    <Scrollable style={{flex: 1}} contentStyle={{flex: 1}} trackStyle={{marginLeft: "0.25em"}}>
```

with:

```tsx
<div className={styles.contentContainer}>
    <div className={styles.controlsPane}>
        <Scrollable style={{flex: 1}} contentStyle={{flex: 1}} trackStyle={{marginLeft: "0.25em"}}>
```

and replace its final closing tags:

```tsx
    </Scrollable>
</div>
```

with:

```tsx
        </Scrollable>
    </div>
</div>
```

- [ ] **Step 3: Run the focused test**

Run: `node --test --test-name-pattern="dedicated second pane" tests/transit-signal-priority-panel.test.mjs`

Expected: PASS.

- [ ] **Step 4: Run the complete UI suite and production build**

Run: `npm test`

Expected: all UI tests pass.

Run: `npm run build`

Expected: webpack completes successfully without TypeScript or SCSS errors.

- [ ] **Step 5: Commit the recovered implementation**

```powershell
git add -- TrafficLightsEnhancement/UI/src/mods/components/main-panel/content.tsx TrafficLightsEnhancement/UI/src/mods/components/main-panel/mainPanel.module.scss
git commit -m "fix: restore expanded diagnostics panel layout"
```

### Task 3: Verify branch scope without installing the mod

**Files:**
- Verify: `TrafficLightsEnhancement/UI/src/mods/components/main-panel/content.tsx`
- Verify: `TrafficLightsEnhancement/UI/src/mods/components/main-panel/mainPanel.module.scss`
- Verify: `TrafficLightsEnhancement/UI/tests/transit-signal-priority-panel.test.mjs`

**Interfaces:**
- Consumes: completed layout recovery and existing vanilla group-lockstep fix.
- Produces: evidence that source and tests are ready for a separately approved installation/playtest.

- [ ] **Step 1: Re-run the relevant simulation and UI tests**

Run from the repository root: `dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest --no-restore`

Expected: all ECS tests pass.

Run from `TrafficLightsEnhancement/UI`: `npm test`

Expected: all UI tests pass.

- [ ] **Step 2: Check the final branch diff and installed-mod boundary**

```powershell
git diff --check origin/main...HEAD
git status --short --branch
```

Expected: no whitespace errors; only the already-known untracked vanilla lockstep plan remains outside commits. Do not run the repository Release build because its project targets deploy directly into the installed mod directory.
