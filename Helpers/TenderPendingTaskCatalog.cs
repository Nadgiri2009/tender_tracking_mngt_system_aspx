using System;
using System.Collections.Generic;
using System.Globalization;

namespace DepartmentModule.Helpers
{
    public sealed class TenderPendingTaskInfo
    {
        public string StageName { get; private set; }
        public string Label { get; private set; }
        public string PermissionKey { get; private set; }
        public string SectionAnchor { get; private set; }

        internal TenderPendingTaskInfo(string stageName, string label,
            string permissionKey, string sectionAnchor)
        {
            StageName = stageName;
            Label = label;
            PermissionKey = permissionKey;
            SectionAnchor = sectionAnchor;
        }

        public string GetUrl(int tenderId)
        {
            string tenderIdText = tenderId.ToString(CultureInfo.InvariantCulture);
            if (!String.IsNullOrEmpty(SectionAnchor))
                return "~/Pages/TenderDetails.aspx?id=" + tenderIdText + "#" + SectionAnchor;
            return "~/Pages/TenderWorkflow.aspx?id=" + tenderIdText;
        }
    }

    public static class TenderPendingTaskCatalog
    {
        private static readonly TenderPendingTaskInfo[] Tasks =
        {
            new TenderPendingTaskInfo("Final_JE_SubmitTender", "Complete tender setup",
                "tender.create.department", null),
            new TenderPendingTaskInfo("Final_Cell_InitialTenderDocumentVerification", "Verify tender documents",
                "tender.cell.verify", "tender-documents-section"),
            new TenderPendingTaskInfo("Final_JE_CorrectTenderDocuments", "Correct tender documents",
                "tender.create.department", "tender-documents-section"),
            new TenderPendingTaskInfo("Final_JE_PublishTender", "Publish tender",
                "tender.publication.manage", null),
            new TenderPendingTaskInfo("Final_JE_OpenTechnicalBids", "Open technical bids",
                "tender.primary.open", null),
            new TenderPendingTaskInfo("Final_JE_CompletePrimaryVerification", "Complete primary verification",
                "tender.primary.verify", null),
            new TenderPendingTaskInfo("Final_JE_ForwardToTenderCell", "Forward to Tender Cell",
                "tender.primary.verify", null),
            new TenderPendingTaskInfo("Final_Cell_CompleteSecondaryVerification", "Complete Tender Cell verification",
                "tender.cell.verify", null),
            new TenderPendingTaskInfo("Final_Cell_ReverifyRejectedOpening", "Re-verify financial opening",
                "tender.cell.verify", null),
            new TenderPendingTaskInfo("Final_Cell_CreateShortfall", "Create document shortfall",
                "shortfall.create", null),
            new TenderPendingTaskInfo("Final_JE_ApproveShortfall", "Approve document shortfall",
                "shortfall.approve", null),
            new TenderPendingTaskInfo("Final_Contractor_SubmitShortfall", "Submit shortfall documents",
                "shortfall.respond.own", null),
            new TenderPendingTaskInfo("Final_Cell_ClearShortfall", "Review shortfall documents",
                "shortfall.clear", null),
            new TenderPendingTaskInfo("Final_Cell_ResumeVerification", "Resume document verification",
                "tender.cell.verify", null),
            new TenderPendingTaskInfo("Final_Samiti_ApproveFinancialOpening", "Approve financial bid opening",
                "financial.open.approve", null),
            new TenderPendingTaskInfo("Final_JE_OpenFinancialBids", "Open and enter financial bids",
                "financial.open.execute", null),
            new TenderPendingTaskInfo("Final_Auditor_ApproveFinancialBids", "Complete Chief Audit review",
                "financial.approve.final", null),
            new TenderPendingTaskInfo("Final_JE_ResubmitFinancialApproval", "Resubmit financial review",
                "financial.open.execute", null),
            new TenderPendingTaskInfo("Final_JE_StartWorkOrder", "Create work order",
                "workorder.manage", null),
            new TenderPendingTaskInfo("Final_JE_GenerateWorkOrder", "Generate work order",
                "workorder.manage", null),
            new TenderPendingTaskInfo("Final_JE_StartAgreement", "Create tender agreement",
                "agreement.manage", null),
            new TenderPendingTaskInfo("Final_JE_GenerateAgreement", "Generate tender agreement",
                "agreement.manage", null),
            new TenderPendingTaskInfo("Final_JE_CompleteTender", "Complete tender",
                "agreement.manage", null)
        };

        public static IEnumerable<TenderPendingTaskInfo> All
        {
            get { return Tasks; }
        }

        public static TenderPendingTaskInfo Find(string stageName)
        {
            foreach (TenderPendingTaskInfo task in Tasks)
                if (String.Equals(task.StageName, stageName, StringComparison.Ordinal))
                    return task;
            return null;
        }
    }
}
