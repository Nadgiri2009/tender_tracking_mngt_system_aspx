<%@ Page Title="My Applications" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="MyApplications.aspx.cs" Inherits="DepartmentModule.Pages.MyApplications" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">My Applications</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1>My Applications</h1><div class="muted">Your saved drafts, submitted applications, and current bid status.</div></div>
        <div class="actions"><asp:HyperLink ID="BrowseLink" runat="server" NavigateUrl="~/Pages/Tenders.aspx" CssClass="button secondary">Browse tenders</asp:HyperLink></div>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <h2>Application notifications</h2>
        <div class="table-wrap">
            <asp:GridView ID="NotificationsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
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
            <asp:GridView ID="ApplicationsGrid" runat="server" AutoGenerateColumns="false" AllowPaging="true" PageSize="10" GridLines="None" EmptyDataText="You have not applied for any tender yet." OnPageIndexChanging="ApplicationsGrid_PageIndexChanging">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                    <asp:BoundField DataField="CurrentStatus" HeaderText="Tender stage" />
                    <asp:BoundField DataField="SubmittedDate" HeaderText="Applied on" DataFormatString="{0:dd MMM yyyy}" />
                    <asp:BoundField DataField="ApplicationStatus" HeaderText="Application status" />
                    <asp:BoundField DataField="FinancialStatus" HeaderText="Financial bid" NullDisplayText="Not submitted" />
                    <asp:TemplateField HeaderText="Application"><ItemTemplate>
                        <asp:HyperLink ID="OpenApplicationLink" runat="server" Text="View application"
                            NavigateUrl='<%# ResolveUrl("~/Pages/ApplicationDetails.aspx?id=" + Eval("TechnicalBidId")) %>'
                            Visible='<%# Convert.ToInt32(Eval("WorkflowVersion")) >= 2 %>' />
                    </ItemTemplate></asp:TemplateField>
                    <asp:HyperLinkField Text="Open bid" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="Bids.aspx?tenderId={0}" HeaderText="Action" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
