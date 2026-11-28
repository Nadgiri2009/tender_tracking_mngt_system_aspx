using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class Applications : ModulePage
    {
        protected Literal Message, ApplicationsScopeText;
        protected GridView ApplicationsGrid, NotificationsGrid;
        protected HyperLink WorkflowLink;
        protected TextBox ApplicationSearchText;
        protected DropDownList ApplicationStatusFilter;
        protected Button FilterApplicationsButton;

        protected void Page_Init(object sender, EventArgs e)
        {
            bool isJe = IsJeAdministrator;
            if (!RequireRoles("Junior Engineer (JE)", "Tender Cell")) return;
            if (!RequirePermission(isJe ? "tender.primary.verify" : "tender.cell.verify")) return;
            ApplicationsScopeText.Text = isJe
                ? "All applications across every tender, including drafts."
                : "Applications for tenders currently assigned to your role and department.";
            BindApplications();
        }

        private void BindApplications()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT b.TechnicalBidId,b.TenderId,t.TenderNumber,t.Title AS TenderTitle,c.Name AS ContractorName,
                             department.DepartmentName,
                             N'TAPP-' + CONVERT(nvarchar(4),YEAR(b.SubmittedDate)) + N'-' +
                                 RIGHT(N'000000' + CONVERT(nvarchar(20),b.TechnicalBidId),6) AS ApplicationReference,
                             COALESCE(latest.NewStatus,b.Status) AS ApplicationStatus,b.SubmittedDate,
                             COALESCE(currentStage.StageName,t.CurrentStatus) AS CurrentStage,
                             (SELECT COUNT(1) FROM dbo.TechnicalBidDocuments d
                              WHERE d.TechnicalBidId=b.TechnicalBidId) AS DocumentCount,
                             (SELECT COUNT(1) FROM dbo.TenderRequiredBidDocuments r
                              WHERE r.TenderId=t.TenderId AND r.IsMandatory=1
                                AND NOT EXISTS
                                (
                                    SELECT 1 FROM dbo.TechnicalBidDocuments d
                                    WHERE d.TechnicalBidId=b.TechnicalBidId AND d.DocumentType=r.DocumentType
                                )) AS PendingMandatoryDocumentCount
                      FROM dbo.TechnicalBid b
                      INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                      INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                      INNER JOIN dbo.Departments department ON department.DepartmentId=t.DepartmentId
                      OUTER APPLY
                      (
                          SELECT TOP (1) h.NewStatus
                          FROM dbo.ContractorApplicationHistory h
                          WHERE h.TechnicalBidId=b.TechnicalBidId AND h.NewStatus IS NOT NULL
                          ORDER BY h.HistoryId DESC
                      ) latest
                      OUTER APPLY
                      (
                          SELECT TOP (1) REPLACE(REPLACE(s.StageName,N'Final_',N''),N'_',N' ') AS StageName
                          FROM dbo.TenderWorkflowAssignments a
                          INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                            AND s.WorkflowVersion=2 AND s.IsActive=1
                          WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                          ORDER BY a.AssignmentId DESC
                      ) currentStage
                      WHERE (@IsJE=1 OR COALESCE(latest.NewStatus,b.Status)<>N'Draft')
                        AND (@ApplicationStatus=N'' OR COALESCE(latest.NewStatus,b.Status)=@ApplicationStatus)
                        AND (@SearchPattern IS NULL
                             OR t.TenderNumber LIKE @SearchPattern OR t.Title LIKE @SearchPattern
                             OR c.Name LIKE @SearchPattern OR department.DepartmentName LIKE @SearchPattern
                             OR currentStage.StageName LIKE @SearchPattern
                             OR CONVERT(nvarchar(20),b.TechnicalBidId) LIKE @SearchPattern
                             OR (N'TAPP-' + CONVERT(nvarchar(4),YEAR(b.SubmittedDate)) + N'-' +
                                 RIGHT(N'000000' + CONVERT(nvarchar(20),b.TechnicalBidId),6)) LIKE @SearchPattern)
                        AND
                        (
                            @IsJE=1
                            OR EXISTS
                            (
                                SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                  AND a.AssignedRoleId=@RoleId
                                  AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                                  AND a.DepartmentId=t.DepartmentId
                            )
                        )
                      ORDER BY b.SubmittedDate DESC,b.TechnicalBidId DESC;", connection))
                {
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@IsJE", SqlDbType.Bit, IsJeAdministrator);
                    string search = (ApplicationSearchText.Text ?? String.Empty).Trim();
                    ModuleDb.AddParameter(command, "@SearchPattern", SqlDbType.NVarChar,
                        search.Length == 0 ? null : "%" + search + "%", 240);
                    ModuleDb.AddParameter(command, "@ApplicationStatus", SqlDbType.NVarChar,
                        ApplicationStatusFilter.SelectedValue, 30);
                    DataTable data = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                    ApplicationsGrid.DataSource = data;
                    ApplicationsGrid.DataBind();
                    NotificationsGrid.DataSource = ContractorApplicationRules.GetApplicationNotifications(connection, CurrentUserId);
                    NotificationsGrid.DataBind();
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Authorized contractor applications could not be loaded.", ex);
                Message.Text = "<div class=\"message\">The application queue could not be loaded. Please retry or contact support.</div>";
            }
        }

        protected void ApplicationsGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            ApplicationsGrid.PageIndex = e.NewPageIndex;
            BindApplications();
        }

        protected void FilterApplicationsButton_Click(object sender, EventArgs e)
        {
            ApplicationsGrid.PageIndex = 0;
            BindApplications();
        }

        protected void ApplicationsGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            DataRowView row = e.Row.DataItem as DataRowView;
            if (row == null) return;
            e.Row.Cells[2].ToolTip = Convert.ToString(row["TenderNumber"]);
            e.Row.Cells[3].ToolTip = Convert.ToString(row["TenderTitle"]);
            e.Row.Cells[4].ToolTip = Convert.ToString(row["ContractorName"]);
            e.Row.Cells[5].ToolTip = Convert.ToString(row["DepartmentName"]);
            e.Row.Cells[6].ToolTip = Convert.ToString(row["CurrentStage"]);
        }

        protected int ApplicationSerialNumber(int rowIndex)
        {
            return rowIndex + 1;
        }

        protected void NotificationsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "MarkNotificationRead", StringComparison.Ordinal)) return;
            int rowIndex;
            long notificationId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), out rowIndex) ||
                rowIndex < 0 || rowIndex >= NotificationsGrid.Rows.Count ||
                !Int64.TryParse(Convert.ToString(NotificationsGrid.DataKeys[rowIndex].Value, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out notificationId))
            {
                Message.Text = "<div class=\"message\">The selected notification could not be verified. Refresh the page and try again.</div>";
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    if (!ContractorApplicationRules.MarkApplicationNotificationRead(
                        connection, notificationId, CurrentUserId))
                    {
                        Message.Text = "<div class=\"message\">The notification is no longer unread.</div>";
                    }
                }
                BindApplications();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application notification could not be marked read.", ex);
                Message.Text = "<div class=\"message\">The notification could not be updated. Please retry.</div>";
            }
        }
    }
}
