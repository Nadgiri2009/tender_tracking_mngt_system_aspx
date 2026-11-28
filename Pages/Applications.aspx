<%@ Page Title="Contractor Applications" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Applications.aspx.cs" Inherits="DepartmentModule.Pages.Applications" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Contractor Applications</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1>Contractor applications</h1><div class="muted">Applications for tenders currently assigned to your role and department.</div></div>
        <asp:HyperLink ID="WorkflowLink" runat="server" NavigateUrl="~/Pages/TenderWorkflow.aspx" CssClass="button secondary">Workflow queue</asp:HyperLink>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card application-list-filters">
        <div class="row">
            <div class="field">
                <label for="ApplicationSearchText">Search applications</label>
                <asp:TextBox ID="ApplicationSearchText" runat="server" MaxLength="120"
                    placeholder="Reference, tender, title, contractor, department or stage" />
            </div>
            <div class="field">
                <label for="ApplicationStatusFilter">Status</label>
                <asp:DropDownList ID="ApplicationStatusFilter" runat="server">
                    <asp:ListItem Value="">All statuses</asp:ListItem>
                    <asp:ListItem Value="Submitted">Submitted</asp:ListItem>
                    <asp:ListItem Value="UnderVerification">Under verification</asp:ListItem>
                    <asp:ListItem Value="Shortfall">Shortfall</asp:ListItem>
                    <asp:ListItem Value="Qualified">Qualified</asp:ListItem>
                    <asp:ListItem Value="Disqualified">Disqualified</asp:ListItem>
                </asp:DropDownList>
            </div>
            <asp:Button ID="FilterApplicationsButton" runat="server" Text="Apply filters"
                CssClass="button" OnClick="FilterApplicationsButton_Click" />
        </div>
    </section>
    <section class="card">
        <h2>Application notifications</h2>
        <div class="table-wrap">
            <asp:GridView ID="NotificationsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                CssClass="compact-grid"
                DataKeyNames="NotificationId" EmptyDataText="You have no application notifications."
                OnRowCommand="NotificationsGrid_RowCommand">
                <Columns>
                    <asp:BoundField DataField="TenderNumber" HeaderText="Tender" />
                    <asp:BoundField DataField="Message" HeaderText="Notification" />
                    <asp:BoundField DataField="CreatedAtUtc" HeaderText="Received" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:TemplateField HeaderText="Status"><ItemTemplate>
                        <%# Eval("ReadAtUtc") == DBNull.Value ? "Unread" : "Read" %>
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Action"><ItemTemplate>
                        <asp:Button ID="MarkNotificationReadButton" runat="server" Text="Mark read"
                            CommandName="MarkNotificationRead" CommandArgument='<%# Container.DataItemIndex %>'
                            Visible='<%# Eval("ReadAtUtc") == DBNull.Value %>' />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card">
        <div class="table-wrap">
            <asp:GridView ID="ApplicationsGrid" runat="server" AutoGenerateColumns="false" AllowPaging="true"
                PageSize="10" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No submitted applications match your filters in the current workflow queue."
                OnPageIndexChanging="ApplicationsGrid_PageIndexChanging" OnRowDataBound="ApplicationsGrid_RowDataBound">
                <Columns>
                    <asp:TemplateField HeaderText="Sr. No."><ItemTemplate><%# ApplicationSerialNumber(Container.DataItemIndex) %></ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="ApplicationReference" HeaderText="Application" />
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TechnicalBidId"
                        DataNavigateUrlFormatString="ApplicationDetails.aspx?id={0}" HeaderText="Tender" />
                    <asp:BoundField DataField="TenderTitle" HeaderText="Title" />
                    <asp:BoundField DataField="ContractorName" HeaderText="Contractor" />
                    <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                    <asp:BoundField DataField="CurrentStage" HeaderText="Current stage" />
                    <asp:BoundField DataField="ApplicationStatus" HeaderText="Status" />
                    <asp:BoundField DataField="DocumentCount" HeaderText="Documents" />
                    <asp:BoundField DataField="PendingMandatoryDocumentCount" HeaderText="Mandatory documents missing" />
                    <asp:BoundField DataField="SubmittedDate" HeaderText="Submitted" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:HyperLinkField Text="View application" DataNavigateUrlFields="TechnicalBidId"
                        DataNavigateUrlFormatString="ApplicationDetails.aspx?id={0}" HeaderText="Action" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
