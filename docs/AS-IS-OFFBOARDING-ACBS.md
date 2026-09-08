# ACBS Offboarding - AS-IS

## Purpose

This document summarizes the current ACBS offboarding process identified from Confluence operational documentation and the ACBS API POC.

## Current process

1. HR communicates the employee offboarding.
2. A ServiceNow System Access request is created.
3. An ACBS administrator searches for the account and performs the application-specific deprovisioning activities.
4. Queue membership and other ACBS security settings are reviewed manually.
5. Fidelity/KZone, SharePoint, RealTime/Datamart and other external access paths are handled separately when applicable.

## Automation evidence

The ACBS POC confirms read/update APIs for UserAccount and ServicingQueueUserAssignment, but not every manual control has a supported API. This repository does not invoke those write operations.

## Key integration constraint

ACBS email values can be limited to 20 characters. Therefore downstream consumers must not assume that corporate email is always a unique or lossless application identity.
