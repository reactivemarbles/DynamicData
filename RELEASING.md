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

## Day-to-day flows

### Shipping a patch on the current stable line (e.g. `9.4.36`)
1. Open a PR targeting `release/9.x`.
2. Merge. `release.yml` publishes `9.4.N` to NuGet automatically.

### Shipping a pre-release of the next minor (e.g. `9.5.0-preview.42`)
1. Open a PR targeting `main`.
2. Merge. `release.yml` publishes `9.5.0-preview.N` to NuGet automatically. The GitHub Release is marked as a pre-release.

### Promoting `main` → stable on the existing release branch (e.g. 9.5)
1. Open a PR from `main` into `release/9.x`.
2. In the same PR, edit `version.json` on `release/9.x` to bump the version field from `"9.4"` → `"9.5"` (no `-preview` suffix).
3. Merge. `release.yml` publishes `9.5.0` stable.
4. On `main`, open a follow-up PR bumping `version.json` from `"9.5-preview.{height}"` to `"9.6-preview.{height}"` (or `"10.0-preview.{height}"` if breaking changes are planned next).

### Bumping the major version (breaking changes)
When the first breaking change PR for the next major is merging to `main`, include a `version.json` bump from `"9.5-preview.{height}"` to `"10.0-preview.{height}"`. From that commit forward, `main` publishes `10.0.0-preview.N`.

### Cutting a new release branch for a new major (e.g. `release/10.x`)
1. Create `release/10.x` from `main` at the commit you want to ship as 10.0.0.
2. Edit `version.json` on `release/10.x`: change `"version"` from `"10.0-preview.{height}"` to `"10.0"`.
3. Push. `release.yml` publishes `10.0.0` stable.
4. On `main`, open a PR bumping to the next dev version (`"10.1-preview.{height}"` or `"11.0-preview.{height}"`).

## Cutting `release/9.x` for the first time

This needs to happen **once**, alongside introducing this prerelease infrastructure. The recommended sequence is **two PRs** so the cutover requires no cherry-picks or version height offsets:

### Recommended cutover sequence

1. **PR1 — workflow + docs only** (this commit): updates `.github/workflows/*.yml` to trigger on `main` and `release/**`, adds `RELEASING.md`. Leaves `version.json` at `"9.4"`.
2. **Merge PR1** to main. Main is now publishing `9.4.N` stable (height continues from before).
3. **Cut `release/9.x`** from the post-PR1 main HEAD: `git checkout -b release/9.x main && git push -u origin release/9.x`. `release/9.x` now has the new workflows and `version.json = "9.4"` — it will publish `9.4.N` stable on every merge.
4. **PR2 — version bump only**: changes `version.json` from `"9.4"` to `"9.5-preview.{height}"`. Target: `main`.
5. **Merge PR2** to main. Main starts publishing `9.5.0-preview.N` pre-releases.

### Alternative: single PR + versionHeightOffset

If you prefer one PR for everything (workflow + version bump), the cutover requires a manual `versionHeightOffset` on `release/9.x` to avoid colliding with existing tags:

1. Merge the combined PR to main. Main starts publishing `9.5.0-preview.N`.
2. Cut `release/9.x` from post-merge main HEAD.
3. On `release/9.x`, edit `version.json`:
    - Set `"version"` back to `"9.4"`.
    - Add `"versionHeightOffset": <N>` where `<N>` is the height of the last published `9.4.N` tag (currently `9.4.35` → offset `35`).
4. Commit and push `release/9.x`. Next build produces `9.4.{1 + N + further_height}`.
