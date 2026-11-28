<%@ Page Title="Tenders" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Tenders.aspx.cs" Inherits="DepartmentModule.Pages.Tenders" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Tenders</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1>All Tenders</h1><div class="muted">Search tenders and open each record to manage its workflow and pending work.</div></div>
        <asp:HyperLink ID="NewTenderLink" runat="server" NavigateUrl="~/Pages/TenderForm.aspx" CssClass="button">Create New Tender</asp:HyperLink>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <div class="row">
            <div class="field"><label for="TenderNumberText">Tender number</label><asp:TextBox ID="TenderNumberText" runat="server" MaxLength="50" /></div>
            <div class="field"><label for="TenderTitleText">Tender title</label><asp:TextBox ID="TenderTitleText" runat="server" MaxLength="150" /></div>
            <div class="field"><label for="StatusFilter">Status</label><asp:DropDownList ID="StatusFilter" runat="server" /></div>
            <div class="field"><label for="CategoryFilter">Category</label><asp:DropDownList ID="CategoryFilter" runat="server" /></div>
            <div class="field"><label for="DepartmentFilter">Department</label><asp:DropDownList ID="DepartmentFilter" runat="server" /></div>
            <div class="field"><label for="WorkflowStageFilter">Workflow stage</label><asp:DropDownList ID="WorkflowStageFilter" runat="server" /></div>
            <div class="field"><label for="AssignedOfficerFilter">Assigned officer</label><asp:DropDownList ID="AssignedOfficerFilter" runat="server" /></div>
            <div class="field"><label for="PendingTaskFilter">Pending tasks</label><asp:DropDownList ID="PendingTaskFilter" runat="server">
                <asp:ListItem Text="All tasks" Value="" /><asp:ListItem Text="Has pending task" Value="pending" />
                <asp:ListItem Text="No pending task" Value="none" />
            </asp:DropDownList></div>
            <div class="field"><label for="PublicationStatusFilter">Publication status</label><asp:DropDownList ID="PublicationStatusFilter" runat="server">
                <asp:ListItem Text="All publication states" Value="" /><asp:ListItem Text="Not published" Value="Not published" />
                <asp:ListItem Text="Scheduled" Value="Scheduled" /><asp:ListItem Text="Active" Value="Active" />
                <asp:ListItem Text="Closed" Value="Closed" /><asp:ListItem Text="Published" Value="Published" />
            </asp:DropDownList></div>
            <div class="field"><label for="DateFrom">Created from</label><asp:TextBox ID="DateFrom" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="DateTo">Created to</label><asp:TextBox ID="DateTo" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="DeadlineFrom">Closing date from</label><asp:TextBox ID="DeadlineFrom" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="DeadlineTo">Closing date to</label><asp:TextBox ID="DeadlineTo" runat="server" TextMode="Date" /></div>
            <asp:Button ID="FilterButton" runat="server" Text="Apply filters" CssClass="button" OnClick="FilterButton_Click" />
            <asp:Button ID="ClearFiltersButton" runat="server" Text="Clear filters" CssClass="button secondary"
                OnClick="ClearFiltersButton_Click" CausesValidation="false" />
        </div>
        <div class="table-wrap">
            <asp:GridView ID="TenderGrid" runat="server" AutoGenerateColumns="false" AllowPaging="true" AllowCustomPaging="true" AllowSorting="true"
                CssClass="compact-grid"
                PageSize="10" GridLines="None" EmptyDataText="No tenders match your filters."
                OnPageIndexChanging="TenderGrid_PageIndexChanging" OnSorting="TenderGrid_Sorting">
                <Columns>
                    <asp:TemplateField HeaderText="Sr. No."><ItemTemplate><%# TenderSerialNumber(Container.DataItemIndex) %></ItemTemplate></asp:TemplateField>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." SortExpression="TenderNumber" />
                    <asp:HyperLinkField DataTextField="Title" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Title" SortExpression="Title" />
                    <asp:BoundField DataField="TenderCategory" HeaderText="Category" />
                    <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                    <asp:BoundField DataField="CreatedDate" HeaderText="Created" DataFormatString="{0:dd MMM yyyy}" SortExpression="CreatedDate" />
                    <asp:BoundField DataField="CurrentStage" HeaderText="Workflow stage" SortExpression="CurrentStage" />
                    <asp:TemplateField HeaderText="Overall status" SortExpression="CurrentStatus">
                        <ItemTemplate><span class='<%# TenderStatusCssClass(Eval("CurrentStatus")) %>'><%#: Eval("CurrentStatus") %></span></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="PublicationStatus" HeaderText="Publication" />
                    <asp:TemplateField HeaderText="Pending task">
                        <ItemTemplate>
                            <asp:HyperLink ID="PendingTaskLink" runat="server" Text='<%# Eval("PendingTask") %>'
                                NavigateUrl='<%# Eval("PendingTaskUrl") %>'
                                Visible='<%# !String.IsNullOrEmpty(Convert.ToString(Eval("PendingTaskUrl"))) %>' />
                            <asp:Literal runat="server" Text="—"
                                Visible='<%# String.IsNullOrEmpty(Convert.ToString(Eval("PendingTaskUrl"))) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="AssignedOfficer" HeaderText="Responsible officer" SortExpression="AssignedOfficer" />
                    <asp:BoundField DataField="ModifiedDate" HeaderText="Last updated" DataFormatString="{0:dd MMM yyyy HH:mm}" NullDisplayText="-" SortExpression="ModifiedDate" />
                </Columns>
            </asp:GridView>
        </div>
        <div class="pagination"><asp:Literal ID="PageSummary" runat="server" /><span>Page size: 10</span></div>
    </section>
</asp:Content>
