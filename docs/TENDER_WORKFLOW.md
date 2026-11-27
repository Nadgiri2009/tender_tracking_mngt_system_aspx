# SMC Tender Workflow

This document describes the workflow implemented by the portal for newly created tenders. It is intended as an operational guide and as a reference when maintaining workflow behavior.

## At a glance

```text
JE creates a draft in the five-step wizard
  -> JE submits the prepared draft from the workflow page
  -> JE publishes and sets the public bid window
  -> Contractors submit technical applications
  -> JE opens technical bids and completes primary verification
  -> JE forwards applications to Tender Cell
  -> Tender Cell completes secondary verification
       -> if corrections are required: Tender Cell creates a shortfall
          -> JE approves or returns it
          -> contractor submits corrections within three calendar days
          -> Tender Cell clears them, requests further corrections, or disqualifies an overdue application
          -> cleared items return to secondary verification
       -> when complete: Chairman approves financial bid opening
          -> if rejected: Tender Cell re-verifies, then returns it to the Chairman or creates a shortfall
  -> JE records bidder-level financial bid data
  -> Director Auditor approves or returns financial review
       -> if returned: JE resubmits for review
  -> JE creates and generates work order
  -> JE creates and generates agreement
  -> JE completes the tender after required final PDFs exist
```

New tenders are stored with `WorkflowVersion = 2`. The current assigned stage and status are kept on the tender workflow assignment, with each transition recorded in workflow history and the audit log. Users can act only when the stage is assigned to their role (and, where applicable, their user and department) and they have the required permission.

## Create and prepare the draft

The tender form has five steps: Basic Information, Important Dates, Approvals (JE-only), GIS Location, and Review & Submit. Saving as Draft or choosing **Submit for processing** saves the tender as a draft; **Submit for processing** does not itself advance the workflow. From the workflow page, JE uses the separate **Submit tender** action to move the prepared draft from `Draft` to `Submitted`.

Before that workflow submission, the portal requires at least one BOQ row, all four `TenderDocuments` categories (Tender Proposal, Tender Document, Technical Supporting Document, and Financial Supporting Document), and a bidder-document checklist. The wizard's Scope of Work, Technical Requirements, and Financial Requirements attachments are separate requirement documents; they do not replace those four categories. Publication is then scheduled in the workflow page with a future start and a later closing time.

## Detailed process

| # | Stage / status | Responsible role | What happens and the main gate |
|---|---|---|---|
| 1 | Draft — `Final_JE_SubmitTender` | Junior Engineer (JE) | JE creates and saves the tender draft. The portal assigns a tender number, saves the tender information and supporting records, and creates the initial JE workflow assignment. The creation form's **Submit for processing** action still leaves the tender in Draft. |
| 2 | Submitted — `Final_JE_PublishTender` | JE | JE uses **Submit tender** on the workflow page. This transition checks the BOQ, four required `TenderDocuments` categories, and bidder-document checklist. JE then publishes by setting a future publication start and a closing date/time after the start. The approved schedule controls public visibility and the application window. |
| 3 | Tender Published — `Final_JE_OpenTechnicalBids` | JE | During the approved publication window, contractors can view eligible tender details and create/update a draft application, upload the required documents, accept the current terms, and submit. Submitted applications are accepted only while the window is open. The owning department officer may request an extension before the current closing time and before bid opening; only one request may be pending. The JE administrator may approve or reject it. Approval updates the current closing time, and at most three extensions may be approved. |
| 4 | Primary Verification Pending — `Final_JE_CompletePrimaryVerification` | JE | After the approved closing time, JE records the technical-bid opening. The portal requires a configured bidder-document checklist and at least one submitted application. JE records a review for every submitted bidder and a decision for every uploaded or missing required document. Bid contents remain confidential until opening. |
| 5 | Primary Verification Completed — `Final_JE_ForwardToTenderCell` | JE | JE completes the primary verification only after all bidder and document decisions are recorded, then forwards the tender to Tender Cell. |
| 6 | Tender Cell Verification Pending — `Final_Cell_CompleteSecondaryVerification` | Tender Cell | Tender Cell performs secondary checks for each submitted bidder and records decisions on the relevant documents/applications. If all checks pass, the stage advances to financial-opening approval. If corrections are needed, Tender Cell sends the process to shortfall creation. |
| 7 | Shortfall Generation Pending — `Final_Cell_CreateShortfall` | Tender Cell | Tender Cell creates structured shortfall items with a document requirement, issue description, and corrective action. A shortfall is available only after a secondary review records a shortfall decision. |
| 8 | Shortfall Pending JE Approval — `Final_JE_ApproveShortfall` | JE | JE approves the requested shortfall or returns it to Tender Cell for correction. Approval starts a three-calendar-day contractor deadline; this duration is currently fixed in the service, not an administrator-configurable setting. |
| 9 | Awaiting Contractor Submission — `Final_Contractor_SubmitShortfall` | Contractor | The contractor uploads the requested corrections and submits them before the deadline. The service checks ownership and the active contractor profile. The seeded stage's role code is `TENDER_CELL`, but the assignment is bound to the contractor user and the contractor submits through the contractor service. |
| 10 | Shortfall Documents Submitted — `Final_Cell_ClearShortfall` | Tender Cell | Tender Cell reviews each submitted correction. If all items are cleared, the workflow advances to **Shortfall Cleared**. If an item remains deficient while the deadline is open, it is returned to the contractor for another submission. If the deadline expires without clearance, the application is disqualified and the tender returns to Tender Cell verification. |
| 11 | Shortfall Cleared — `Final_Cell_ResumeVerification` | Tender Cell | Tender Cell explicitly resumes secondary verification. The assignment returns to `Final_Cell_CompleteSecondaryVerification` with status `Tender Cell Verification Pending`. |
| 12 | Financial Bid Opening Approval Pending — `Final_Samiti_ApproveFinancialOpening` | Nivida Samiti Adhaksha / Chairman | After secondary verification is complete, the Chairman approves or rejects the financial bid opening. Rejection creates a `Final_Cell_ReverifyRejectedOpening` assignment for Tender Cell. After re-verification, Tender Cell either returns the review to the Chairman or routes it to shortfall creation. |
| 13 | Financial Bid Opening Approved — `Final_JE_OpenFinancialBids` | JE | JE records bidder-level financial bid amounts for eligible bidders. Only bidders approved by Tender Cell are listed for entry. |
| 14 | Final Financial Approval Pending — `Final_Auditor_ApproveFinancialBids` | Chief Auditor / Director Auditor | The auditor reviews the financial bids and records approval with the selected bid, or returns the review. The return must be followed by a JE resubmission with an explanation. |
| 15 | Final Financial Approval Completed — `Final_JE_StartWorkOrder` | JE | JE can start the work-order step only after the auditor-approved financial decision identifies an approved bid and amount. |
| 16 | Work Order Pending — `Final_JE_GenerateWorkOrder` | JE | JE supplies the work-order number, issue date, description, terms, and authorized signatory. The portal creates and stores the work order, then generates and records its PDF. |
| 17 | Work Order Generated — `Final_JE_StartAgreement` | JE | JE creates the agreement using the approved bidder and amount associated with the work order. |
| 18 | Tender Agreement Pending — `Final_JE_GenerateAgreement` | JE | JE generates and stores the agreement PDF for the created agreement. |
| 19 | Tender Agreement Generated — `Final_JE_CompleteTender` | JE | JE completes the tender only after the agreement PDF and final BOQ PDF have both been generated and stored. The terminal status is `Completed`. |

