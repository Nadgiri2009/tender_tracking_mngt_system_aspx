using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class Profile : ModulePage
    {
        protected Literal Message;
        protected TextBox FullNameText, EmailText, MobileText, RoleText;
        protected Button SaveButton;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) LoadProfile();
        }

        private void LoadProfile()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    "SELECT FullName,Email,Mobile FROM dbo.Users WHERE UserId=@UserId AND IsActive=1;", connection))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Message.Text = "<div class=\"message\">This account is no longer active.</div>";
                            SaveButton.Enabled = false;
                            return;
                        }
                        FullNameText.Text = Convert.ToString(reader["FullName"]);
                        EmailText.Text = Convert.ToString(reader["Email"]);
                        MobileText.Text = reader["Mobile"] == DBNull.Value || String.IsNullOrWhiteSpace(Convert.ToString(reader["Mobile"]))
                            ? "Not set" : Convert.ToString(reader["Mobile"]);
                    }
                }
                RoleText.Text = CurrentRole;
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Profile could not be loaded.", ex);
                Message.Text = "<div class=\"message\">Could not load your profile. Please retry or contact support.</div>";
                SaveButton.Enabled = false;
            }
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            Page.Validate("Profile");
            if (!Page.IsValid) return;
            string fullName = FullNameText.Text.Trim();
            string email = EmailText.Text.Trim();
            if (fullName.Length < 2 || fullName.Length > 150 || email.Length == 0 || email.Length > 150)
            {
                Message.Text = "<div class=\"message\">Enter a valid name and email address.</div>";
                RoleText.Text = CurrentRole;
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    if (ModuleDb.Exists(connection, transaction,
                        "SELECT COUNT(1) FROM dbo.Users WHERE Email=@Email AND UserId<>@UserId;",
                        ModuleDb.Param("@Email", SqlDbType.NVarChar, email, 150),
                        ModuleDb.Param("@UserId", SqlDbType.Int, CurrentUserId)))
                    {
                        Message.Text = "<div class=\"message\">A user with this email already exists.</div>";
                        RoleText.Text = CurrentRole;
                        return;
                    }

                    int changed = ModuleDb.NonQuery(connection, transaction,
                        @"UPDATE dbo.Users SET FullName=@FullName,Email=@Email,ModifiedBy=@UserId,ModifiedDate=SYSUTCDATETIME()
                          WHERE UserId=@UserId AND IsActive=1;",
                        ModuleDb.Param("@FullName", SqlDbType.NVarChar, fullName, 150),
                        ModuleDb.Param("@Email", SqlDbType.NVarChar, email, 150),
                        ModuleDb.Param("@UserId", SqlDbType.Int, CurrentUserId));
                    if (changed != 1)
                    {
                        Message.Text = "<div class=\"message\">This account is no longer active.</div>";
                        return;
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "UpdateProfile", "User", CurrentUserId,
                        null, Request.UserHostAddress);
                    transaction.Commit();
                }
                Session["FullName"] = fullName;
                Message.Text = "<div class=\"message success\">Profile saved.</div>";
                RoleText.Text = CurrentRole;
                LoadProfile();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Profile could not be saved.", ex);
                Message.Text = "<div class=\"message\">Could not save your profile.</div>";
                RoleText.Text = CurrentRole;
            }
        }
    }
}
