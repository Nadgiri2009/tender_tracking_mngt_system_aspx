<%@ Page Title="My Applications" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="MyApplications.aspx.cs" Inherits="DepartmentModule.Pages.MyApplications" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">My Applications</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1>My Applications</h1><div class="muted">Tenders you have applied for and the current status of each bid.</div></div>
        <div class="actions"><asp:HyperLink ID="BrowseLink" runat="server" NavigateUrl="~/Pages/Tenders.aspx" CssClass="button secondary">Browse tenders</asp:HyperLink></div>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <div class="table-wrap">
            <asp:GridView ID="ApplicationsGrid" runat="server" AutoGenerateColumns="false" AllowPaging="true" PageSize="10" GridLines="None" EmptyDataText="You have not applied for any tender yet." OnPageIndexChanging="ApplicationsGrid_PageIndexChanging">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                    <asp:BoundField DataField="CurrentStatus" HeaderText="Tender stage" />
                    <asp:BoundField DataField="SubmittedDate" HeaderText="Applied on" DataFormatString="{0:dd MMM yyyy}" />
                    <asp:BoundField DataField="TechnicalStatus" HeaderText="Technical bid" />
                    <asp:BoundField DataField="FinancialStatus" HeaderText="Financial bid" NullDisplayText="Not submitted" />
                    <asp:HyperLinkField Text="Open bid" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="Bids.aspx?tenderId={0}" HeaderText="Action" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
