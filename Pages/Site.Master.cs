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
            bool isSystemAdministrator = isJe;

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
            SignOutLink.Text = isContractor ? "Log out" : "Sign out";
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
                HtmlGenericControl tenders = AddSection(navigation, "TENDER SERVICES");
                AddLink(tenders, "Dashboard", "~/Pages/MyApplications.aspx", "dashboard");
                AddLink(tenders, "Browse tenders", "~/Pages/Home.aspx#published-tenders", "tender");
                AddLink(tenders, "My applied tenders", "~/Pages/MyApplications.aspx#my-applications", "application");
                AddLink(tenders, "My documents", "~/Pages/MyApplications.aspx#my-documents", "documents");
                AddLink(tenders, "Payments / EMD details", "~/Pages/MyApplications.aspx#payments-emd", "payments");
                AddLink(tenders, "Awarded tenders", "~/Pages/MyApplications.aspx#awarded-tenders", "award");
                HtmlGenericControl account = AddSection(navigation, "ACCOUNT");
                AddLink(account, "Profile & account settings", "~/Pages/Profile.aspx", "profile");
                AddLink(account, "Help & support", "~/Pages/Terms.aspx#portal-contact", "support");
            }
            else
            {
                BuildOfficerNavigation(navigation, role, permissionKeys);
            }

        }

        private void BuildAdministratorNavigation(HtmlGenericControl navigation)
        {
            HtmlGenericControl tenderSection = AddSection(navigation, "PROJECT & TENDER");
            AddLink(tenderSection, "All Tenders", "~/Pages/Tenders.aspx", "tender");
            AddLink(tenderSection, "Create New Tender", "~/Pages/TenderForm.aspx", "add");

            HtmlGenericControl more = AddExpandableGroup(navigation, "More / Administration",
                IsCurrentRouteAny("~/Pages/AdminDepartments.aspx", "~/Pages/AdminMasterData.aspx",
                    "~/Pages/AdminUsers.aspx", "~/Pages/AdminRoles.aspx", "~/Pages/Approvals.aspx",
                    "~/Pages/Applications.aspx", "~/Pages/ApplicationDetails.aspx",
                    "~/Pages/TenderWorkflow.aspx", "~/Pages/TenderLifecycle.aspx",
                    "~/Pages/TenderEvaluation.aspx", "~/Pages/AdminReports.aspx",
                    "~/Pages/AdminAudit.aspx", "~/Pages/AdminAnnouncements.aspx",
                    "~/Pages/Profile.aspx"));

            HtmlGenericControl masterSection = AddSection(more, "MASTER MANAGEMENT");
            AddLink(masterSection, "Departments", "~/Pages/AdminDepartments.aspx", "department");
            AddLink(masterSection, "Designations", "~/Pages/AdminMasterData.aspx?catalog=designations", "designation");
            AddLink(masterSection, "Tender types", "~/Pages/AdminMasterData.aspx?catalog=methods", "master");
            AddLink(masterSection, "Categories", "~/Pages/AdminMasterData.aspx?catalog=categories", "master");
            AddLink(masterSection, "Subcategories", "~/Pages/AdminMasterData.aspx?catalog=subcategories", "master");
            AddLink(masterSection, "Financial years", "~/Pages/AdminMasterData.aspx?catalog=financialyears", "schedule");
            AddLink(masterSection, "Document types", "~/Pages/AdminMasterData.aspx?catalog=documents", "documents");
            AddLink(masterSection, "Units", "~/Pages/AdminMasterData.aspx?catalog=units", "master");
            AddLink(masterSection, "Users", "~/Pages/AdminUsers.aspx", "users");
            AddLink(masterSection, "Roles & permissions", "~/Pages/AdminRoles.aspx", "roles");

            HtmlGenericControl workflowSection = AddSection(more, "APPLICATIONS & WORKFLOW");
            AddLink(workflowSection, "Pending approvals", "~/Pages/Approvals.aspx", "approval");
            AddLink(workflowSection, "Contractor applications", "~/Pages/Applications.aspx", "application");
            AddLink(workflowSection, "Assigned workflow queue", "~/Pages/TenderWorkflow.aspx", "workflow");
            AddLink(workflowSection, "Corrigendum / extensions", "~/Pages/TenderLifecycle.aspx", "schedule");
            AddLink(workflowSection, "Bid opening and evaluation", "~/Pages/TenderEvaluation.aspx", "evaluation");

            HtmlGenericControl oversightSection = AddSection(more, "REPORTS & OVERSIGHT");
            AddLink(oversightSection, "Reports and analytics", "~/Pages/AdminReports.aspx", "report");
            AddLink(oversightSection, "Audit logs", "~/Pages/AdminAudit.aspx", "audit");
            AddLink(oversightSection, "Announcements", "~/Pages/AdminAnnouncements.aspx", "notice");

            HtmlGenericControl accountSection = AddSection(more, "ACCOUNT");
            AddLink(accountSection, "Profile and account settings", "~/Pages/Profile.aspx", "profile");
        }

        private void BuildOfficerNavigation(HtmlGenericControl navigation, string role, string[] permissionKeys)
        {
            bool canViewTenders = HasAnyPermission(permissionKeys,
                "tenders.monitor", "tenders.manage.all", "tender.create.department", "tender.read.department",
                "tender.publication.manage", "tender.primary.open", "tender.primary.verify",
                "tender.cell.verify", "shortfall.create", "shortfall.approve", "shortfall.respond.own",
                "shortfall.clear", "financial.open.approve", "financial.open.execute",
                "financial.approve.final", "workorder.manage", "agreement.manage");

            HtmlGenericControl tenderSection = AddSection(navigation, "PROJECT & TENDER");
            if (canViewTenders || WorkflowRules.PendingStatusesForRole(role).Length > 0)
                AddLink(tenderSection, "All Tenders", "~/Pages/Tenders.aspx", "tender");
            if (HasAnyPermission(permissionKeys, "tender.create.department"))
                AddLink(tenderSection, "Create New Tender", "~/Pages/TenderForm.aspx", "add");

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
            bool canManagePublication = HasAnyPermission(permissionKeys, "tender.publication.manage");
            bool canOpenEvaluation = HasAnyPermission(permissionKeys, "tender.primary.open", "tender.primary.verify",
                "financial.open.approve", "financial.open.execute", "financial.approve.final");

            HtmlGenericControl more = AddExpandableGroup(navigation, "More",
                IsCurrentRouteAny("~/Pages/Approvals.aspx", "~/Pages/Applications.aspx",
                    "~/Pages/ApplicationDetails.aspx", "~/Pages/TenderWorkflow.aspx",
                    "~/Pages/TenderLifecycle.aspx", "~/Pages/TenderEvaluation.aspx",
                    "~/Pages/Profile.aspx"));
            if (canOpenApprovals || canOpenWorkflow || canOpenApplications ||
                canManagePublication || canOpenEvaluation)
            {
                HtmlGenericControl workflowSection = AddSection(more, "APPLICATIONS & WORKFLOW");
                if (canOpenApprovals)
                    AddLink(workflowSection, "Pending approvals", "~/Pages/Approvals.aspx", "approval");
                if (canOpenApplications)
                    AddLink(workflowSection, "Contractor applications", "~/Pages/Applications.aspx", "application");
                if (canOpenWorkflow)
                    AddLink(workflowSection, "Assigned workflow queue", "~/Pages/TenderWorkflow.aspx", "workflow");
                if (canManagePublication)
                    AddLink(workflowSection, "Publication / extensions", "~/Pages/TenderLifecycle.aspx", "schedule");
                if (canOpenEvaluation)
                    AddLink(workflowSection, "Bid opening and evaluation", "~/Pages/TenderEvaluation.aspx", "evaluation");
            }

            HtmlGenericControl accountSection = AddSection(more, "ACCOUNT");
            AddLink(accountSection, "Profile and account settings", "~/Pages/Profile.aspx", "profile");
        }

        private HtmlGenericControl AddExpandableGroup(Control parent, string title, bool open)
        {
            HtmlGenericControl group = new HtmlGenericControl("details");
            group.Attributes["class"] = "side-nav-more";
            if (open) group.Attributes["open"] = "open";

            HtmlGenericControl summary = new HtmlGenericControl("summary");
            summary.InnerText = title;
            group.Controls.Add(summary);
            parent.Controls.Add(group);
            return group;
        }

        private bool IsCurrentRouteAny(params string[] routes)
        {
            foreach (string route in routes)
                if (IsRouteCurrent(route))
                    return true;
            return false;
        }

        private HtmlGenericControl AddSection(Control parent, string title)
        {
            HtmlGenericControl section = new HtmlGenericControl("section");
            section.Attributes["class"] = "side-nav-section";
            HtmlGenericControl heading = new HtmlGenericControl("h2");
            heading.Attributes["class"] = "side-nav-heading";
            heading.InnerText = title;
            section.Controls.Add(heading);
            parent.Controls.Add(section);
            return section;
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
                case "designation": path = "M12 3 4 7v5c0 5 3.5 8 8 9 4.5-1 8-4 8-9V7zM9 12h6M12 9v6"; break;
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
                case "documents": path = "M6 3h8l4 4v14H6zM14 3v5h5M9 12h6M9 16h6"; break;
                case "payments": path = "M3 6h18v12H3zM3 10h18M7 15h4"; break;
                case "award": path = "M8 4h8v5a4 4 0 0 1-8 0zM8 6H5a3 3 0 0 0 3 4M16 6h3a3 3 0 0 1-3 4M12 13v4M8 21h8M10 17h4"; break;
                case "support": path = "M4 13v-2a8 8 0 0 1 16 0v2M4 13h3v6H5a2 2 0 0 1-2-2v-2a2 2 0 0 1 1-2zM20 13h-3v6h2a2 2 0 0 0 2-2v-2a2 2 0 0 0-1-2z"; break;
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
