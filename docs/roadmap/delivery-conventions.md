# Delivery Conventions

This page records how PowerHub V2 is owned, reviewed, branched, released, and where it runs. It closes the Phase 0 item "define code ownership, review policy, branching, release naming, and supported environments" of the [implementation plan](implementation-plan.md). Change it through a pull request like any other document.

## Repository

V2 is developed on `main` of [thanhtan2210/PowerHub](https://github.com/thanhtan2210/PowerHub). The original team repository holds the prototype preserved under `legacy/` and does not receive V2 changes.

## Ownership

| Area | Owner |
| --- | --- |
| Everything in the repository | @thanhtan2210 |

`.github/CODEOWNERS` states the same. The review roles in [documentation governance](../documentation-governance.md) are all held by the owner while the project has one maintainer. When a second contributor joins, split ownership by the paths in the [repository structure](../architecture/repository-structure.md) and update both files.

## Branches

- Work happens on short-lived branches cut from `main` and merged back through a pull request.
- Branch names are `<type>/<topic>`, using the commit types below, for example `feat/telemetry-ingestion` or `docs/phase-0-2-closeout`.
- A pull request may stack on another open branch. Fix a defect in a stack on the lowest branch and merge it forward; do not rebase.
- Published history is never rewritten: no force-push, amend, or rebase of a branch that has been pushed.

## Commits

- One concern per commit, so it can be reviewed and reverted on its own.
- Subject in Conventional Commits form, `type(scope): summary`, with types `feat`, `fix`, `test`, `docs`, `build`, `ci`, and `chore`.
- The body explains why. Reference requirement IDs and ADRs where they apply.

## Merging

A pull request is merged into `main` with a merge commit when:

- every job of the CI workflow passes: backend, frontend, images, manifests, and traceability;
- the change updates the requirements register, contracts, tests, runbooks, and documentation it affects, as the [Definition of Done](definition-of-done.md) requires;
- the owner has reviewed it.

A dependency with a newly published advisory fails restore, because NuGet audit warnings are errors. Fix it on the branch that is being merged by moving to the patched version, pinning a transitive package centrally if needed. Do not lower the audit level.

## Releases

- A release is a commit on `main`. There are no semantic application versions before the first production release.
- The Images workflow publishes every commit on `main` as `ghcr.io/thanhtan2210/powerhub-<component>:<full commit SHA>`, scanned, signed, and with SBOM and provenance attached.
- Environments reference images only by digest. Releasing to staging is a pull request titled `build(kubernetes): release <short SHA> to staging` that changes the digests in the staging overlay. The [staging runbook](../operations/staging-runbook.md) describes the deployment, and the outcome is recorded in [staging evidence](../operations/staging-evidence.md).
- Public and device contracts carry their own semantic major versions, as [ADR 0008](../adr/0008-api-contracts.md) requires.

## Environments

| Environment | Purpose | Defined by |
| --- | --- | --- |
| Local | Feature development with Docker Compose | `deploy/compose/` |
| CI | Every pull request and every commit on `main`, on GitHub-hosted Ubuntu runners | `.github/workflows/` |
| Staging | Release verification on a developer workstation | [ADR 0014](../adr/0014-workstation-staging.md), `deploy/kubernetes/overlays/staging` |
| Production | Not provisioned | `deploy/kubernetes/overlays/production`, pending the provider decisions in the implementation plan |
