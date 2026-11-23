# SMC Tender Management System: ASP.NET Web Forms edition

Classic ASP.NET Web Forms (`.aspx` + C# code-behind) on .NET Framework 4.8, ADO.NET (`SqlConnection`, `SqlCommand`, `SqlDataAdapter`) and Microsoft SQL Server. Every action is a normal postback or page navigation.

## Layout

| Path | Purpose |
|---|---|
| `Pages/` | Web Forms pages and `Site.Master` |
| `Helpers/` | Session/permission handling, database helpers, workflow rules, application services, and secure upload handling |
| `Content/Styles.css` | Site styling |
| `SMC_TENDER_DATABASE.sql` | SQLCMD-mode database installer |
| `Migrations/20261120_TenderFormExtensions.sql` | Additive tender-form and contractor-profile schema migration |
| `Web.config`, `Global.asax`, `DepartmentModule.csproj` | Configuration and project |
| `Uploads/` | Private document storage; the app-pool identity needs write access |

## Set up

1. **Database** (SQL Server 2016 SP1+):
   `sqlcmd -S "<server>" -E -b -v DatabaseName="<db>" -v InstallMode=Fresh -v BackupVerified=NO -i "SMC_TENDER_DATABASE.sql"`
   For an existing database use `InstallMode=Upgrade -v BackupVerified=YES` only after a verified backup and a rehearsal on a restored copy.
   Existing deployments also need `Migrations/20261120_TenderFormExtensions.sql` for the dynamic financial-year/subcategory masters, expanded tender details, requirement and revision records, and contractor profile columns. The migration is additive and idempotent; back up the database before applying it with `sqlcmd -S "<server>" -d "<db>" -E -b -i "Migrations\20261120_TenderFormExtensions.sql"`.
2. **Seed data:** the installer deliberately seeds nothing. Provision roles (including the role codes used by authorization), permissions, departments, workflow stages and an administrator account (with a BCrypt password hash) before users sign in.
3. **Connection string:** edit `TenderManagement` in `Web.config` to match the installed database.
4. **Build and publish:** build `DepartmentModule.csproj` in Visual Studio 2019/2022, restore NuGet packages, then publish to IIS (Integrated pipeline, .NET CLR v4.0). Grant the app-pool identity modify rights on `Uploads/`.
5. **HTTPS:** serve the site over HTTPS. Session and authentication cookies require SSL; OTP-authenticated sessions use a signed, HTTP-only cookie and reload the active role and permissions from SQL Server.
6. **OTP/SMS:** all login and contractor-registration OTPs use the SMS gateway; there is no development OTP or fallback. Configure these environment variables for the IIS application pool: `SMC_SmsGatewayUrl` (HTTPS endpoint `https://push3.aclgateway.com/servlet/com.aclwireless.pushconnectivity.listeners.TextListener`), `SMC_SmsGatewayAppId`, `SMC_SmsGatewayUserId`, `SMC_SmsGatewayPassword`, `SMC_SmsGatewaySender`, `SMC_SmsGatewayDltTemplateId`, `SMC_SmsOtpMessageTemplate` (the provider-approved message text with `{OTP}` placeholder), and `SMC_SmsGatewaySuccessResponsePattern` (a regex matching only the success acknowledgement documented by the provider). SMS settings are read only from the process environment, never from `Web.config`. Requests use an HTTPS form-encoded POST so credentials are not placed in the URL; verify with the provider that this endpoint accepts POST requests. `OtpLifetimeMinutes` defaults to 5 and accepts values from 1 to 15; use `SMC_OtpLifetimeMinutes` to override it through the process environment. Set secrets in the server's protected environment, not in source control, and recycle the app pool after changing them. OTP requests fail closed if any required setting is missing, the response pattern is invalid/unmatched, or the gateway request fails. A gateway acknowledgement is not proof of handset delivery.

## Contractor applications

Version-2 tenders support contractor application drafts, requirement-specific document uploads, terms acceptance, submission, reviewer queues, document verification, clarification responses, decisions, notifications, and visible application history. Contractors manage their applications from `MyApplications.aspx`; internal reviewers use `Applications.aspx` and `ApplicationDetails.aspx`. Version-1 technical and financial bid behavior remains on `Bids.aspx`.

The version-2 upgrade adds scheduled notification availability and final BOQ PDF records. A shortfall's three-day deadline starts when JE approves it; an in-portal reminder becomes visible 24 hours before the deadline when the contractor next opens their application page. Generating the approved agreement also creates a versioned BOQ PDF, which contractors can download only for their own application. Apply the database script in `Upgrade` mode after a verified backup and rehearsal before deploying code that uses these schema changes; the application does not apply migrations automatically.

The installer adds tender eligibility and document requirement metadata, plus immutable application history and remarks. Uploads are stored under the configured private `UploadRoot`, validated against the tender's size and file type rules, and served through the authorized `DocumentDownload.aspx` endpoint.

## Other conversion notes

* The former stored-procedure workflows are implemented as parameterized C# and SQL transactions; the installer removes user stored procedures and validates that none remain.
* `Profile.aspx`, `Terms.aspx`, `MyApplications.aspx`, application review pages, and the associated navigation are part of the Web Forms project.
* The Next.js frontend, ASP.NET Core API, and layered .NET 8 backend are not included.
