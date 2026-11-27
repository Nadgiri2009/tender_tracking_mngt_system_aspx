<%@ Page Title="Admin Dashboard" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Admin.aspx.cs" Inherits="DepartmentModule.Pages.AdminDashboard" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Admin Dashboard</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading"><div><h1>JE Administration Dashboard</h1><div class="muted">Department-wise tender status and system administration.</div></div></div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <h2>Tender overview</h2>
        <div class="page-heading department-tenders-heading">
            <div><h3>Department-wise tender breakdown</h3>
                <p class="muted">Select a department name or a coloured status count to view matching tenders.</p></div>
        </div>
        <asp:Repeater ID="DepartmentTendersCards" runat="server" OnItemCommand="DepartmentTendersCards_ItemCommand">
            <HeaderTemplate><div class="department-tender-cards"></HeaderTemplate>
            <ItemTemplate>
                <asp:Panel ID="DepartmentTenderCard" runat="server" CssClass="department-tender-card">
                    <span class="department-tender-card-heading">
                        <asp:LinkButton ID="ViewDepartmentTendersButton" runat="server"
                            CssClass="department-tender-department" CommandName="ViewDepartmentTenders"
                            CommandArgument='<%# Eval("DepartmentId") %>' CausesValidation="false"
                            aria-label='<%# "View all tenders for " + Eval("DepartmentName") %>'>
                            <strong><%# Eval("DepartmentName") %></strong>
                        </asp:LinkButton>
                        <span class="department-tender-total"><%# Eval("TenderCount") %> tenders</span>
                    </span>
                    <span class="department-tender-card-metrics">
                        <asp:LinkButton ID="ViewDraftTendersButton" runat="server"
                            CssClass="department-tender-metric draft" CommandName="ViewDepartmentStatus"
                            CommandArgument='<%# Eval("DepartmentId") + "|Draft" %>' CausesValidation="false"
                            aria-label='<%# "View " + Eval("DraftCount") + " draft tenders in " + Eval("DepartmentName") %>'>
                            <span>Draft</span><strong><%# Eval("DraftCount") %></strong>
                        </asp:LinkButton>
                        <asp:LinkButton ID="ViewPublishedTendersButton" runat="server"
                            CssClass="department-tender-metric published" CommandName="ViewDepartmentStatus"
                            CommandArgument='<%# Eval("DepartmentId") + "|Published" %>' CausesValidation="false"
                            aria-label='<%# "View " + Eval("PublishedCount") + " published tenders in " + Eval("DepartmentName") %>'>
                            <span>Published</span><strong><%# Eval("PublishedCount") %></strong>
                        </asp:LinkButton>
                        <asp:LinkButton ID="ViewActiveTendersButton" runat="server"
                            CssClass="department-tender-metric active" CommandName="ViewDepartmentStatus"
                            CommandArgument='<%# Eval("DepartmentId") + "|Active" %>' CausesValidation="false"
                            aria-label='<%# "View " + Eval("ActiveCount") + " active tenders in " + Eval("DepartmentName") %>'>
                            <span>Active</span><strong><%# Eval("ActiveCount") %></strong>
                        </asp:LinkButton>
                        <asp:LinkButton ID="ViewClosedTendersButton" runat="server"
                            CssClass="department-tender-metric closed" CommandName="ViewDepartmentStatus"
                            CommandArgument='<%# Eval("DepartmentId") + "|Closed" %>' CausesValidation="false"
                            aria-label='<%# "View " + Eval("ClosedCount") + " closed tenders in " + Eval("DepartmentName") %>'>
                            <span>Closed</span><strong><%# Eval("ClosedCount") %></strong>
                        </asp:LinkButton>
                    </span>
                    <span class="department-tender-card-action">Select a status or department name to view tenders</span>
                </asp:Panel>
            </ItemTemplate>
            <FooterTemplate></div></FooterTemplate>
        </asp:Repeater>
        <asp:Panel ID="DepartmentTendersEmptyPanel" runat="server" CssClass="muted" Visible="false">
            No department tender records are available.
        </asp:Panel>
        <asp:Panel ID="DepartmentTenderDetailsPanel" runat="server" CssClass="department-tender-details"
            Visible="false" aria-live="polite">
            <div class="page-heading">
                <div>
                    <h3><asp:Literal ID="SelectedDepartmentStatus" runat="server" /> tenders in
                        <asp:Literal ID="SelectedDepartmentName" runat="server" /></h3>
                    <p class="muted">Select a tender reference to open its complete details.</p>
                </div>
                <asp:LinkButton ID="ClearDepartmentSelectionButton" runat="server" CssClass="button secondary"
                    Text="Back to departments" CausesValidation="false"
                    OnClick="ClearDepartmentSelectionButton_Click" />
            </div>
            <div class="table-wrap">
                <asp:GridView ID="DepartmentTenderDetailsGrid" runat="server" AutoGenerateColumns="false"
                    GridLines="None" EmptyDataText="No tenders match the selected department and status.">
                    <Columns>
                        <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId"
                            DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender no." />
                        <asp:BoundField DataField="Title" HeaderText="Tender" />
                        <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                        <asp:BoundField DataField="CreatedDate" HeaderText="Created"
                            DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>
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
        <h2>Administration</h2>
        <div class="cards dashboard-statistics admin-dashboard-statistics" aria-label="Administration">
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminUsers.aspx?accountType=Internal#department-users") %>"><span class="muted">Department users</span><strong><asp:Literal ID="TotalDepartmentUsers" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminUsers.aspx?accountType=Internal&userStatus=1#department-users") %>"><span class="muted">Active department users</span><strong><asp:Literal ID="ActiveDepartmentUsers" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/AdminRoles.aspx#role-catalog") %>"><span class="muted">System roles</span><strong><asp:Literal ID="TotalRoles" runat="server" /></strong></a>
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=assigned") %>"><span class="muted">Pending administrative actions</span><strong><asp:Literal ID="PendingAdminActions" runat="server" /></strong></a>
        </div>
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
</asp:Content>
