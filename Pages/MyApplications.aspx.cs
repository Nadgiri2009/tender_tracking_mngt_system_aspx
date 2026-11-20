using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class MyApplications : ModulePage
    {
        protected Literal Message;
        protected GridView ApplicationsGrid, NotificationsGrid;

        protected void Page_Init(object sender, EventArgs e)
        {
            if (!RequireRoles("Contractor")) return;
            BindApplications();
        }

        private void BindApplications()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT t.TenderId,t.WorkflowVersion,t.TenderNumber,t.Title,d.DepartmentName,t.CurrentStatus,
                             tb.TechnicalBidId,tb.SubmittedDate,tb.Status AS TechnicalStatus,
                             COALESCE(latest.NewStatus,tb.Status) AS ApplicationStatus,fb.Status AS FinancialStatus
                      FROM dbo.TechnicalBid tb
                      INNER JOIN dbo.Contractors c ON c.ContractorId=tb.ContractorId AND c.UserId=@UserId
                      INNER JOIN dbo.Tender t ON t.TenderId=tb.TenderId AND t.IsDeleted=0
                      INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                      LEFT JOIN dbo.FinancialBid fb ON fb.TenderId=tb.TenderId AND fb.ContractorId=tb.ContractorId
                      OUTER APPLY
                      (
                          SELECT TOP (1) h.NewStatus
                          FROM dbo.ContractorApplicationHistory h
                          WHERE h.TechnicalBidId=tb.TechnicalBidId AND h.NewStatus IS NOT NULL
                          ORDER BY h.HistoryId DESC
                      ) latest
                      ORDER BY tb.SubmittedDate DESC;", connection))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    DataTable table = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                    ApplicationsGrid.DataSource = table;
                    ApplicationsGrid.DataBind();
                    NotificationsGrid.DataSource = ContractorApplicationRules.GetApplicationNotifications(connection, CurrentUserId);
                    NotificationsGrid.DataBind();
                    if (!ContractorApplicationRules.HasScheduledNotificationAvailability(connection))
                    {
                        Message.Text = "<div class=\"message\">Scheduled shortfall reminders will appear after the database upgrade is applied.</div>";
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Applications could not be loaded.", ex);
                Message.Text = "<div class=\"message\">Your applications could not be loaded. Please retry or contact support.</div>";
            }
        }

        protected void ApplicationsGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            ApplicationsGrid.PageIndex = e.NewPageIndex;
            BindApplications();
        }

        protected int ApplicationSerialNumber(int rowIndex)
        {
            return ApplicationsGrid.PageIndex * ApplicationsGrid.PageSize + rowIndex + 1;
        }

        protected void NotificationsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "MarkNotificationRead", StringComparison.Ordinal)) return;
            int rowIndex;
            long notificationId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out rowIndex) ||
                rowIndex < 0 || rowIndex >= NotificationsGrid.Rows.Count ||
                !Int64.TryParse(Convert.ToString(NotificationsGrid.DataKeys[rowIndex].Value), out notificationId))
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
