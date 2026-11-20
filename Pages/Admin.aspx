<%@ Page Title="Admin Dashboard" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Admin.aspx.cs" Inherits="DepartmentModule.Pages.AdminDashboard" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Admin Dashboard</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading"><div><h1>JE Administration Dashboard</h1><div class="muted">Live system overview and recent activity.</div></div></div>
    <asp:Literal ID="Message" runat="server" />
    <section class="cards dashboard-statistics admin-dashboard-statistics" aria-label="System summary">
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx") %>">
            <span class="muted">Total tenders</span><strong><asp:Literal ID="TotalTenders" runat="server" /></strong>
            <span class="dashboard-stat-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M6 3h8l4 4v14H6zM14 3v5h5M9 12h6M9 16h6" /></svg></span>
        </a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?status=Draft") %>">
            <span class="muted">Draft tenders</span><strong><asp:Literal ID="DraftTenders" runat="server" /></strong>
            <span class="dashboard-stat-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M5 3h14v18H5zM8 8h8M8 12h8M8 16h5" /></svg></span>
        </a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Published") %>">
            <span class="muted">Published tenders</span><strong><asp:Literal ID="PublishedTenders" runat="server" /></strong>
            <span class="dashboard-stat-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M5 12.5 10 17l9-10" /></svg></span>
        </a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Active") %>">
            <span class="muted">Active tenders</span><strong><asp:Literal ID="ActiveTenderCount" runat="server" /></strong>
            <span class="dashboard-stat-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M12 3a9 9 0 1 0 9 9M12 7v5l3 2M16 3h5v5" /></svg></span>
        </a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Closed") %>">
            <span class="muted">Closed tenders</span><strong><asp:Literal ID="ClosedTenders" runat="server" /></strong>
            <span class="dashboard-stat-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M5 5l14 14M19 5 5 19" /></svg></span>
        </a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/AdminDepartments.aspx") %>">
            <span class="muted">Departments</span><strong><asp:Literal ID="Departments" runat="server" /></strong>
            <span class="dashboard-stat-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M3 21h18M5 21V7l7-4 7 4v14M9 10h1M14 10h1M9 14h1M14 14h1M10 21v-4h4v4" /></svg></span>
        </a>
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
