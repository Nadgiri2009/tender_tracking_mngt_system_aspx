# SMC Tender Management System: ASP.NET Web Forms edition

Classic ASP.NET Web Forms (`.aspx` + C# code-behind) on .NET Framework 4.8, ADO.NET (`SqlConnection`, `SqlCommand`, `SqlDataAdapter`) and Microsoft SQL Server. Every action is a normal postback or page navigation.

## Layout

| Path | Purpose |
|---|---|
| `Pages/` | Web Forms pages and `Site.Master` |
| `Helpers/` | Session/permission handling, database helpers, workflow rules, application services, and secure upload handling |
| `Content/Styles.css` | Site styling |
| `SMC_TENDER_DATABASE.sql` | SQLCMD-mode database installer |
| `Web.config`, `Global.asax`, `DepartmentModule.csproj` | Configuration and project |
| `Uploads/` | Private document storage; the app-pool identity needs write access |

## Set up

1. **Database** (SQL Server 2016 SP1+):
   `sqlcmd -S "<server>" -E -b -v DatabaseName="<db>" -v InstallMode=Fresh -v BackupVerified=NO -i "SMC_TENDER_DATABASE.sql"`
   For an existing database use `InstallMode=Upgrade -v BackupVerified=YES` only after a verified backup and a rehearsal on a restored copy.
2. **Seed data:** the installer deliberately seeds nothing. Provision roles (including the role codes used by authorization), permissions, departments, workflow stages and an administrator account (with a BCrypt password hash) before users sign in.
3. **Connection string:** edit `TenderManagement` in `Web.config` to match the installed database.
4. **Build and publish:** build `DepartmentModule.csproj` in Visual Studio 2019/2022, restore NuGet packages, then publish to IIS (Integrated pipeline, .NET CLR v4.0). Grant the app-pool identity modify rights on `Uploads/`.
5. **HTTPS:** cookies require SSL. Serve the site over HTTPS; the `Console` OTP mode permits session and authentication cookies over HTTP only for local requests. OTP-authenticated sessions use a signed, HTTP-only cookie and reload the active role and permissions from SQL Server.
6. **OTP/SMS:** `OtpDeliveryMode` is `Console` by default. Local requests show the development OTP on the form and write it to the process user's `%TEMP%\TenderManagement-DevelopmentOtp.log`; remote requests are not given a development code. In local `Console` mode only, sign-in also accepts 10-digit placeholder identifiers for active database accounts; registration and SMS delivery still require a valid Indian mobile number. Configure the `SmsGateway*` settings for real SMS delivery.

## Contractor applications

Version-2 tenders support contractor application drafts, requirement-specific document uploads, terms acceptance, submission, reviewer queues, document verification, clarification responses, decisions, notifications, and visible application history. Contractors manage their applications from `MyApplications.aspx`; internal reviewers use `Applications.aspx` and `ApplicationDetails.aspx`. Version-1 technical and financial bid behavior remains on `Bids.aspx`.

The version-2 upgrade adds scheduled notification availability and final BOQ PDF records. A shortfall's three-day deadline starts when JE approves it; an in-portal reminder becomes visible 24 hours before the deadline when the contractor next opens their application page. Generating the approved agreement also creates a versioned BOQ PDF, which contractors can download only for their own application. Apply the database script in `Upgrade` mode after a verified backup and rehearsal before deploying code that uses these schema changes; the application does not apply migrations automatically.

The installer adds tender eligibility and document requirement metadata, plus immutable application history and remarks. Uploads are stored under the configured private `UploadRoot`, validated against the tender's size and file type rules, and served through the authorized `DocumentDownload.aspx` endpoint.

## Other conversion notes

* The former stored-procedure workflows are implemented as parameterized C# and SQL transactions; the installer removes user stored procedures and validates that none remain.
* `Profile.aspx`, `Terms.aspx`, `MyApplications.aspx`, application review pages, and the associated navigation are part of the Web Forms project.
* The Next.js frontend, ASP.NET Core API, and layered .NET 8 backend are not included.
