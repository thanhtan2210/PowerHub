# Operational Runbooks

These are design-phase runbooks. They define decision points, evidence, safety controls, and ownership before platform-specific commands exist. Executable commands, provider links, dashboards, contacts, and exact thresholds must be added and tested in staging before production.

## Runbooks

- [Deployment and Rollback](deployment-and-rollback.md)
- [Backup and Restore](backup-and-restore.md)
- [Incident Response](incident-response.md)

## Runbook standard

Every production runbook must include:

- purpose, scope, owner, and last exercise date;
- prerequisites and required authorization;
- observable entry and exit conditions;
- copy-safe commands with explicit environment and resource targets;
- validation after every material change;
- stop conditions and escalation path;
- evidence and audit-record location;
- rollback or recovery path;
- known limitations.

No procedure should require an operator to infer a destructive target from an unverified variable or broad wildcard.
