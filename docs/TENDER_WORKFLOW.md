# SMC Tender Workflow

This document describes the workflow implemented by the portal for newly created tenders. It is intended as an operational guide and as a reference when maintaining workflow behavior.

## At a glance

```text
JE creates draft
  -> JE submits tender
  -> JE publishes and sets the public bid window
  -> Contractors submit technical applications
  -> JE opens technical bids and completes primary verification
  -> JE forwards applications to Tender Cell
  -> Tender Cell completes secondary verification
       -> if corrections are required: Tender Cell creates shortfall
          -> JE approves/returns it
          -> contractor submits corrections
          -> Tender Cell clears and resumes verification
       -> when complete: Chairman approves financial bid opening
  -> JE records bidder-level financial bid data
  -> Director Auditor approves or returns financial review
       -> if returned: JE resubmits for review
  -> JE creates and generates work order
  -> JE creates and generates agreement
  -> JE completes the tender after required final PDFs exist
```

New tenders are stored with `WorkflowVersion = 2`. The current assigned stage and status are kept on the tender workflow assignment, with each transition recorded in workflow history and the audit log. Users can act only when the stage is assigned to their role (and, where applicable, their user and department) and they have the required permission.

## Detailed process

| # | Stage / status | Responsible role | What happens and the main gate |
|---|---|---|---|
| 1 | Draft — `Final_JE_SubmitTender` | Junior Engineer (JE) | JE creates and saves the tender draft. The portal assigns a tender number, saves the tender information and supporting records, and creates the initial JE workflow assignment. Before submission, the portal requires a BOQ item, tender proposal, tender document, technical supporting document, financial supporting document, and a required bidder-document checklist. |
| 2 | Submitted — `Final_JE_PublishTender` | JE | JE submits the prepared tender to workflow. JE then publishes it by setting a future publication start and closing date/time. The closing time must follow the start time. The approved publication schedule controls when the tender is publicly visible and when contractors may apply. |
| 3 | Tender Published — `Final_JE_OpenTechnicalBids` | JE | During the approved publication window, contractors can view eligible tender details and create/update a draft application, upload the required documents, accept the current terms, and submit. Submitted applications are accepted only while the window is open. An extension request can be raised by the owning department officer before opening; CAFO or an administrator can approve/reject it. At most three extensions may be approved. |
| 4 | Primary Verification Pending — `Final_JE_CompletePrimaryVerification` | JE | After the approved closing time, JE records the technical-bid opening. The portal requires a configured bidder-document checklist and at least one submitted application. JE records a review for every submitted bidder and a decision for every uploaded or missing required document. Bid contents remain confidential until opening. |
| 5 | Primary Verification Completed — `Final_JE_ForwardToTenderCell` | JE | JE completes the primary verification only after all bidder and document decisions are recorded, then forwards the tender to Tender Cell. |
| 6 | Tender Cell Verification Pending — `Final_Cell_CompleteSecondaryVerification` | Tender Cell | Tender Cell performs secondary checks for each submitted bidder and records decisions on the relevant documents/applications. If all checks pass, the stage advances to financial-opening approval. If corrections are needed, Tender Cell sends the process to shortfall creation. |
| 7 | Shortfall Generation Pending — `Final_Cell_CreateShortfall` | Tender Cell | Tender Cell creates structured shortfall items with a document requirement, issue description, and corrective action. A shortfall is available only after a secondary review records a shortfall decision. |
| 8 | Shortfall Pending JE Approval — `Final_JE_ApproveShortfall` | JE | JE approves the requested shortfall or returns it to Tender Cell for correction. An approved shortfall is sent to the contractor with a response deadline (configured to three days by default). |
| 9 | Awaiting Contractor Submission — `Final_Contractor_SubmitShortfall` | Contractor | The contractor uploads the requested corrections and submits them before the deadline. The contractor can update a draft or an application with an open clarification; the service checks ownership and the active contractor profile. |
| 10 | Shortfall Documents Submitted — `Final_Cell_ClearShortfall` | Tender Cell | Tender Cell reviews each submitted correction and clears or rejects it. A cleared shortfall returns to Tender Cell verification. A rejected or overdue response returns to the appropriate verification/shortfall handling stage. |
| 11 | Shortfall Cleared — `Final_Cell_ResumeVerification` | Tender Cell | Tender Cell explicitly resumes secondary verification after clearing the shortfall. |
| 12 | Financial Bid Opening Approval Pending — `Final_Samiti_ApproveFinancialOpening` | Nivida Samiti Adhaksha / Chairman | After secondary verification is complete, the Chairman approves or rejects the financial bid opening. Rejection returns the tender to Tender Cell for re-verification. |
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
