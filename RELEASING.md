# Releasing DynamicData

DynamicData uses [Nerdbank.GitVersioning (NBGV)](https://github.com/dotnet/Nerdbank.GitVersioning) to compute package versions from git history. Versions are deterministic: the patch number is git height (commit count since `version.json`'s `version` field last changed).

## Branching model

| Branch | Purpose | Package version |
|---|---|---|
| `main` | Active development for the next minor (or major). | Pre-release: `X.Y.0-preview.N` |
| `release/<major>.x` | Lifetime branch for an entire major version (e.g. `release/9.x` hosts all 9.x minors). | Stable: `X.Y.N` |

Every PR merged to either branch publishes a NuGet package automatically via `.github/workflows/release.yml`.

## How patch numbers are computed

NBGV walks the first-parent history from `HEAD` back to the commit where `version.json`'s `version` field last changed. The count of commits is the "height". `{height}` in `version.json` is replaced with that count.

- Two PRs merging the same day get distinct heights — no collisions.
- A `version.json` change resets height to 1.
- `fetch-depth: 0` in CI is required for height to be correct.

## Automation workflows

All `version.json` edits and release-branch creation are scripted via `workflow_dispatch` actions in the GitHub Actions UI. Maintainers should not edit `version.json` by hand.

| Workflow | When to run | What it does |
|---|---|---|
| **Promote main to stable minor** (`promote-minor.yml`) | Ready to ship the next minor (e.g. `9.5.0`) from `main` to an existing release branch. | Opens two PRs: (1) merges `main` into `release/<major>.x` and sets stable version; (2) bumps `main` to the next preview minor. |
| **Cut major release** (`cut-major.yml`) | Ready to ship a new major as stable from `main` (e.g. `10.0.0`). | Creates a new `release/<major>.x` branch with stable version, then opens a PR to advance `main` to the next preview. |
| **Bump main to next major preview** (`bump-major-preview.yml`) | First breaking change is about to land on `main`. | Opens a PR bumping `main` from `<X>.Y-preview.{height}` to `<X+1>.0-preview.{height}`. |

Two passive guards run on every PR / release:

| Workflow | Trigger | What it does |
|---|---|---|
| **PR version check** (`pr-version-check.yml`) | Pull request to `main` / `release/**`. | If the PR is labeled `breaking-change` or `semver:major`, fails the check unless `main`'s major is **exactly one greater** than the latest stable tag's major (no skipping). |
| **Prerelease regression guard** (in `release.yml`) | Every push to `main`. | Fails the publish step if a stable `X.Y.*` tag already exists for the major.minor being prereleased. |

## Day-to-day flows

### Patch on the current stable line (e.g. `9.4.38`)
Open a PR targeting `release/9.x`. Merge. `release.yml` publishes `9.4.N` to NuGet automatically. **No manual version edits.**

### Preview of the next minor (e.g. `9.5.0-preview.42`)
Open a PR targeting `main`. Merge. `release.yml` publishes `9.5.0-preview.N`. The GitHub Release is automatically marked as a pre-release. **No manual version edits.**

### Promoting `main` → next stable minor (e.g. shipping `9.5.0`)
1. Run the **Promote main to stable minor** workflow from the GitHub Actions tab. Inputs: `target_release_branch=release/9.x`, `stable_version=9.5`.
2. Review and merge the two PRs it creates (promotion PR first, main-bump PR second).
3. `release.yml` ships `9.5.0` stable on the release branch; `main` continues at `9.6-preview.{height}`.

### Breaking change landing on `main`
1. Run the **Bump main to next major preview** workflow before merging the first breaking change. Inputs: `next_major=10` (must be exactly one greater than the latest stable major; the workflow refuses skips like `next_major=11` when stable is `9.x`).
2. Merge the PR it creates. `main` now publishes `10.0.0-preview.N`.
3. Label the breaking-change PR with `breaking-change`. The **PR version check** workflow will block it until step 2 has merged.

### Cutting a new major release (e.g. shipping `10.0.0`)
1. Run the **Cut major release** workflow. Inputs: `major_version=10`. Optional: `next_main_version=11.0` if more breaking changes are queued.
2. The workflow creates `release/10.x` directly (publishes `10.0.0` stable) and opens a PR to advance `main`.
3. Merge the main-bump PR.

### Manual escape hatch
The automation workflows are thin wrappers around `version.json` edits. If something goes wrong, you can always perform the equivalent edits by hand. See the workflow YAML files for the exact operations.

## Initial cutover (one-time, when introducing this infrastructure)

This is **the only time manual steps are required**, because release/9.x does not yet exist when this PR merges.

After this PR merges to `main`:

1. `main` will start publishing `9.5.0-preview.N` (the version bump in this PR took effect).
2. Cut `release/9.x` from main HEAD:
    ```sh
    git fetch origin
    git checkout -b release/9.x origin/main
    ```
3. On `release/9.x`, edit `version.json`:
    - Set `"version"` back to `"9.4"`.
    - Add `"versionHeightOffset": 37` (the height of the latest published stable tag `9.4.37`, so the next build doesn't collide with it).
4. Commit and push `release/9.x`. The next build produces `9.4.38` stable.

From this point on, all subsequent releases use the automation workflows above. No more manual `version.json` edits.

## Known limitation: bot PRs and downstream CI

PRs opened by the automation workflows are authored by `GITHUB_TOKEN`. GitHub deliberately suppresses workflow runs triggered by this token to prevent recursive workflows, so `ci-build.yml` and `pr-version-check.yml` will **not** run on these bot-authored PRs by default.

The version-bump PRs are mechanical (single-line `version.json` edits with no code impact), so this is usually fine. If you want CI checks to run on them, close and reopen the PR once, or push an empty commit, or wire the workflows to use a personal access token (PAT) stored as a repository secret.

