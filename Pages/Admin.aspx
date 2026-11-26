<%@ Page Title="Admin Dashboard" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Admin.aspx.cs" Inherits="DepartmentModule.Pages.AdminDashboard" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Admin Dashboard</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading"><div><h1>JE Administration Dashboard</h1><div class="muted">Live system overview and recent activity.</div></div></div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <h2>Tender overview</h2>
        <div class="cards dashboard-statistics admin-dashboard-statistics" aria-label="Tender overview">
            <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx") %>"><span class="muted">Total tenders</span><strong><asp:Literal ID="TotalTenders" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Draft") %>"><span class="muted">Draft tenders</span><strong><asp:Literal ID="DraftTenders" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Published") %>"><span class="muted">Published tenders</span><strong><asp:Literal ID="PublishedTenders" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Active") %>"><span class="muted">Active tenders</span><strong><asp:Literal ID="ActiveTenderCount" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Closed") %>"><span class="muted">Closed tenders</span><strong><asp:Literal ID="ClosedTenders" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Cancelled") %>"><span class="muted">Cancelled tenders</span><strong><asp:Literal ID="CancelledTenders" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Awarded") %>"><span class="muted">Awarded tenders</span><strong><asp:Literal ID="AwardedTenders" runat="server" /></strong></a>
        </div>
    </section>
    <section class="card">
        <h2>Contractor overview</h2>
        <div class="cards dashboard-statistics admin-dashboard-statistics" aria-label="Contractor overview">
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminUsers.aspx#contractor-profiles") %>"><span class="muted">Registered contractors</span><strong><asp:Literal ID="TotalContractors" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminUsers.aspx?contractorStatus=1#contractor-profiles") %>"><span class="muted">Active contractors</span><strong><asp:Literal ID="ActiveContractors" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminUsers.aspx?contractorStatus=0#contractor-profiles") %>"><span class="muted">Suspended / inactive contractors</span><strong><asp:Literal ID="InactiveContractors" runat="server" /></strong></a>
        </div>
    </section>
    <section class="card">
        <h2>Department workflow</h2>
        <div class="cards dashboard-statistics admin-dashboard-statistics" aria-label="Department workflow">
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=verification") %>"><span class="muted">Pending document verification</span><strong><asp:Literal ID="PendingDocumentVerification" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=shortfall") %>"><span class="muted">Shortfalls generated</span><strong><asp:Literal ID="ShortfallsGenerated" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=shortfall&stage=Final_Contractor_SubmitShortfall") %>"><span class="muted">Shortfall responses pending</span><strong><asp:Literal ID="ShortfallResponsesPending" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=approval&stage=Final_Samiti_ApproveFinancialOpening") %>"><span class="muted">Financial bid opening approvals</span><strong><asp:Literal ID="PendingFinancialOpeningApprovals" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=approval&stage=Final_Auditor_ApproveFinancialBids") %>"><span class="muted">Pending audit approvals</span><strong><asp:Literal ID="PendingAuditApprovals" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?stage=Final_JE_GenerateWorkOrder") %>"><span class="muted">Pending work orders</span><strong><asp:Literal ID="PendingWorkOrders" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?stage=Final_JE_GenerateAgreement") %>"><span class="muted">Pending tender agreements</span><strong><asp:Literal ID="PendingAgreements" runat="server" /></strong></a>
        </div>
    </section>
    <section class="card">
        <h2>Administration</h2>
        <div class="cards dashboard-statistics admin-dashboard-statistics" aria-label="Administration">
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminUsers.aspx?accountType=Internal#department-users") %>"><span class="muted">Department users</span><strong><asp:Literal ID="TotalDepartmentUsers" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminUsers.aspx?accountType=Internal&userStatus=1#department-users") %>"><span class="muted">Active department users</span><strong><asp:Literal ID="ActiveDepartmentUsers" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminRoles.aspx#role-catalog") %>"><span class="muted">System roles</span><strong><asp:Literal ID="TotalRoles" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=assigned") %>"><span class="muted">Pending administrative actions</span><strong><asp:Literal ID="PendingAdminActions" runat="server" /></strong></a>
        </div>
    </section>
    <section class="card">
        <div class="page-heading"><div><h2>Tenders by category</h2><div class="muted">Counts use the existing tender type values.</div></div></div>
        <asp:GridView ID="CategoryStatsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="No category statistics are available.">
            <Columns>
                <asp:BoundField DataField="Category" HeaderText="Category" />
                <asp:BoundField DataField="TenderCount" HeaderText="Tenders" />
            </Columns>
        </asp:GridView>
    </section>
    <section class="card">
        <div class="page-heading"><div><h2>Recent tenders</h2></div><a href="Tenders.aspx">View all tenders</a></div>
        <div class="table-wrap"><asp:GridView ID="RecentTendersGrid" runat="server" AutoGenerateColumns="false" AllowPaging="true"
            AllowCustomPaging="true" PageSize="10" DataKeyNames="TenderId" GridLines="None" EmptyDataText="No tenders found."
            OnPageIndexChanging="RecentTendersGrid_PageIndexChanging" OnRowCommand="RecentTendersGrid_RowCommand">
            <Columns>
                <asp:TemplateField HeaderText="Sr. No."><ItemTemplate><%# RecentTenderSerialNumber(Container.DataItemIndex) %></ItemTemplate></asp:TemplateField>
                <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                <asp:BoundField DataField="Title" HeaderText="Title" />
                <asp:BoundField DataField="TenderCategory" HeaderText="Category" />
                <asp:BoundField DataField="TenderType" HeaderText="Tender method" />
                <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                <asp:BoundField DataField="AssignedOfficer" HeaderText="RESP Officer" />
                <asp:BoundField DataField="PublicationDate" HeaderText="Published" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                <asp:BoundField DataField="SubmissionStartDate" HeaderText="Submission starts" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                <asp:BoundField DataField="SubmissionDeadline" HeaderText="Submission ends" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                <asp:TemplateField HeaderText="Actions"><ItemTemplate>
                    <span class="icon-actions">
                        <asp:HyperLink ID="ViewTenderLink" runat="server" CssClass="icon-action"
                            NavigateUrl='<%# ResolveUrl("~/Pages/TenderDetails.aspx?id=" + Eval("TenderId")) %>'
                            aria-label="View tender" title="View tender">
                            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z" /></svg>
                            <span class="sr-only">View</span>
                        </asp:HyperLink>
                        <asp:HyperLink ID="EditTenderLink" runat="server" CssClass="icon-action"
                            NavigateUrl='<%# ResolveUrl("~/Pages/TenderForm.aspx?id=" + Eval("TenderId")) %>'
                            Visible='<%# CanEditDraft(Eval("CanEditDraft")) %>'
                            aria-label="Edit and update draft tender" title="Edit / update draft">
                            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m4 16-.8 4.8L8 20l11.5-11.5a2.1 2.1 0 0 0-3-3L5 17M14.5 6.5l3 3" /></svg>
                            <span class="sr-only">Edit / update</span>
                        </asp:HyperLink>
                        <asp:LinkButton ID="DeleteTenderLink" runat="server" CssClass="icon-action danger"
                            CommandName="DeleteDraft" CommandArgument='<%# Eval("TenderId") %>'
                            Visible='<%# CanEditDraft(Eval("CanEditDraft")) %>'
                            aria-label="Delete draft tender" title="Delete draft"
                            OnClientClick="return confirm('Move this draft tender to deleted records? Its workflow and audit history will be retained.');">
                            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M10 11v6M14 11v6M5 7l1 14h12l1-14M9 7V4h6v3" /></svg>
                            <span class="sr-only">Delete draft</span>
                        </asp:LinkButton>
                    </span>
                </ItemTemplate></asp:TemplateField>
            </Columns>
        </asp:GridView></div>
        <div class="pagination"><asp:Literal ID="RecentTendersPageSummary" runat="server" /><span>10 tenders per page</span></div>
    </section>
    <section class="card">
        <div class="page-heading"><div><h2>Upcoming submission deadlines</h2></div><a href="Tenders.aspx">View tender list</a></div>
        <div class="table-wrap"><asp:GridView ID="UpcomingDeadlinesGrid" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="There are no upcoming submission deadlines.">
            <Columns>
                <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                <asp:BoundField DataField="Title" HeaderText="Title" />
                <asp:BoundField DataField="TenderCategory" HeaderText="Category" />
                <asp:BoundField DataField="TenderType" HeaderText="Tender method" />
                <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                <asp:BoundField DataField="AssignedOfficer" HeaderText="RESP Officer" />
                <asp:BoundField DataField="SubmissionStartDate" HeaderText="Submission starts" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                <asp:BoundField DataField="SubmissionDeadline" HeaderText="Submission ends" DataFormatString="{0:dd MMM yyyy}" />
                <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
            </Columns>
        </asp:GridView></div>
    </section>
    <section class="card">
        <h2>All tenders and recent activity</h2>
        <div class="table-wrap">
        <asp:GridView ID="DashboardFeedGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
            CssClass="admin-feed" EmptyDataText="No tenders or recent activity found.">
            <Columns>
                <asp:TemplateField HeaderText="Sr. No.">
                    <HeaderStyle CssClass="feed-serial" />
                    <ItemStyle CssClass="feed-serial" />
                    <ItemTemplate><%# FeedSerialNumber(Container.DataItemIndex) %></ItemTemplate>
                </asp:TemplateField>
                <asp:BoundField DataField="ActivityDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}"
                    HeaderStyle-CssClass="feed-date" ItemStyle-CssClass="feed-date" />
                <asp:BoundField DataField="FeedType" HeaderText="Type" />
                <asp:BoundField DataField="Area" HeaderText="Tender / area" />
                <asp:BoundField DataField="Details" HeaderText="Details" />
                <asp:BoundField DataField="Actor" HeaderText="By" />
                <asp:BoundField DataField="CurrentStatus" HeaderText="Status"
                    HeaderStyle-CssClass="feed-status" ItemStyle-CssClass="feed-status" />
                <asp:TemplateField HeaderText="Action">
                    <HeaderStyle CssClass="feed-action" />
                    <ItemStyle CssClass="feed-action" />
                    <ItemTemplate>
                        <asp:HyperLink ID="ViewTenderLink" runat="server" CssClass="button secondary tender-details-link"
                            Text="View" NavigateUrl='<%# ResolveUrl("~/Pages/TenderDetails.aspx?id=" + Eval("TenderId")) %>'
                            Visible='<%# Eval("TenderId") != DBNull.Value %>' />
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>
        </div>
        <nav class="pagination tender-pagination" aria-label="Dashboard feed pages">
            <asp:LinkButton ID="PreviousPageButton" runat="server" CssClass="button secondary pagination-control"
                Text="Previous" OnClick="PreviousPageButton_Click" />
            <div class="pagination-pages">
                <asp:Repeater ID="PageNumbers" runat="server" OnItemCommand="PageNumbers_ItemCommand">
                    <ItemTemplate>
                        <asp:LinkButton ID="PageButton" runat="server" CssClass="button secondary pagination-number"
                            Text='<%# Eval("PageNumber") %>' CommandArgument='<%# Eval("PageNumber") %>'
                            Visible='<%# !Convert.ToBoolean(Eval("IsCurrent")) %>' />
                        <asp:Label ID="CurrentPageNumber" runat="server" CssClass="pagination-current" aria-current="page"
                            Text='<%# Eval("PageNumber") %>' Visible='<%# Convert.ToBoolean(Eval("IsCurrent")) %>' />
                    </ItemTemplate>
                </asp:Repeater>
            </div>
            <asp:LinkButton ID="NextPageButton" runat="server" CssClass="button secondary pagination-control"
                Text="Next" OnClick="NextPageButton_Click" />
            <asp:Literal ID="PageSummary" runat="server" />
        </nav>
    </section>
</asp:Content>
