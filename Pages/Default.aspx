<%@ Page Title="Department Dashboard" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="DepartmentModule.Pages.DepartmentDashboard" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Department Dashboard</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1><asp:Literal ID="DashboardTitle" runat="server" /></h1>
            <div class="muted"><asp:Literal ID="DashboardDescription" runat="server" /></div></div>
        <asp:HyperLink ID="NewTenderLink" runat="server" NavigateUrl="~/Pages/TenderForm.aspx" CssClass="button">New Tender</asp:HyperLink>
        <asp:HyperLink ID="WorkflowQueueLink" runat="server" NavigateUrl="~/Pages/TenderWorkflow.aspx" CssClass="button secondary" Visible="false">My workflow queue</asp:HyperLink>
        <asp:HyperLink ID="TenderCellDatesLink" runat="server" NavigateUrl="~/Pages/TenderCellDates.aspx" CssClass="button secondary" Visible="false">Manage important dates</asp:HyperLink>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <div class="page-heading"><div><h2>Tenders by department</h2><p class="muted">Counts reflect tender records visible to your account.</p></div></div>
        <div class="table-wrap">
            <asp:GridView ID="DepartmentTendersGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No department tender records are available to your account.">
                <Columns>
                    <asp:HyperLinkField DataTextField="DepartmentName" DataNavigateUrlFields="DepartmentId"
                        DataNavigateUrlFormatString="Tenders.aspx?departmentId={0}" HeaderText="Department" />
                    <asp:BoundField DataField="TenderCount" HeaderText="Tenders" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <asp:Panel ID="FinalWorkflowDashboardPanel" runat="server" Visible="false">
        <section class="cards dashboard-statistics" aria-label="Assigned workflow summary">
            <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=assigned") %>"><span class="muted"><asp:Literal ID="FinalQueueLabel" runat="server" /></span><strong><asp:Literal ID="FinalQueueCount" runat="server" /></strong></a>
            <asp:Panel ID="FinalVerificationPanel" runat="server">
                <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=verification") %>"><span class="muted"><asp:Literal ID="FinalVerificationLabel" runat="server" /></span><strong><asp:Literal ID="FinalVerificationCount" runat="server" /></strong></a>
            </asp:Panel>
            <asp:Panel ID="FinalShortfallPanel" runat="server">
                <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=shortfall") %>"><span class="muted"><asp:Literal ID="FinalShortfallLabel" runat="server" /></span><strong><asp:Literal ID="FinalShortfallCount" runat="server" /></strong></a>
            </asp:Panel>
            <asp:Panel ID="FinalApprovalPanel" runat="server">
                <a class="stat" href="<%= ResolveUrl("~/Pages/TenderWorkflow.aspx?queue=approval") %>"><span class="muted"><asp:Literal ID="FinalApprovalLabel" runat="server" /></span><strong><asp:Literal ID="FinalApprovalCount" runat="server" /></strong></a>
            </asp:Panel>
            <a class="stat" href="#FinalWorkflowHistory"><span class="muted">Completed actions</span><strong><asp:Literal ID="FinalCompletedCount" runat="server" /></strong></a>
            <a class="stat" href="#FinalWorkflowHistory"><span class="muted">Returned actions</span><strong><asp:Literal ID="FinalReturnedCount" runat="server" /></strong></a>
        </section>
        <section class="card">
            <div class="page-heading"><div><h2><asp:Literal ID="FinalQueueHeading" runat="server" /></h2><div class="muted">Tenders are limited to your role and current workflow assignment.</div></div><a href="TenderWorkflow.aspx">Open queue</a></div>
            <div class="table-wrap">
                <asp:GridView ID="FinalWorkflowGrid" runat="server" AutoGenerateColumns="false" AllowPaging="true"
                    PageSize="10" OnPageIndexChanging="FinalWorkflowGrid_PageIndexChanging"
                    GridLines="None" CssClass="compact-grid" EmptyDataText="No tenders are currently assigned to you.">
                    <Columns>
                        <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender" />
                        <asp:BoundField DataField="Title" HeaderText="Title" />
                        <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                        <asp:BoundField DataField="StageName" HeaderText="Stage" />
                        <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                        <asp:HyperLinkField Text="Open workflow" DataNavigateUrlFields="TenderId"
                            DataNavigateUrlFormatString="TenderWorkflow.aspx?id={0}" HeaderText="Action" />
                    </Columns>
                </asp:GridView>
            </div>
        </section>
        <section id="FinalWorkflowHistory" class="card">
            <div class="page-heading"><div><h2>My recent workflow actions</h2><div class="muted">Actions are loaded from your recorded workflow history.</div></div></div>
            <div class="table-wrap">
                <asp:GridView ID="FinalWorkflowHistoryGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                    EmptyDataText="No workflow actions have been recorded for your account.">
                    <Columns>
                        <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId"
                            DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender" />
                        <asp:BoundField DataField="StageName" HeaderText="Stage" />
                        <asp:BoundField DataField="ActionName" HeaderText="Action" />
                        <asp:BoundField DataField="NewStatus" HeaderText="Resulting status" />
                        <asp:BoundField DataField="ActionAtUtc" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:BoundField DataField="Remarks" HeaderText="Remarks" />
                    </Columns>
                </asp:GridView>
            </div>
        </section>
    </asp:Panel>
    <asp:Panel ID="LegacyDashboardPanel" runat="server">
    <section class="cards" aria-label="Tender summary">
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx") %>"><span class="muted">Total tenders</span><strong><asp:Literal ID="TotalTenders" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Approved") %>"><span class="muted">Approved</span><strong><asp:Literal ID="ApprovedTenders" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Pending") %>"><span class="muted">Pending</span><strong><asp:Literal ID="PendingCount" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Rejected") %>"><span class="muted">Rejected</span><strong><asp:Literal ID="RejectedTenders" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx?bucket=Under%20Review") %>"><span class="muted">Under review</span><strong><asp:Literal ID="UnderReviewTenders" runat="server" /></strong></a>
    </section>
    <asp:Panel ID="LifecycleSummaryPanel" runat="server" CssClass="cards dashboard-statistics">
        <a class="stat" href="<%= ResolveUrl("~/Pages/Approvals.aspx") %>"><span class="muted">Pending approvals</span><strong><asp:Literal ID="LifecyclePendingApprovals" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/TenderLifecycle.aspx") %>"><span class="muted">Tenders awaiting publication</span><strong><asp:Literal ID="LifecycleAwaitingPublication" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/Tenders.aspx") %>"><span class="muted">Currently published</span><strong><asp:Literal ID="LifecyclePublished" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/TenderLifecycle.aspx") %>"><span class="muted">Extension requests</span><strong><asp:Literal ID="LifecycleExtensions" runat="server" /></strong></a>
        <asp:PlaceHolder ID="LifecycleReviewSummaryPanel" runat="server">
        <a class="stat" href="<%= ResolveUrl("~/Pages/TenderEvaluation.aspx") %>"><span class="muted">Pending bid openings</span><strong><asp:Literal ID="LifecycleBidOpenings" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/TenderEvaluation.aspx") %>"><span class="muted">Awaiting evaluation</span><strong><asp:Literal ID="LifecycleAwaitingEvaluation" runat="server" /></strong></a>
        <a class="stat" href="<%= ResolveUrl("~/Pages/TenderEvaluation.aspx") %>"><span class="muted">Under evaluation</span><strong><asp:Literal ID="LifecycleUnderEvaluation" runat="server" /></strong></a>
        </asp:PlaceHolder>
    </asp:Panel>
    <asp:Panel ID="PendingApprovalsPanel" runat="server">
    <section class="card">
        <div class="page-heading"><div><h2>Pending my approval</h2><div class="muted">Tenders waiting for your role.</div></div><a href="Approvals.aspx">View queue</a></div>
        <div class="table-wrap">
        <asp:GridView ID="PendingGrid" runat="server" AutoGenerateColumns="false" CssClass="compact-grid" GridLines="None" EmptyDataText="Nothing is waiting for your approval.">
            <Columns>
                <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                <asp:BoundField DataField="Title" HeaderText="Title" />
                <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                <asp:BoundField DataField="CurrentStatus" HeaderText="Current Stage" />
            </Columns>
        </asp:GridView>
        </div>
    </section>
    </asp:Panel>
    <section class="card">
        <div class="page-heading"><div><h2><asp:Literal ID="RecentHeading" runat="server" /></h2></div><a href="Tenders.aspx">Browse tenders</a></div>
        <div class="table-wrap"><asp:GridView ID="RecentGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid" EmptyDataText="No tenders found.">
            <Columns>
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
            </Columns>
        </asp:GridView></div>
    </section>
    <section class="card">
        <div class="page-heading"><div><h2>Tenders by category</h2><div class="muted">Historical tenders without a category remain uncategorized.</div></div></div>
        <div class="table-wrap"><asp:GridView ID="CategoryStatsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid" EmptyDataText="No category statistics are available.">
            <Columns>
                <asp:BoundField DataField="Category" HeaderText="Category" />
                <asp:BoundField DataField="TenderCount" HeaderText="Tenders" />
            </Columns>
        </asp:GridView></div>
    </section>
    <section class="card">
        <div class="page-heading"><div><h2>Upcoming submission deadlines</h2></div><a href="Tenders.aspx">View tender list</a></div>
        <div class="table-wrap"><asp:GridView ID="UpcomingGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid" EmptyDataText="There are no upcoming submission deadlines.">
            <Columns>
                <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                <asp:BoundField DataField="Title" HeaderText="Title" />
                <asp:BoundField DataField="TenderCategory" HeaderText="Category" />
                <asp:BoundField DataField="TenderType" HeaderText="Tender method" />
                <asp:BoundField DataField="AssignedOfficer" HeaderText="RESP Officer" />
                <asp:BoundField DataField="SubmissionStartDate" HeaderText="Submission starts" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                <asp:BoundField DataField="SubmissionDeadline" HeaderText="Submission ends" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
            </Columns>
        </asp:GridView></div>
    </section>
    </asp:Panel>
</asp:Content>
