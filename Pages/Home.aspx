<%@ Page Title="Project & tender Portal" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Home.aspx.cs" Inherits="DepartmentModule.Pages.Home" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Solapur Municipal Corporation Tender Portal</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Literal ID="Message" runat="server" />
    <section class="public-hero" aria-labelledby="portalHeroTitle">
        <div class="public-hero-copy">
            <span class="public-hero-eyebrow">Government e-Procurement</span>
            <h1 id="portalHeroTitle">Solapur Municipal Corporation Tender Portal</h1>
            <p>Discover published tenders, review official notices, and submit applications through the contractor portal.</p>
        </div>
        <div class="public-hero-actions">
            <asp:HyperLink ID="HeroPublishedLink" runat="server" CssClass="button" NavigateUrl="#published-tenders">View published tenders</asp:HyperLink>
            <asp:HyperLink ID="HeroRegisterLink" runat="server" CssClass="button secondary" NavigateUrl="~/Pages/Register.aspx">Contractor registration</asp:HyperLink>
        </div>
    </section>
    <section class="cards home-statistics" aria-label="Live portal statistics">
        <div class="stat home-stat-card">
            <span class="home-stat-icon" aria-hidden="true">01</span>
            <span><span class="muted">Total tenders</span><strong><asp:Literal ID="TotalTenders" runat="server" /></strong></span>
        </div>
        <div class="stat home-stat-card">
            <span class="home-stat-icon" aria-hidden="true">02</span>
            <span><span class="muted">Published tenders</span><strong><asp:Literal ID="PublishedTenders" runat="server" /></strong></span>
        </div>
        <div class="stat home-stat-card">
            <span class="home-stat-icon" aria-hidden="true">03</span>
            <span><span class="muted">Active tenders</span><strong><asp:Literal ID="ActiveTenders" runat="server" /></strong></span>
        </div>
        <div class="stat home-stat-card">
            <span class="home-stat-icon" aria-hidden="true">04</span>
            <span><span class="muted">Closed tenders</span><strong><asp:Literal ID="ClosedTenders" runat="server" /></strong></span>
        </div>
        <div class="stat home-stat-card">
            <span class="home-stat-icon" aria-hidden="true">05</span>
            <span><span class="muted">Registered contractors</span><strong><asp:Literal ID="TotalContractors" runat="server" /></strong></span>
        </div>
    </section>
    <asp:Panel ID="InternalTendersPanel" runat="server" Visible="false" CssClass="card">
        <div class="page-heading">
            <div>
                <h2>Internal tenders</h2>
                <p class="muted">Unpublished tender records are visible to authorized internal staff only.</p>
            </div>
        </div>
        <div class="table-wrap">
            <asp:GridView ID="InternalTendersGrid" runat="server" AutoGenerateColumns="false"
                GridLines="None" EmptyDataText="No unpublished tenders are available to your account.">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId"
                        DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                    <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                    <asp:BoundField DataField="CreatedDate" HeaderText="Created"
                        DataFormatString="{0:dd MMM yyyy}" />
                </Columns>
            </asp:GridView>
        </div>
        <asp:Literal ID="InternalTendersSummary" runat="server" />
    </asp:Panel>
    <section class="card" id="published-tenders">
        <div class="page-heading"><div><h2>Published tenders</h2><p class="muted">View tender details and check whether applications are open.</p></div></div>
        <div class="public-tender-filters" role="search" aria-label="Search and filter published tenders">
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="TenderSearchText">Search by reference or title</asp:Label>
                <asp:TextBox ID="TenderSearchText" runat="server" MaxLength="100" />
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="DepartmentFilter">Department</asp:Label>
                <asp:DropDownList ID="DepartmentFilter" runat="server" />
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="CategoryFilter">Category</asp:Label>
                <asp:DropDownList ID="CategoryFilter" runat="server" />
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="TenderTypeFilter">Tender type</asp:Label>
                <asp:DropDownList ID="TenderTypeFilter" runat="server" />
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="TenderStatusFilter">Status</asp:Label>
                <asp:DropDownList ID="TenderStatusFilter" runat="server">
                    <asp:ListItem Text="All published tenders" Value="" />
                    <asp:ListItem Text="Open for applications" Value="open" />
                    <asp:ListItem Text="Completed tenders" Value="completed" />
                </asp:DropDownList>
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="SortBySelect">Sort by</asp:Label>
                <asp:DropDownList ID="SortBySelect" runat="server">
                    <asp:ListItem Text="Recently published" Value="recent" />
                    <asp:ListItem Text="Closing soonest" Value="closing" />
                    <asp:ListItem Text="Estimated cost: high to low" Value="amount" />
                    <asp:ListItem Text="Title: A to Z" Value="title" />
                </asp:DropDownList>
            </div>
            <div class="admin-actions">
                <asp:Button ID="ApplyTenderFiltersButton" runat="server" Text="Search tenders" CausesValidation="false" OnClick="ApplyTenderFiltersButton_Click" />
                <asp:Button ID="ClearTenderFiltersButton" runat="server" Text="Clear filters" CausesValidation="false" CssClass="button secondary" OnClick="ClearTenderFiltersButton_Click" />
            </div>
        </div>
        <div class="table-wrap tender-list-wrap">
        <asp:GridView ID="TendersGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="tender-list" EmptyDataText="No published tenders match the selected filters. Clear filters or check again later.">
            <Columns>
                <asp:TemplateField HeaderText="Sr. No.">
                    <ItemTemplate><%# TenderSerialNumber(Container.DataItemIndex) %></ItemTemplate>
                </asp:TemplateField>
                <asp:BoundField DataField="TenderNumber" HeaderText="Tender No." />
                <asp:BoundField DataField="Title" HeaderText="Project title" />
                <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                <asp:BoundField DataField="TenderCategory" HeaderText="Category" />
                <asp:BoundField DataField="TenderType" HeaderText="Tender method" />
                <asp:BoundField DataField="PublicationDate" HeaderText="Publication date" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="-" />
                <asp:BoundField DataField="CurrentClosingUtc" HeaderText="Closing date and time (IST)" DataFormatString="{0:dd MMM yyyy HH:mm}" NullDisplayText="-" />
                <asp:BoundField DataField="EstimatedAmount" HeaderText="Estimated cost" DataFormatString="{0:N2}" NullDisplayText="-" />
                <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                <asp:TemplateField HeaderText="Action">
                    <ItemTemplate>
                        <asp:HyperLink ID="ViewTenderLink" runat="server" CssClass="button secondary tender-details-link"
                            Text="View details" NavigateUrl='<%# ResolveUrl("~/Pages/TenderDetails.aspx?id=" + Eval("TenderId")) %>' />
                        <asp:HyperLink ID="ApplyTenderLink" runat="server" CssClass="button tender-details-link"
                            Text="Apply" NavigateUrl='<%# ResolveUrl("~/Pages/Bids.aspx?tenderId=" + Eval("TenderId")) %>'
                            Visible='<%# Convert.ToBoolean(Eval("IsApplicationOpen")) %>' />
                        <asp:Literal ID="ApplicationClosedText" runat="server" Text="Applications closed"
                            Visible='<%# !Convert.ToBoolean(Eval("IsApplicationOpen")) %>' />
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>
        </div>
        <nav class="pagination tender-pagination" aria-label="Tender pages">
            <asp:LinkButton ID="PreviousPageButton" runat="server" CssClass="button secondary pagination-control" Text="Previous" OnClick="PreviousPageButton_Click" />
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
            <asp:LinkButton ID="NextPageButton" runat="server" CssClass="button secondary pagination-control" Text="Next" OnClick="NextPageButton_Click" />
            <asp:Literal ID="PageSummary" runat="server" />
        </nav>
    </section>
    <section class="card" id="announcements">
        <div class="page-heading"><div><h2>Notices and Announcements</h2><p class="muted">Official updates from the Project & tender system.</p></div></div>
        <div class="table-wrap">
        <asp:GridView ID="AnnouncementsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="There are no current announcements.">
            <Columns>
                <asp:BoundField DataField="Title" HeaderText="Notice" />
                <asp:BoundField DataField="Body" HeaderText="Details" />
                <asp:BoundField DataField="PublishDate" HeaderText="Published" DataFormatString="{0:dd MMM yyyy}" />
                <asp:BoundField DataField="ExpiryDate" HeaderText="Expiry" DataFormatString="{0:dd MMM yyyy}" />
            </Columns>
        </asp:GridView>
        </div>
    </section>
</asp:Content>
