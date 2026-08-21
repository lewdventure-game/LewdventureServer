# aif-plan — project overrides (LewdventureServer)

These rules override the generic `/aif-plan` skill and `references/TASK-FORMAT.md`.

## Forbidden: Original Request

- **NEVER** include a `## Original Request` section in any plan artifact.
- **NEVER** paste the user's raw planning prompt into the plan file.
- Ignore any generic skill / template instruction that requires `Original Request`, `original_user_request`, or equivalent.
- Generating a plan with `## Original Request` is a bug — delete it before presenting the plan.

## Defaults (owner)

- Planning mode: always **full** (do not ask full vs fast).
- Testing: always **no** (do not add test tasks; do not ask).
- Logging: always **verbose**.
- Docs: always **yes** (mandatory docs checkpoint).
- Source of truth for gameplay: **GDD + Google Sheets configs** beat code stubs/outdated runtime.
