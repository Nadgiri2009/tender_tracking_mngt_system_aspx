<%@ Page Title="Contractor Dashboard" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="MyApplications.aspx.cs" Inherits="DepartmentModule.Pages.MyApplications" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Contractor Dashboard</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1>Contractor Dashboard</h1><div class="muted">Your tenders, submissions, decisions, and next steps.</div></div>
        <div class="actions"><asp:HyperLink ID="BrowseLink" runat="server" NavigateUrl="~/Pages/Home.aspx#published-tenders" CssClass="button">Browse available tenders</asp:HyperLink></div>
    </div>
    <section class="cards dashboard-statistics contractor-dashboard-statistics" aria-label="Your tender summary">
        <a class="stat dashboard-stat-card" href="Home.aspx#published-tenders"><span class="stat-index">01</span><span class="muted">Available tenders</span><strong><asp:Literal ID="AvailableTendersCount" runat="server" /></strong></a>
        <a class="stat dashboard-stat-card" href="#my-applications"><span class="stat-index">02</span><span class="muted">My applied tenders</span><strong><asp:Literal ID="AppliedTendersCount" runat="server" /></strong></a>
        <a class="stat dashboard-stat-card" href="#my-applications"><span class="stat-index">03</span><span class="muted">Pending applications</span><strong><asp:Literal ID="PendingApplicationsCount" runat="server" /></strong></a>
        <a class="stat dashboard-stat-card" href="#under-evaluation"><span class="stat-index">04</span><span class="muted">Under evaluation</span><strong><asp:Literal ID="UnderEvaluationCount" runat="server" /></strong></a>
        <a class="stat dashboard-stat-card" href="#pending-actions"><span class="stat-index">05</span><span class="muted">Shortfall received</span><strong><asp:Literal ID="ShortfallReceivedCount" runat="server" /></strong></a>
        <a class="stat dashboard-stat-card" href="#pending-actions"><span class="stat-index">06</span><span class="muted">Action required</span><strong><asp:Literal ID="PendingActionsCount" runat="server" /></strong></a>
        <a class="stat dashboard-stat-card" href="#awarded-tenders"><span class="stat-index">07</span><span class="muted">Awarded tenders</span><strong><asp:Literal ID="AwardedTendersCount" runat="server" /></strong></a>
        <a class="stat dashboard-stat-card" href="#rejected-applications"><span class="stat-index">08</span><span class="muted">Rejected applications</span><strong><asp:Literal ID="RejectedApplicationsCount" runat="server" /></strong></a>
    </section>
    <asp:Literal ID="Message" runat="server" />
    <section class="card" id="pending-actions">
        <h2>Pending actions</h2>
        <p class="muted">Drafts to complete and open document clarifications for your account.</p>
        <div class="table-wrap">
            <asp:GridView ID="PendingActionsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="You have no pending contractor actions.">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TechnicalBidId"
                        DataNavigateUrlFormatString="ApplicationDetails.aspx?id={0}" HeaderText="Tender" />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="ApplicationStatus" HeaderText="Status" />
                    <asp:BoundField DataField="PendingAction" HeaderText="Next step" />
                    <asp:BoundField DataField="PendingDeadlineUtc" HeaderText="Response deadline" DataFormatString="{0:dd MMM yyyy HH:mm}" NullDisplayText="—" />
                    <asp:BoundField DataField="LatestVisibleRemark" HeaderText="Update" NullDisplayText="—" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card" id="under-evaluation">
        <h2>Under evaluation</h2>
        <div class="table-wrap">
            <asp:GridView ID="UnderEvaluationGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No applications are currently under evaluation.">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TechnicalBidId"
                        DataNavigateUrlFormatString="ApplicationDetails.aspx?id={0}" HeaderText="Tender" />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="TenderStage" HeaderText="Tender stage" />
                    <asp:BoundField DataField="ApplicationStatus" HeaderText="Application status" />
                    <asp:BoundField DataField="SubmittedDate" HeaderText="Submitted" DataFormatString="{0:dd MMM yyyy}" />
                    <asp:BoundField DataField="LatestVisibleRemark" HeaderText="Latest update" NullDisplayText="—" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card" id="my-applications">
        <h2>My applied tenders</h2>
        <p class="muted">Your saved drafts and submitted tender applications.</p>
        <div class="row application-list-filters">
            <div class="field">
                <label for="ApplicationSearchText">Search your applications</label>
                <asp:TextBox ID="ApplicationSearchText" runat="server" MaxLength="120"
                    placeholder="Reference, tender, title, department or stage" />
            </div>
            <div class="field">
                <label for="ApplicationStatusFilter">Status</label>
                <asp:DropDownList ID="ApplicationStatusFilter" runat="server">
                    <asp:ListItem Value="">All statuses</asp:ListItem>
                    <asp:ListItem Value="Draft">Draft</asp:ListItem>
                    <asp:ListItem Value="Submitted">Submitted</asp:ListItem>
                    <asp:ListItem Value="UnderVerification">Under verification</asp:ListItem>
                    <asp:ListItem Value="Shortfall">Shortfall</asp:ListItem>
                    <asp:ListItem Value="Qualified">Qualified</asp:ListItem>
                    <asp:ListItem Value="UnderEvaluation">Under evaluation</asp:ListItem>
                    <asp:ListItem Value="FinancialEvaluation">Financial evaluation</asp:ListItem>
                    <asp:ListItem Value="Disqualified">Disqualified</asp:ListItem>
                    <asp:ListItem Value="Rejected">Rejected</asp:ListItem>
                </asp:DropDownList>
            </div>
            <asp:Button ID="FilterApplicationsButton" runat="server" Text="Apply filters"
                CssClass="button" OnClick="FilterApplicationsButton_Click" />
        </div>
        <div class="table-wrap">
            <asp:GridView ID="ApplicationsGrid" runat="server" AutoGenerateColumns="false" AllowPaging="true"
                PageSize="10" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No applications match your filters." OnPageIndexChanging="ApplicationsGrid_PageIndexChanging">
                <Columns>
                    <asp:TemplateField HeaderText="Sr. No."><ItemTemplate><%# ApplicationSerialNumber(Container.DataItemIndex) %></ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="ApplicationReference" HeaderText="Application ref." />
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender No." />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                    <asp:BoundField DataField="CurrentStage" HeaderText="Current stage" />
                    <asp:BoundField DataField="SubmittedDate" HeaderText="Applied on" DataFormatString="{0:dd MMM yyyy}" />
                    <asp:BoundField DataField="ApplicationStatus" HeaderText="Application status" />
                    <asp:BoundField DataField="FinancialStatus" HeaderText="Financial bid" NullDisplayText="Not submitted" />
                    <asp:BoundField DataField="LatestVisibleRemark" HeaderText="Latest update" NullDisplayText="—" />
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
    <section class="card" id="rejected-applications">
        <h2>Rejected applications</h2>
        <div class="table-wrap">
            <asp:GridView ID="RejectedApplicationsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="You have no rejected applications.">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TechnicalBidId"
                        DataNavigateUrlFormatString="ApplicationDetails.aspx?id={0}" HeaderText="Tender" />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="ApplicationStatus" HeaderText="Status" />
                    <asp:BoundField DataField="LatestVisibleRemark" HeaderText="Decision remarks" NullDisplayText="No remarks provided" />
                    <asp:BoundField DataField="SubmittedDate" HeaderText="Submitted" DataFormatString="{0:dd MMM yyyy}" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card" id="awarded-tenders">
        <h2>Awarded tenders</h2>
        <div class="table-wrap">
            <asp:GridView ID="AwardedTendersGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No award or work order has been recorded for your contractor account.">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId"
                        DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender" />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="AwardType" HeaderText="Award record" />
                    <asp:BoundField DataField="AwardReference" HeaderText="Reference" />
                    <asp:BoundField DataField="AwardDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                    <asp:BoundField DataField="AwardAmount" HeaderText="Amount" DataFormatString="₹{0:N2}" NullDisplayText="—" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card" id="my-documents">
        <h2>My documents</h2>
        <p class="muted">Application documents and submitted shortfall responses belonging to your account.</p>
        <div class="table-wrap">
            <asp:GridView ID="DocumentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No application or shortfall documents have been uploaded.">
                <Columns>
                    <asp:TemplateField HeaderText="Sr. No."><ItemTemplate><%# Container.DataItemIndex + 1 %></ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="TenderNumber" HeaderText="Tender" />
                    <asp:BoundField DataField="DocumentType" HeaderText="Document type" />
                    <asp:BoundField DataField="DocumentKind" HeaderText="Application / response" />
                    <asp:TemplateField HeaderText="Document name"><ItemTemplate>
                        <asp:HyperLink ID="DocumentDownloadLink" runat="server" Text='<%# Eval("FileName") %>'
                            NavigateUrl='<%# ContractorDocumentDownloadUrl(Eval("DocumentKind"), Eval("DocumentId")) %>' />
                    </ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="UploadedAt" HeaderText="Uploaded" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:BoundField DataField="UploadedBy" HeaderText="Uploaded by" />
                    <asp:BoundField DataField="VerificationStatus" HeaderText="Status" NullDisplayText="Pending" />
                    <asp:BoundField DataField="Remarks" HeaderText="Reviewer / response remarks" NullDisplayText="—" />
                    <asp:TemplateField HeaderText="Action"><ItemTemplate>
                        <asp:HyperLink ID="DownloadDocumentAction" runat="server" Text="Download"
                            NavigateUrl='<%# ContractorDocumentDownloadUrl(Eval("DocumentKind"), Eval("DocumentId")) %>' />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card" id="payments-emd">
        <h2>Payments / EMD details</h2>
        <p class="muted">Tender EMD and fee amounts below are the published requirements. The current database records post-award bill payments, but does not track EMD payment receipts.</p>
        <h3>EMD and fee requirements for my applications</h3>
        <div class="table-wrap">
            <asp:GridView ID="EmdRequirementsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No tender fee requirements are linked to your applications.">
                <Columns>
                    <asp:BoundField DataField="TenderNumber" HeaderText="Tender" />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="ApplicationStatus" HeaderText="Application status" />
                    <asp:BoundField DataField="EMDAmount" HeaderText="EMD required" DataFormatString="₹{0:N2}" NullDisplayText="Not specified" />
                    <asp:BoundField DataField="TenderDocumentFee" HeaderText="Document fee" DataFormatString="₹{0:N2}" NullDisplayText="Not specified" />
                    <asp:BoundField DataField="ProcessingFee" HeaderText="Processing fee" DataFormatString="₹{0:N2}" NullDisplayText="Not specified" />
                </Columns>
            </asp:GridView>
        </div>
        <h3>Recorded bill payments</h3>
        <div class="table-wrap">
            <asp:GridView ID="PaymentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No bill payments have been recorded for your account.">
                <Columns>
                    <asp:BoundField DataField="TenderNumber" HeaderText="Tender" />
                    <asp:BoundField DataField="BillNo" HeaderText="Bill" />
                    <asp:BoundField DataField="Amount" HeaderText="Amount" DataFormatString="₹{0:N2}" />
                    <asp:BoundField DataField="TransactionNo" HeaderText="Transaction reference" />
                    <asp:BoundField DataField="PaymentDate" HeaderText="Payment date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card" id="application-notifications">
        <h2>Application notifications</h2>
        <div class="table-wrap">
            <asp:GridView ID="NotificationsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
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
</asp:Content>
