using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class SiteMaster : MasterPage
    {
        protected HtmlGenericControl AppLayout, AdminSidebar, PublicNavigation;
        protected PlaceHolder SidebarNavigation;
        protected HyperLink DashboardLink, PublicApprovalsLink, PublicApplicationsLink, PublicWorkflowLink,
            PublicTenderLifecycleLink, PublicTenderEvaluationLink, PublicAdminLink, LoginLink, RegisterLink,
            MyApplicationsLink, ProfileLink, TendersLink;
        protected Literal CurrentUser, PublicCurrentUser, SidebarSubtitle;
        protected LinkButton SignOutLink, PublicSignOutLink;

        protected void Page_Load(object sender, EventArgs e)
        {
            string userKey = System.Configuration.ConfigurationManager.AppSettings["SessionUserIdKey"] ?? "UserId";
            string roleKey = System.Configuration.ConfigurationManager.AppSettings["SessionRoleKey"] ?? "RoleName";
            int userId;
            bool signedIn = Session != null &&
                Int32.TryParse(Convert.ToString(Session[userKey]), out userId) &&
                userId > 0 && !String.IsNullOrWhiteSpace(Convert.ToString(Session[roleKey]));
            string role = signedIn ? Convert.ToString(Session[roleKey]) : String.Empty;
            string path = Request.AppRelativeCurrentExecutionFilePath;
            bool isLoginPage = String.Equals(path, "~/Pages/Login.aspx", StringComparison.OrdinalIgnoreCase);
            bool isRegisterPage = String.Equals(path, "~/Pages/Register.aspx", StringComparison.OrdinalIgnoreCase);
            bool isWorkspace = signedIn && !isLoginPage && !isRegisterPage;
            string roleCode = signedIn ? Convert.ToString(Session["RoleCode"]) : String.Empty;
            string accountType = signedIn ? Convert.ToString(Session["AccountType"]) : String.Empty;
            string[] permissionKeys = signedIn ? Session["PermissionKeys"] as string[] : null;
            bool isJe = String.Equals(roleCode, "JE", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(role, "Junior Engineer (JE)", StringComparison.OrdinalIgnoreCase);
            bool isContractor = String.Equals(accountType, "ExternalBidder", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(role, "Contractor", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(role, "Contractor / Vendor", StringComparison.OrdinalIgnoreCase);
            bool isSystemAdministrator = isJe || HasPermission(permissionKeys, "system.admin");

            AppLayout.Attributes["class"] = isWorkspace
                ? "app-shell workspace-layout"
                : "app-shell public-layout";
            AdminSidebar.Visible = isWorkspace;
            PublicNavigation.Visible = !isWorkspace;
            TendersLink.NavigateUrl = signedIn && !isContractor
                ? "~/Pages/Tenders.aspx"
                : "~/Pages/Home.aspx#published-tenders";

            LoginLink.Visible = !signedIn;
            RegisterLink.Visible = !signedIn && !isLoginPage;
            PublicSignOutLink.Visible = false;
            SignOutLink.Visible = isWorkspace;
            CurrentUser.Text = isWorkspace
                ? "<span class=\"nav-user\">" + HttpUtility.HtmlEncode(Convert.ToString(Session["FullName"])) + "</span>"
                : String.Empty;
            PublicCurrentUser.Text = String.Empty;

            if (isWorkspace)
            {
                SidebarSubtitle.Text = HttpUtility.HtmlEncode(role);
                BuildNavigation(role, isSystemAdministrator, isContractor, permissionKeys);
            }
        }

        private void BuildNavigation(string role, bool isSystemAdministrator, bool isContractor, string[] permissionKeys)
        {
            SidebarNavigation.Controls.Clear();
            HtmlGenericControl navigation = new HtmlGenericControl("nav");
            navigation.Attributes["class"] = "side-nav";
            navigation.Attributes["aria-label"] = "Account navigation";
            SidebarNavigation.Controls.Add(navigation);

            if (isSystemAdministrator)
            {
                BuildAdministratorNavigation(navigation);
            }
            else if (isContractor)
            {
                AddLink(navigation, "Dashboard", "~/Pages/MyApplications.aspx", "dashboard");
                HtmlGenericControl tenderGroup = AddGroup(navigation, "Tenders", IsRouteCurrent("~/Pages/Home.aspx"));
                AddLink(tenderGroup, "Browse tenders", "~/Pages/Home.aspx#published-tenders", "tender");
                AddLink(tenderGroup, "My applications", "~/Pages/MyApplications.aspx", "application");
                HtmlGenericControl accountGroup = AddGroup(navigation, "Account", IsRouteCurrent("~/Pages/Profile.aspx"));
                AddLink(accountGroup, "Company profile", "~/Pages/Profile.aspx", "profile");
                AddLink(accountGroup, "Account settings", "~/Pages/Profile.aspx", "settings");
            }
            else
            {
                BuildOfficerNavigation(navigation, role, permissionKeys);
            }

            if (!isContractor)
                AddLink(navigation, "Profile and account settings", "~/Pages/Profile.aspx", "profile");
        }

        private void BuildAdministratorNavigation(HtmlGenericControl navigation)
        {
            AddLink(navigation, "Dashboard", "~/Pages/Admin.aspx", "dashboard");

            HtmlGenericControl tenderGroup = AddGroup(navigation, "Tender management",
                IsRouteCurrent("~/Pages/Tenders.aspx") || IsRouteCurrent("~/Pages/TenderForm.aspx") ||
                IsRouteCurrent("~/Pages/TenderLifecycle.aspx") || IsRouteCurrent("~/Pages/TenderEvaluation.aspx"));
            AddLink(tenderGroup, "All tenders", "~/Pages/Tenders.aspx", "tender");
            AddLink(tenderGroup, "New tender", "~/Pages/TenderForm.aspx", "add");
            AddLink(tenderGroup, "Draft tenders", "~/Pages/Tenders.aspx?status=Draft", "draft");
            AddLink(tenderGroup, "Published tenders", "~/Pages/Tenders.aspx?bucket=Published", "published");
            AddLink(tenderGroup, "Active tenders", "~/Pages/Tenders.aspx?bucket=Active", "active");
            AddLink(tenderGroup, "Closed tenders", "~/Pages/Tenders.aspx?bucket=Closed", "closed");
            AddLink(tenderGroup, "Cancelled tenders", "~/Pages/Tenders.aspx?bucket=Cancelled", "cancelled");
            AddLink(tenderGroup, "Corrigendum / extensions", "~/Pages/TenderLifecycle.aspx", "schedule");

            HtmlGenericControl masterGroup = AddGroup(navigation, "Master management",
                IsRouteCurrent("~/Pages/AdminDepartments.aspx") || IsRouteCurrent("~/Pages/AdminMasterData.aspx"));
            AddLink(masterGroup, "Departments", "~/Pages/AdminDepartments.aspx", "department");
            AddLink(masterGroup, "Tender types and categories", "~/Pages/AdminMasterData.aspx", "master");

            HtmlGenericControl usersGroup = AddGroup(navigation, "User management",
                IsRouteCurrent("~/Pages/AdminUsers.aspx"));
            AddLink(usersGroup, "Internal users and contractors", "~/Pages/AdminUsers.aspx", "users");
            AddLink(navigation, "Roles and permissions", "~/Pages/AdminRoles.aspx", "roles");

            HtmlGenericControl workflowGroup = AddGroup(navigation, "Applications and workflow",
                IsRouteCurrent("~/Pages/Approvals.aspx") || IsRouteCurrent("~/Pages/Applications.aspx") ||
                IsRouteCurrent("~/Pages/ApplicationDetails.aspx") || IsRouteCurrent("~/Pages/TenderWorkflow.aspx"));
            AddLink(workflowGroup, "Pending approvals", "~/Pages/Approvals.aspx", "approval");
            AddLink(workflowGroup, "Contractor applications", "~/Pages/Applications.aspx", "application");
            AddLink(workflowGroup, "Assigned workflow queue", "~/Pages/TenderWorkflow.aspx", "workflow");
            AddLink(workflowGroup, "Bid opening and evaluation", "~/Pages/TenderEvaluation.aspx", "evaluation");

            HtmlGenericControl oversightGroup = AddGroup(navigation, "Reports and oversight",
                IsRouteCurrent("~/Pages/AdminReports.aspx") || IsRouteCurrent("~/Pages/AdminAudit.aspx") ||
                IsRouteCurrent("~/Pages/AdminAnnouncements.aspx"));
            AddLink(oversightGroup, "Reports and analytics", "~/Pages/AdminReports.aspx", "report");
            AddLink(oversightGroup, "Audit logs", "~/Pages/AdminAudit.aspx", "audit");
            AddLink(oversightGroup, "Announcements", "~/Pages/AdminAnnouncements.aspx", "notice");
        }

        private void BuildOfficerNavigation(HtmlGenericControl navigation, string role, string[] permissionKeys)
        {
            AddLink(navigation, "Dashboard", "~/Pages/Default.aspx", "dashboard");

            bool canViewTenders = HasAnyPermission(permissionKeys,
                "tenders.monitor", "tenders.manage.all", "tender.create.department",
                "tender.publication.manage", "tender.primary.open", "tender.primary.verify",
                "tender.cell.verify", "shortfall.create", "shortfall.approve", "shortfall.respond.own",
                "shortfall.clear", "financial.open.approve", "financial.open.execute",
                "financial.approve.final", "workorder.manage", "agreement.manage");
            bool canCreateTender = HasAnyPermission(permissionKeys, "tender.create.department", "tenders.manage.all");
            bool canOpenApprovals = WorkflowRules.PendingStatusesForRole(role).Length > 0;
            bool canOpenWorkflow = HasAnyPermission(permissionKeys,
                "tender.create.department", "tender.publication.manage", "tender.primary.open",
                "tender.primary.verify", "tender.cell.verify", "shortfall.create", "shortfall.approve",
                "shortfall.respond.own", "shortfall.clear", "financial.open.approve",
                "financial.open.execute", "financial.approve.final", "workorder.manage", "agreement.manage",
                "tenders.monitor");
            bool canOpenApplications =
                HasAnyPermission(permissionKeys, "tender.cell.verify", "tenders.monitor") ||
                String.Equals(role, "Tender Cell", StringComparison.OrdinalIgnoreCase);

            HtmlGenericControl tenderGroup = AddGroup(navigation, "Tenders",
                IsRouteCurrent("~/Pages/Tenders.aspx") || IsRouteCurrent("~/Pages/TenderForm.aspx"));
            if (canViewTenders) AddLink(tenderGroup, "Assigned / available tenders", "~/Pages/Tenders.aspx", "tender");
            if (canCreateTender) AddLink(tenderGroup, "New tender", "~/Pages/TenderForm.aspx", "add");
            if (canViewTenders)
            {
                AddLink(tenderGroup, "Approved tenders", "~/Pages/Tenders.aspx?bucket=Approved", "approved");
                AddLink(tenderGroup, "Rejected tenders", "~/Pages/Tenders.aspx?bucket=Rejected", "rejected");
            }
            if (canOpenApprovals) AddLink(tenderGroup, "Pending approvals", "~/Pages/Approvals.aspx", "approval");
            if (canOpenWorkflow) AddLink(tenderGroup, "Assigned workflow queue", "~/Pages/TenderWorkflow.aspx", "workflow");
            if (HasAnyPermission(permissionKeys, "tender.publication.manage"))
                AddLink(tenderGroup, "Publication / extensions", "~/Pages/TenderLifecycle.aspx", "schedule");
            if (HasAnyPermission(permissionKeys, "tender.primary.open", "tender.primary.verify",
                "financial.open.approve", "financial.open.execute", "financial.approve.final"))
                AddLink(tenderGroup, "Bid opening / evaluation", "~/Pages/TenderEvaluation.aspx", "evaluation");

            if (canOpenApplications)
            {
                HtmlGenericControl applicationsGroup = AddGroup(navigation, "Applications",
                    IsRouteCurrent("~/Pages/Applications.aspx") || IsRouteCurrent("~/Pages/ApplicationDetails.aspx"));
                AddLink(applicationsGroup, "Contractor applications", "~/Pages/Applications.aspx", "application");
            }

            if (HasAnyPermission(permissionKeys, "masterdata.manage"))
            {
                HtmlGenericControl reportsGroup = AddGroup(navigation, "Reports",
                    IsRouteCurrent("~/Pages/AdminReports.aspx"));
                AddLink(reportsGroup, "Reports and analytics", "~/Pages/AdminReports.aspx", "report");
            }
        }

        private HtmlGenericControl AddGroup(Control parent, string title, bool open)
        {
            HtmlGenericControl group = new HtmlGenericControl("details");
            group.Attributes["class"] = "side-nav-group";
            if (open) group.Attributes["open"] = "open";
            HtmlGenericControl summary = new HtmlGenericControl("summary");
            summary.InnerText = title;
            group.Controls.Add(summary);
            parent.Controls.Add(group);
            return group;
        }

        private void AddLink(Control parent, string label, string target, string iconName)
        {
            HtmlAnchor link = new HtmlAnchor();
            link.HRef = ResolveUrl(target);
            link.Attributes["class"] = "side-nav-link";
            if (IsLinkCurrent(target))
            {
                link.Attributes["class"] += " is-current";
                link.Attributes["aria-current"] = "page";
            }

            HtmlGenericControl icon = new HtmlGenericControl("span");
            icon.Attributes["class"] = "side-nav-icon";
            icon.Attributes["aria-hidden"] = "true";
            icon.InnerHtml = IconMarkup(iconName);
            link.Controls.Add(icon);

            HtmlGenericControl text = new HtmlGenericControl("span");
            text.InnerText = label;
            link.Controls.Add(text);
            parent.Controls.Add(link);
        }

        private bool IsLinkCurrent(string target)
        {
            string[] parts = target.Split('?');
            if (!IsRouteCurrent(parts[0])) return false;
            if (parts.Length < 2) return true;

            NameValueCollection expected = HttpUtility.ParseQueryString(parts[1]);
            foreach (string key in expected.AllKeys)
            {
                if (!String.Equals(Request.QueryString[key], expected[key], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private bool IsRouteCurrent(string target)
        {
            string route = target.Split('?')[0].Split('#')[0];
            return String.Equals(Request.AppRelativeCurrentExecutionFilePath, route,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPermission(string[] permissionKeys, string permission)
        {
            if (permissionKeys == null) return false;
            foreach (string key in permissionKeys)
                if (String.Equals(key, permission, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasAnyPermission(string[] permissionKeys, params string[] permissions)
        {
            foreach (string permission in permissions)
                if (HasPermission(permissionKeys, permission)) return true;
            return false;
        }

        private static string IconMarkup(string name)
        {
            string path;
            switch (name)
            {
                case "dashboard": path = "M3 3h8v8H3zM13 3h8v5h-8zM13 10h8v11h-8zM3 13h8v8H3z"; break;
                case "tender": path = "M6 3h8l4 4v14H6zM14 3v5h5M9 12h6M9 16h6"; break;
                case "add": path = "M12 5v14M5 12h14"; break;
                case "draft": path = "M5 3h14v18H5zM8 8h8M8 12h8M8 16h5"; break;
                case "published": path = "M5 12.5 10 17l9-10"; break;
                case "active": path = "M12 3a9 9 0 1 0 9 9M12 7v5l3 2M16 3h5v5"; break;
                case "closed": path = "M5 5l14 14M19 5 5 19"; break;
                case "cancelled": path = "M6 6l12 12M18 6 6 18"; break;
                case "schedule": path = "M4 5h16v16H4zM8 3v4M16 3v4M4 10h16"; break;
                case "department": path = "M3 21h18M5 21V7l7-4 7 4v14M9 10h1M14 10h1M9 14h1M14 14h1M10 21v-4h4v4"; break;
                case "master": path = "m12 3 9 5-9 5-9-5 9-5zM3 12l9 5 9-5M3 16l9 5 9-5"; break;
                case "users": path = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M20 8v6M23 11h-6"; break;
                case "roles": path = "M12 3 4 7v5c0 5 3.5 8 8 9 4.5-1 8-4 8-9V7zM9 12l2 2 4-4"; break;
                case "approval": path = "M5 12.5 10 17l9-10M4 4h16v16H4z"; break;
                case "application": path = "M5 3h14v18H5zM8 8h8M8 12h8M8 16h5"; break;
                case "workflow": path = "M5 5h5v5H5zM14 14h5v5h-5zM10 7h5a2 2 0 0 1 2 2v5M14 16h-5a2 2 0 0 1-2-2v-4"; break;
                case "evaluation": path = "M4 20V4M4 20h17M8 16l3-4 3 2 5-7"; break;
                case "report": path = "M4 20h16M6 17V9M11 17V4M16 17v-6"; break;
                case "audit": path = "M4 4h16v16H4zM8 8h8M8 12h8M8 16h4"; break;
                case "notice": path = "M4 10v4h3l9 5V5l-9 5zM7 14l2 6h4l-3-5M19 9a4 4 0 0 1 0 6"; break;
                case "profile": path = "M20 21a8 8 0 0 0-16 0M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8"; break;
                case "settings": path = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM19 13a7 7 0 0 0 0-2l2-1-2-4-2 1a7 7 0 0 0-2-1l-.5-2h-5L9 6a7 7 0 0 0-2 1L5 6l-2 4 2 1a7 7 0 0 0 0 2l-2 1 2 4 2-1a7 7 0 0 0 2 1l.5 2h5l.5-2a7 7 0 0 0 2-1l2 1 2-4z"; break;
                default: path = "M4 12h16M12 4v16"; break;
            }
            return "<svg viewBox=\"0 0 24 24\" focusable=\"false\"><path d=\"" + path + "\"></path></svg>";
        }

        protected void SignOutLink_Click(object sender, EventArgs e)
        {
            FormsAuthentication.SignOut();
            if (Session != null)
            {
                Session.Clear();
                Session.Abandon();
            }
            Response.Redirect(ResolveUrl("~/Pages/Home.aspx"), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
