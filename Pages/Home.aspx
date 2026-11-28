<%@ Page Title="Project & tender Portal" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Home.aspx.cs" Inherits="DepartmentModule.Pages.Home" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Solapur Municipal Corporation Tender Portal</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Literal ID="Message" runat="server" />
    <section class="public-hero" aria-labelledby="portalHeroTitle">
        <div class="public-hero-copy">
            <span class="public-hero-eyebrow">Government e-Procurement</span>
            <h1 id="portalHeroTitle">Solapur Municipal Corporation Tender Portal</h1>
            <p>Browse current tender records, review official notices, and submit applications through the contractor portal.</p>
        </div>
        <div class="public-hero-actions">
            <asp:HyperLink ID="HeroPublishedLink" runat="server" CssClass="button" NavigateUrl="#published-tenders">View all tenders</asp:HyperLink>
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
    <section class="card" id="published-tenders">
        <div class="page-heading"><div><h2>All tenders</h2><p class="muted">Presentation view: all non-deleted tender records and their current statuses are listed here.</p></div></div>
        <div class="public-tender-filters" role="search" aria-label="Search and filter all tenders">
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
                    <asp:ListItem Text="All tenders" Value="" />
                    <asp:ListItem Text="Open for applications" Value="open" />
                    <asp:ListItem Text="Completed tenders" Value="completed" />
                </asp:DropDownList>
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="SortBySelect">Sort by</asp:Label>
                <asp:DropDownList ID="SortBySelect" runat="server">
                    <asp:ListItem Text="Recently added" Value="recent" />
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
        <asp:GridView ID="TendersGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="tender-list" EmptyDataText="No tenders match the selected filters. Clear filters or check again later.">
            <Columns>
                <asp:TemplateField HeaderText="Sr. No.">
                    <ItemTemplate><%# TenderSerialNumber(Container.DataItemIndex) %></ItemTemplate>
                </asp:TemplateField>
                <asp:BoundField DataField="TenderNumber" HeaderText="Tender No." />
                <asp:BoundField DataField="Title" HeaderText="Project title" />
                <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                <asp:BoundField DataField="TenderCategory" HeaderText="Category" />
                <asp:BoundField DataField="TenderType" HeaderText="Tender method" />
                <asp:BoundField DataField="CreatedDate" HeaderText="Created" DataFormatString="{0:dd MMM yyyy}" />
                <asp:BoundField DataField="PublicationDate" HeaderText="Publication date" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="Not published" />
                <asp:BoundField DataField="CurrentClosingUtc" HeaderText="Closing date and time (IST)" DataFormatString="{0:dd MMM yyyy HH:mm}" NullDisplayText="Not scheduled" />
                <asp:BoundField DataField="EstimatedAmount" HeaderText="Estimated cost" DataFormatString="{0:N2}" NullDisplayText="-" />
                <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                <asp:TemplateField HeaderText="Action">
                    <ItemTemplate>
                        <asp:HyperLink ID="ViewTenderLink" runat="server" CssClass="button secondary tender-details-link"
                            Text="View details" NavigateUrl='<%# ResolveUrl("~/Pages/TenderDetails.aspx?id=" + Eval("TenderId")) %>'
                            Visible='<%# Convert.ToBoolean(Eval("CanViewTenderDetails")) %>' />
                        <asp:HyperLink ID="ApplyTenderLink" runat="server" CssClass="button tender-details-link"
                            Text="Apply" NavigateUrl='<%# ResolveUrl("~/Pages/Bids.aspx?tenderId=" + Eval("TenderId")) %>'
                            Visible='<%# Convert.ToBoolean(Eval("IsApplicationOpen")) %>' />
                        <asp:Literal ID="ApplicationClosedText" runat="server" Text="Applications closed"
                            Visible='<%# Convert.ToBoolean(Eval("IsPublicTender")) && !Convert.ToBoolean(Eval("IsApplicationOpen")) %>' />
                        <asp:Literal ID="TenderDetailsUnavailableText" runat="server" Text="Public actions unavailable"
                            Visible='<%# !Convert.ToBoolean(Eval("CanViewTenderDetails")) %>' />
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
        <div class="page-heading"><div><h2>Notices and Announcements</h2><p class="muted">General portal reminders and official updates.</p></div></div>
        <p class="muted home-notice-intro">General portal reminders. Tender-specific notices, schedules, and corrigenda take precedence.</p>
        <div class="home-notice-list" aria-label="Tender portal reminders">
            <article class="home-notice">
                <span class="home-notice-index" aria-hidden="true">01</span>
                <div><h3>Review the complete tender notice</h3><p>Check the tender notice, schedules, BOQ, eligibility requirements, and any corrigenda before preparing an application.</p></div>
            </article>
            <article class="home-notice">
                <span class="home-notice-index" aria-hidden="true">02</span>
                <div><h3>Allow time before the closing deadline</h3><p>Complete registration and document uploads ahead of the published closing date and time.</p></div>
            </article>
            <article class="home-notice">
                <span class="home-notice-index" aria-hidden="true">03</span>
                <div><h3>Keep contractor details current</h3><p>Review your contractor profile and supporting documents before applying for a tender.</p></div>
            </article>
        </div>
        <asp:Repeater ID="OfficialAnnouncementsRepeater" runat="server">
            <HeaderTemplate><div class="home-notice-list home-official-notice-list" aria-label="Official announcements"></HeaderTemplate>
            <ItemTemplate>
                <article class="home-notice home-official-notice">
                    <span class="home-notice-index" aria-hidden="true">OFFICIAL</span>
                    <div>
                        <h3><%#: Eval("Title") %></h3>
                        <p><%#: Eval("Body") %></p>
                        <small class="home-official-dates">
                            <asp:Literal runat="server" Text='<%# FormatAnnouncementDate(Eval("PublishDate"), Eval("ExpiryDate")) %>' />
                        </small>
                    </div>
                </article>
            </ItemTemplate>
            <FooterTemplate></div></FooterTemplate>
        </asp:Repeater>
        <asp:Literal ID="NoOfficialAnnouncements" runat="server" Visible="false"
            Text="&lt;div class='home-notice-list home-official-notice-list'&gt;&lt;article class='home-notice home-official-notice home-official-empty'&gt;&lt;span class='home-notice-index' aria-hidden='true'&gt;—&lt;/span&gt;&lt;div&gt;&lt;h3&gt;No current official announcements&lt;/h3&gt;&lt;p&gt;Please check again for future portal updates.&lt;/p&gt;&lt;/div&gt;&lt;/article&gt;&lt;/div&gt;" />
    </section>
</asp:Content>