Every successful stage transition updates the tender status, closes the previous assignment, creates the next assignment, writes workflow history, and records an audit event. A rejected decision follows the stage's configured return status; users should check the current workflow queue and history rather than assuming a rejection always ends the tender.

## Contractor application requirements

- Applications are available only for a published tender during its active approved publication window.
- A contractor can maintain one application per tender; draft uploads and correction uploads are checked against the configured document type, file size, and allowed extensions.
- Submission requires all mandatory bidder documents and acceptance of the current Terms & Conditions version.
- The portal records application and document events in application history.
- Financial data in this version-2 process is entered by JE after Chairman approval; contractors do not use the legacy financial-bid submission flow for this workflow.

## Online and Offline method behavior

The tender master stores and displays the selected tender method (`Online` or `Offline`). However, the version-2 workflow stages and server-side transition logic currently do not branch on `TenderMethod`. The documented contractor application flow above is implemented through the online portal. There is no separate offline receipt/opening workflow in the current version-2 stage configuration. Do not treat selecting `Offline` as evidence that the online application controls have been bypassed; an offline-specific workflow needs to be implemented and permission-tested separately.

## Legacy workflow

Existing tenders may have `WorkflowVersion = 1`. They use the older status-based transition map in `Helpers/WorkflowRules.cs` and associated legacy tender, bid, lifecycle, and evaluation pages. That map includes requirement/estimate preparation, TS/GB/administrative/budget approvals, tender committee/publication approvals, bid evaluation and financial opinion stages, and legacy post-award work/billing/payment statuses. Do not use that legacy status list as the assigned-stage sequence for newly created version-2 tenders.

## Main portal pages

- **Tender creation and editing:** `Pages/TenderForm.aspx`
- **Assigned workflow queue and stage actions:** `Pages/TenderWorkflow.aspx`
- **Tender information, public details, and authorized document handling:** `Pages/TenderDetails.aspx`
- **Contractor bid/application entry and legacy bid operations:** `Pages/Bids.aspx`
- **Publication schedules and extension requests/reviews:** `Pages/TenderLifecycle.aspx`
- **Legacy technical evaluation:** `Pages/TenderEvaluation.aspx`

## Maintenance notes

1. Keep the version-2 stage rows in `dbo.WorkflowStages` aligned with the stage names, pending/approved/rejected statuses, and role codes seeded in `SMC_TENDER_DATABASE.sql`.
2. Keep the stage action handlers and permission checks in `Pages/TenderWorkflow.aspx.cs` and `Helpers/TenderFinalizedWorkflowService.cs` aligned with those rows.
3. Keep contractor submission gates and ownership/file checks in `Helpers/ContractorApplicationService.cs`.
4. Preserve transaction boundaries and write history/audit records whenever a workflow status or decision changes.
5. Before changing an ordering or status, check existing assignments and tender records; a status change can strand a tender if its next stage is not configured.
6. When implementing offline tenders, define the offline receipt, custody, opening, verification, confidentiality, and audit steps explicitly, then add a separate role-authorized path instead of routing offline tenders through contractor online submission.
7. The contractor shortfall stage currently uses the `TENDER_CELL` role ID with a contractor-specific assigned user. Preserve and test that service-level ownership check if changing the stage's role configuration.
