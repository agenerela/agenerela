# Releasing

A release is a tagged version of the framework package that a developer can install by
URL, with its changelog as the notes. The demo games, the docs and the tools are not
released; they always load the framework from this repository.

## When, and which number

- **A release closes a phase.** When a phase's gate passes and the work is merged to
  `main`, the next minor version is cut: `0.2.0` for Phase 1, `0.3.0` for Phase 2, and so
  on. (`0.1.0` was the package skeleton, from Phase 0.)
- **A patch release** (`0.2.1`) fixes a bug in a released version without new API.
- **While the version is `0.x`, the public API may change in any release**, as the
  changelog says. `1.0.0` is the first version that promises it will not.

The version is [Semantic Versioning](https://semver.org/spec/v2.0.0.html) and the tag is the
version with a `v` in front: `v0.2.0`.

## How

Releases are cut from `main`, which only the repository owner can push to, so only the
owner releases. Every step before the tag is ordinary pull-request work.

1. **Prepare the version** on a branch off `test`:

   ```bash
   python tools/ci/release.py prepare 0.2.0
   ```

   It changes the version in `package.json` and `AgenerelaInfo.Version`, and moves the
   changelog's `[Unreleased]` entries under `## [0.2.0] - <today>`, leaving an empty
   `[Unreleased]` above them. Read the changelog section as a user would, since it becomes
   the release notes. Then do the same by hand in the repository's root
   [`CHANGELOG.md`](../CHANGELOG.md), whose sections are named after the same versions, and
   open the pull request into `test` as usual.
2. **Merge `test` into `main`** with a pull request, once the release pull request is in
   `test`.
3. **Tag the merge on `main` and push the tag:**

   ```bash
   git fetch origin
   ```

   ```bash
   git tag -a v0.2.0 origin/main -m "Agenerela 0.2.0"
   ```

   ```bash
   python tools/ci/release.py check v0.2.0
   ```

   ```bash
   git push origin v0.2.0
   ```

   The `check` is what the release workflow runs first: the tag must be on `main`, and
   `package.json`, `AgenerelaInfo.Version` and the changelog must all carry its version.
4. **The `Release` workflow publishes it.** It runs on the tag, repeats the check, and
   creates the GitHub release with the changelog section and the install line as its notes
   (`python tools/ci/release.py notes v0.2.0` shows them beforehand).

If the workflow fails, nothing was published. Fix the cause on `test`, merge it to `main`,
then move the tag: `git push origin :refs/tags/v0.2.0`, tag again, and push again. Never
move a tag after its release is published; cut a patch release instead.

## Between releases

`package.json` and `AgenerelaInfo.Version` stay at the last release, and new entries go
under `[Unreleased]`. The hygiene check `version` fails a pull request in which the two
disagree, or whose version has no dated changelog section.

## Installing a release

In Unity, *Window → Package Manager → + → Install package from git URL*, with the tag
after `#`:

```
https://github.com/agenerela/agenerela.git?path=UnityProject/Packages/com.agenerela.framework#v0.2.0
```
