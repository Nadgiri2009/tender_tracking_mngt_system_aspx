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
        protected GridView ApplicationsGrid;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RequireRoles("Contractor")) return;
            if (!IsPostBack) BindApplications();
        }

        private void BindApplications()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT t.TenderId,t.TenderNumber,t.Title,d.DepartmentName,t.CurrentStatus,
                             tb.SubmittedDate,tb.Status AS TechnicalStatus,fb.Status AS FinancialStatus
                      FROM dbo.TechnicalBid tb
                      INNER JOIN dbo.Contractors c ON c.ContractorId=tb.ContractorId AND c.UserId=@UserId
                      INNER JOIN dbo.Tender t ON t.TenderId=tb.TenderId AND t.IsDeleted=0
                      INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                      LEFT JOIN dbo.FinancialBid fb ON fb.TenderId=tb.TenderId AND fb.ContractorId=tb.ContractorId
                      ORDER BY tb.SubmittedDate DESC;", connection))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    DataTable table = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                    ApplicationsGrid.DataSource = table;
                    ApplicationsGrid.DataBind();
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
    }
}
