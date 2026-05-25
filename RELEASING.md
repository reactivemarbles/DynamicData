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

- Two PRs merging the same day get distinct heights, no collisions.
- A `version.json` change resets height to 1. Combined with `"versionHeightOffset": -1` (written by the automation workflows on stable branches), the commit that sets a new stable version publishes as `X.Y.0` (per semver convention; previews stay at `X.Y.0-preview.1` because main does not use the offset).
- `fetch-depth: 0` in CI is required for height to be correct.

## Automation workflows

All `version.json` edits and release-branch creation are scripted via `workflow_dispatch` actions in the GitHub Actions UI. Maintainers should not edit `version.json` by hand.

| Workflow | When to run | What it does |
|---|---|---|
| **Promote main to stable minor** (`promote-minor.yml`) | Ready to ship the next minor (first published patch is `X.Y.0`) from `main` to an existing release branch. | Opens two PRs: (1) merges `main` into `release/<major>.x` and sets stable version (contains the full diff, NOT mechanical); (2) bumps `main` to the next preview minor (mechanical). |
| **Cut major release** (`cut-major.yml`) | Ready to ship a new major as stable from `main` (first published patch is `X.0.0`). | Opens a main-bump PR first, then creates `release/<major>.x` with the stable version, then dispatches `release.yml` to publish the first patch. |
| **Bump main to next major preview** (`bump-major-preview.yml`) | First breaking change is about to land on `main`. | Opens a PR bumping `main` from `<X>.Y-preview.{height}` to `<X+1>.0-preview.{height}`. |

Two passive guards run on every PR / release:

| Workflow | Trigger | What it does |
|---|---|---|
| **PR version check** (`pr-version-check.yml`) | Pull request to `main` / `release/*.x`. | For PRs to `main` labeled `breaking-change`, fails the check unless the PR's `version.json` major is **exactly one greater** than the latest stable tag's major (no skipping). For PRs to `release/*.x`, the check is skipped (breaking changes are a main-only concern). |
| **Prerelease regression guard** (in `release.yml`) | Every push to `main` and dispatched run on `release/*.x`. | Refuses to publish a prerelease from a `release/*.x` branch (those must be stable only). Refuses to publish a prerelease for `X.Y` if a stable `X.Y.*` tag already exists. |

## Day-to-day flows

### Patch on the current stable line (e.g. `9.4.38`)
Open a PR targeting `release/9.x`. Merge. `release.yml` publishes `9.4.N` to NuGet automatically. **No manual version edits.**

### Preview of the next minor (e.g. `9.5.0-preview.42`)
Open a PR targeting `main`. Merge. `release.yml` publishes `9.5.0-preview.N`. The GitHub Release is automatically marked as a pre-release. **No manual version edits.**

### Cherry-picking a fix from `main` to a release branch
The promote workflow merges all of `main` into the release branch. For backporting just one (or a few) commits without promoting everything:
1. `git checkout release/9.x && git pull`
2. `git checkout -b fix/backport-XYZ release/9.x`
3. `git cherry-pick <sha-on-main>` (repeat for each commit)
4. Push and open a PR targeting `release/9.x`. Merging publishes the next patch via `release.yml`. **No manual version edits.**

### Promoting `main` → next stable minor (e.g. shipping the first `9.5.x` stable)
1. Run the **Promote main to stable minor** workflow from the GitHub Actions tab. Inputs: `target_release_branch=release/9.x`, `stable_version=9.5`.
2. Review and merge the two PRs it creates. **The promotion PR is NOT mechanical**: it contains the full diff of `main` since the last promotion. Review it carefully.
3. Merge the promotion PR first, then **immediately** merge the main-bump PR. Don't leave the bump PR sitting: every push to `main` between the stable ship and the bump merge fails the prerelease-regression guard.
4. `release.yml` ships the first `9.5.x` patch (i.e. `9.5.1`) on the release branch; `main` resumes at `9.6-preview.{height}`.

### Breaking change landing on `main`
1. Run the **Bump main to next major preview** workflow before merging the first breaking change. Inputs: `next_major=10` (must be exactly one greater than the latest stable major; the workflow refuses skips like `next_major=11` when stable is `9.x`).
2. Merge the PR it creates. `main` now publishes `10.0.0-preview.N`.
3. Label the breaking-change PR with `breaking-change`. The **PR version check** workflow will block it until step 2 has merged. After step 2 merges, rebase the breaking-change PR onto the updated `main` (or merge `main` into it) so the PR head includes the bumped `version.json`. Pushing an empty commit alone is not enough: the check reads `version.json` from the PR head, which is unchanged until the bump lands in the PR's branch.

### Cutting a new major release (e.g. shipping the first `10.x` stable)
1. Run the **Cut major release** workflow. Inputs: `major_version=10`. Optional: `next_main_version=11.0` if more breaking changes are queued (the workflow refuses values that aren't `10.<minor>` or exactly `11.0`).
2. The workflow opens a main-bump PR, creates `release/10.x`, and dispatches `release.yml` against the new branch to publish the first `10.0.x` patch. The dispatched run URL is in the workflow summary.
3. Merge the main-bump PR. Don't wait: every push to `main` after the new stable ships will fail the prerelease-regression guard until this PR merges.

### Manual escape hatch
The automation workflows are thin wrappers around `version.json` edits. If something goes wrong, you can always perform the equivalent edits by hand. See the workflow YAML files for the exact operations. Recovery scenarios:

- **`cut-major` failed after pushing the release branch but before dispatching `release.yml`**: manually run `release.yml` against the new `release/<major>.x` branch from the Actions tab.
- **`promote-minor` failed mid-flight**: the bot branches `bot/promote-<version>-<run_id>` and `bot/bump-main-after-<version>-<run_id>` carry the run ID, so a retry produces fresh branches. Delete any stale bot branches or PRs from the failed run before re-running.

## Known limitation: bot PRs and downstream CI

PRs opened by the automation workflows are authored by `GITHUB_TOKEN`. GitHub deliberately suppresses workflow runs triggered by this token to prevent recursive workflows, so `ci-build.yml` and `pr-version-check.yml` will **not** run on these bot-authored PRs by default.

The main-bump PRs are mechanical (single-line `version.json` edits with no code impact). The promotion PRs from `promote-minor` carry the full diff of `main` and SHOULD be reviewed carefully, and CI should be run on them. To trigger CI on a bot PR, close and reopen it, push an empty commit, or wire the workflows to use a personal access token (PAT) stored as a repository secret.
