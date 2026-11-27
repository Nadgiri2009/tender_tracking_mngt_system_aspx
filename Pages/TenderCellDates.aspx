<%@ Page Title="Important Tender Dates" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="TenderCellDates.aspx.cs" Inherits="DepartmentModule.Pages.TenderCellDates" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Important Tender Dates</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1>Important tender dates</h1><p class="muted">Tender Cell manages planned and publication dates for tenders in its department.</p></div>
        <a href="Default.aspx" class="button secondary">Back to dashboard</a>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <asp:Panel ID="QueuePanel" runat="server" CssClass="card">
        <h2>Department tenders</h2>
        <div class="table-wrap">
            <asp:GridView ID="TenderQueue" runat="server" AutoGenerateColumns="false" GridLines="None"
                EmptyDataText="There are no tenders in your department.">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId"
                        DataNavigateUrlFormatString="TenderCellDates.aspx?id={0}" HeaderText="Tender reference" />
                    <asp:BoundField DataField="Title" HeaderText="Project title" />
                    <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                    <asp:BoundField DataField="PublicationStartIst" HeaderText="Publication starts (IST)" NullDisplayText="Not scheduled" />
                    <asp:BoundField DataField="CurrentClosingIst" HeaderText="Publication closes (IST)" NullDisplayText="Not scheduled" />
                    <asp:BoundField DataField="ModifiedAtIst" HeaderText="Dates last updated (IST)" NullDisplayText="-" />
                </Columns>
            </asp:GridView>
        </div>
    </asp:Panel>
    <asp:Panel ID="EditPanel" runat="server" CssClass="card" Visible="false">
        <h2><asp:Literal ID="TenderHeading" runat="server" /></h2>
        <p><strong>Status:</strong> <asp:Literal ID="TenderStatus" runat="server" /></p>
        <asp:Panel ID="ReadOnlyPanel" runat="server" CssClass="message" Visible="false">
            This tender has entered publication. Its schedule is read-only here; use the existing extension workflow to change a closing date.
        </asp:Panel>
        <div class="row">
            <div class="field"><label for="PublicationDateText">Planned publication date (IST)</label><asp:TextBox ID="PublicationDateText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="SubmissionStartText">Submission start date (IST)</label><asp:TextBox ID="SubmissionStartText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="SubmissionClosingText">Submission closing date (IST)</label><asp:TextBox ID="SubmissionClosingText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="TechnicalOpeningText">Technical bid opening date (IST)</label><asp:TextBox ID="TechnicalOpeningText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="FinancialOpeningText">Financial bid opening date (IST)</label><asp:TextBox ID="FinancialOpeningText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="PreBidMeetingText">Pre-bid meeting date (IST)</label><asp:TextBox ID="PreBidMeetingText" runat="server" TextMode="Date" /></div>
        </div>
        <details class="tender-form-optional">
            <summary>Publication window and pre-bid details</summary>
            <div class="row">
                <div class="field"><label for="PublicationStartText">Publication start date and time (IST)</label><asp:TextBox ID="PublicationStartText" runat="server" TextMode="DateTimeLocal" /></div>
                <div class="field"><label for="PublicationCloseText">Publication closing date and time (IST)</label><asp:TextBox ID="PublicationCloseText" runat="server" TextMode="DateTimeLocal" /></div>
                <div class="field"><label for="PreBidLocationText">Pre-bid location</label><asp:TextBox ID="PreBidLocationText" runat="server" MaxLength="300" /></div>
            </div>
            <div class="field"><label for="PreBidDetailsText">Pre-bid meeting details</label><asp:TextBox ID="PreBidDetailsText" runat="server" TextMode="MultiLine" Rows="2" MaxLength="2000" /></div>
        </details>
        <div class="field"><label for="ScheduleRemarksText">Tender Cell remarks</label><asp:TextBox ID="ScheduleRemarksText" runat="server" TextMode="MultiLine" MaxLength="1000" Rows="3" /></div>
        <asp:Button ID="SaveDatesButton" runat="server" Text="Save important dates" OnClick="SaveDatesButton_Click" />
        <h3>Date modification history</h3>
        <div class="table-wrap">
            <asp:GridView ID="DateHistoryGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                EmptyDataText="No date changes have been recorded.">
                <Columns>
                    <asp:BoundField DataField="FieldName" HeaderText="Field" />
                    <asp:BoundField DataField="PreviousValue" HeaderText="Previous value" NullDisplayText="-" />
                    <asp:BoundField DataField="NewValue" HeaderText="New value" NullDisplayText="-" />
                    <asp:BoundField DataField="ModifiedByName" HeaderText="Changed by" />
                    <asp:BoundField DataField="ModifiedAtUtc" HeaderText="Changed at (UTC)" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                </Columns>
            </asp:GridView>
        </div>
    </asp:Panel>
</asp:Content>
