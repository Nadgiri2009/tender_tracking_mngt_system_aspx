# SMC Project & tender System: ASP.NET Web Forms edition

Classic ASP.NET Web Forms (`.aspx` + C# code-behind) on .NET Framework 4.8, ADO.NET (`SqlConnection`, `SqlCommand`, `SqlDataAdapter`) and Microsoft SQL Server. Every action is a normal postback or page navigation.

## Layout

| Path | Purpose |
|---|---|
| `Pages/` | Web Forms pages and `Site.Master` |
| `Helpers/` | Session/permission handling, database helpers, workflow rules, application services, and secure upload handling |
| `Content/Styles.css` | Site styling |
| `SMC_TENDER_DATABASE.sql` | SQLCMD-mode database installer |
| `Migrations/20261120_TenderFormExtensions.sql` | Additive tender-form and contractor-profile schema migration |
| `Migrations/20261125_TenderCreationWizard.sql` | Adds tender categories, methods, wizard state, GIS and administrative approval data |
| `Migrations/20261125_TenderGisArea.sql` | Stores selected GIS polygon coordinates and calculated site area |
| `Migrations/20261125_TenderDocumentManagement.sql` | Adds tender document metadata and a document-type ID relationship |
| `Migrations/20261126_TenderRequirementDocuments.sql` | Seeds the three tender requirement document types and converts saved wizard progress to six steps |
| `Migrations/20261127_TenderTitleCapacity.sql` | Removes fixed project-title storage limits |
| `Migrations/20261127_TenderWizardFiveSteps.sql` | Removes the separate fee step and remaps saved wizard progress to five steps |
| `Migrations/20261128_TenderGisLinearBoundary.sql` | Adds saved GIS boundary-line geometry |
| `Migrations/20261129_TenderImportantDateHistory.sql` | Records tender schedule-date changes |
| `Migrations/20261130_TenderDocumentVerification.sql` | Adds tender-document verification fields |
| `Migrations/20261201_TenderInitialDocumentVerification.sql` | Adds JE/Tender Cell verification history, document shortfalls, and workflow stages |
| `Migrations/20261202_TenderWorkOrderCompletionStages.sql` | Adds the missing JE work-order, agreement, and workflow-completion stages |
| `Migrations/20261203_TenderWorkflowSchemaCompatibility.sql` | Adds missing financial-decision/history columns and permits versioned BOQ documents |
| `Migrations/20261204_TenderWorkflowStageAlignment.sql` | Aligns every version-2 workflow stage and transition status with the current workflow UI |
| `Scripts/SeedDemoTenderData.sql`, `Scripts/ResetDemoTenderData.sql` | Safely seed and reset DEMO-PORTAL records in the local development database |
| `Web.config`, `Global.asax`, `DepartmentModule.csproj` | Configuration and project |
| `Uploads/` | Private document storage; the app-pool identity needs write access |

## Set up

1. **Database** (SQL Server 2016 SP1+):
   `sqlcmd -S "<server>" -E -b -v DatabaseName="<db>" -v InstallMode=Fresh -v BackupVerified=NO -i "SMC_TENDER_DATABASE.sql"`
   For an existing database use `InstallMode=Upgrade -v BackupVerified=YES` only after a verified backup and a rehearsal on a restored copy.
   Existing deployments also need `Migrations/20261120_TenderFormExtensions.sql` for the dynamic financial-year/subcategory masters, expanded tender details, requirement and revision records, and contractor profile columns. The migration is additive and idempotent; back up the database before applying it with `sqlcmd -S "<server>" -d "<db>" -E -b -i "Migrations\20261120_TenderFormExtensions.sql"`.
   Apply `Migrations/20261125_TenderCreationWizard.sql` after the tender-form extensions migration to add the wizard state, tender category and method masters, GIS location, and JE approval records.
   Apply `Migrations/20261125_TenderGisArea.sql` after the creation-wizard migration and before deploying cursor-drawn GIS boundaries. It adds the saved polygon coordinates and calculated area columns.
   Finishing a GIS boundary captures a 2x PNG of the visible OpenStreetMap street map with the selected boundary and centroid, then attaches it to the existing GIS image upload for normal private file storage. Map tiles must load for automatic capture; a JPEG or PNG can still be uploaded manually. This is a street-map snapshot, not satellite imagery.
   Apply `Migrations/20261125_TenderDocumentManagement.sql` in the same way before deploying document-management code. It preserves legacy document type strings and leaves unresolvable legacy IDs nullable while requiring valid document type IDs for new records.
   Apply `Migrations/20261126_TenderRequirementDocuments.sql` after the document-management migration and before deploying the six-step tender wizard. It activates the Scope of Work, Technical Requirements, and Financial Requirements document types and remaps saved wizard progress; it does not remove legacy tender financial columns.
   Apply `Migrations/20261127_TenderTitleCapacity.sql` before deploying the unbounded project-title field. It expands tender and revision title storage to `NVARCHAR(MAX)` without changing saved tender data.
   Apply `Migrations/20261127_TenderWizardFiveSteps.sql` after the requirement-document migration and before deploying the five-step wizard. It keeps existing saved progress, folds the former fee step into Basic Information, and is safe to rerun.
   Apply `Migrations/20261128_TenderGisLinearBoundary.sql`, `Migrations/20261129_TenderImportantDateHistory.sql`, and `Migrations/20261130_TenderDocumentVerification.sql` in order before deploying their corresponding GIS, date-history, and verification code. The current installer creates the migration's two history tables. Because it deliberately seeds no roles, run `Migrations/20261201_TenderInitialDocumentVerification.sql` after provisioning the canonical JE and Tender Cell roles (including on a fresh install) so the workflow stages are assigned to those roles. For an existing installation, also apply it after the earlier document migrations. It is additive and idempotent; back up and rehearse every database change before production.
   Apply `Migrations/20261202_TenderWorkOrderCompletionStages.sql` after the initial document verification migration. It fills in the JE work-order creation, agreement preparation, and final completion stages expected by the current workflow UI. It refuses to reorder those stages while one is actively assigned.
   Apply `Migrations/20261203_TenderWorkflowSchemaCompatibility.sql` before deploying the current finalized-workflow code. It adds the selected financial bid and actor-role history fields when absent and aligns generated-document constraints with the BOQ generator; it is safe to rerun.
   Apply `Migrations/20261204_TenderWorkflowStageAlignment.sql` after the workflow schema migrations. It restores the complete JE, Tender Cell, Samiti, and Auditor stage sequence expected by the current UI, and refuses to reorder stages while a non-initial version-2 assignment is active.
   The tender schedule uses date-only inputs in IST, saved as UTC values in the existing date columns. When entered, dates must follow publication, submission start, submission closing, technical bid opening, then financial bid opening; submission start and closing must be supplied together. A submission closing date remains open through the end of that IST calendar day. Times are not collected from users.
   The tender-creation form no longer collects tender document or processing fees. Existing fee values are preserved when editing a draft, and the database columns remain for existing tender details and revision history.
   New public tender numbers use `TND-YYYY-NNN` with a yearly sequence starting at `001`; the number is displayed across the portal and tender documents. The integer `TenderId` remains the internal database key for existing foreign-key relationships.
2. **Seed data:** the installer deliberately seeds nothing. Provision roles (including the role codes used by authorization), permissions, departments, workflow stages and an administrator account (with a BCrypt password hash) before users sign in.
3. **Connection string:** edit `TenderManagement` in `Web.config` to match the installed database.
4. **Build and publish:** build `DepartmentModule.csproj` in Visual Studio 2019/2022, restore NuGet packages, then publish to IIS (Integrated pipeline, .NET CLR v4.0). Grant the app-pool identity modify rights on `Uploads/`.
5. **HTTPS:** serve Production over HTTPS and set both `httpCookies requireSSL="true"` and Forms Authentication `requireSSL="true"`. Local HTTP development uses non-secure cookies so the localhost session can retain OTP state; do not deploy those local cookie settings.
6. **OTP/SMS:** select the mode with `OTP_MODE=Development` or `OTP_MODE=Production` (the local `Web.config` defaults to `Development`; the process environment variable overrides the app setting). Development OTPs are displayed only for localhost requests and are never enabled for remote clients, even if the mode is misconfigured. In localhost Development mode, the login accepts any 10-digit numeric account identifier so seeded department placeholder numbers can be used; registration and Production login continue to require a valid Indian mobile number. Production sends OTPs only through the SMS gateway and never displays the code. Configure these process environment variables for production: `OTP_MODE=Production`, `SMC_SmsGatewayUrl` (HTTPS endpoint `https://push3.aclgateway.com/servlet/com.aclwireless.pushconnectivity.listeners.TextListener`), `SMC_SmsGatewayAppId`, `SMC_SmsGatewayUserId`, `SMC_SmsGatewayPassword`, `SMC_SmsGatewaySender`, `SMC_SmsGatewayDltTemplateId`, `SMC_SmsOtpMessageTemplate` (the provider-approved message text with `{OTP}` placeholder), and `SMC_SmsGatewaySuccessResponsePattern` (a regex matching only the success acknowledgement documented by the provider). SMS credentials/settings are read only from the process environment, never from `Web.config`. Do not put secret values in source or configuration files. Protect machine-level environment settings or use an approved secret manager that injects them into the worker process. `OtpLifetimeMinutes` defaults to 5 and accepts values from 1 to 15; use `SMC_OtpLifetimeMinutes` to override it through the process environment. Recycle the app pool after changing process variables. OTP requests fail closed if any required setting is missing, the response pattern is invalid/unmatched, or the gateway request fails. A gateway acknowledgement is not proof of handset delivery.

## Demo tender presentation data

After applying the workflow migrations and verifying the local `TenderManagement` connection, run `Scripts/SeedDemoTenderData.sql` against the local `tender_tracking` database. It creates 13 clearly labeled `DEMO-PORTAL-*` tenders with role assignments, workflow history, sample documents, bidder records, dates, GIS points, approvals, and completion records. The script is insert-only for existing demo records, safe to rerun, and refuses to run on another database or a non-local SQL Server. The demo publication record is presentation data with a current publication window and supporting records; it is not an actual SMC procurement notice or an authenticated publication action.

Use `Scripts/ResetDemoTenderData.sql` to remove only records whose tender number and tracking ID carry the reserved `DEMO-PORTAL` prefix. It leaves the shared sample files and all non-demo tenders untouched, and refuses to reset if immutable audit records exist for a demo tender. The sample documents under `Uploads/Demo/` are plain-text placeholders and are explicitly marked as DEMO.

The existing staff accounts and role mappings are not changed by these scripts. Authentication remains mobile OTP; the email/password values sometimes used in presentations are not login credentials for this application. In local Development mode, request an OTP from localhost and use the code shown by the existing development flow.

## Contractor applications

Version-2 tenders support contractor application drafts, requirement-specific document uploads, terms acceptance, submission, reviewer queues, document verification, clarification responses, decisions, notifications, and visible application history. Contractors manage their applications from `MyApplications.aspx`; internal reviewers use `Applications.aspx` and `ApplicationDetails.aspx`. Version-1 technical and financial bid behavior remains on `Bids.aspx`.

The version-2 upgrade adds scheduled notification availability and final BOQ PDF records. A shortfall's response duration is configurable with the `ShortfallResponseDurationDays` app setting (default 3 calendar days; not a statutory minimum) or the `SMC_ShortfallResponseDurationDays` process environment variable (1–30 days). The deadline starts when JE approves the shortfall; an in-portal reminder becomes visible 24 hours before the deadline when the contractor next opens their application page. Generating the approved agreement also creates a versioned BOQ PDF, which contractors can download only for their own application. Apply the database script in `Upgrade` mode after a verified backup and rehearsal before deploying code that uses these schema changes; the application does not apply migrations automatically.

The installer adds tender eligibility and document requirement metadata, plus immutable application history and remarks. Uploads are stored under the configured private `UploadRoot`, validated against the tender's size and file type rules, and served through the authorized `DocumentDownload.aspx` endpoint.

## Other conversion notes

* The former stored-procedure workflows are implemented as parameterized C# and SQL transactions; the installer removes user stored procedures and validates that none remain.
* `Profile.aspx`, `Terms.aspx`, `MyApplications.aspx`, application review pages, and the associated navigation are part of the Web Forms project.
* The Next.js frontend, ASP.NET Core API, and layered .NET 8 backend are not included.
