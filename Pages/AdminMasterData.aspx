<%@ Page Title="Master Data" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="AdminMasterData.aspx.cs" Inherits="DepartmentModule.Pages.AdminMasterData" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Master Data</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1>Master data</h1><div class="muted">Manage tender categories, subcategories, methods, financial years, document types, units, and officer designations.</div></div>
        <a class="button secondary" href="Admin.aspx">Back to administration</a>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <div class="field">
            <asp:Label runat="server" AssociatedControlID="CatalogSelect">Catalog</asp:Label>
            <asp:DropDownList ID="CatalogSelect" runat="server" AutoPostBack="true" OnSelectedIndexChanged="CatalogSelect_SelectedIndexChanged" />
        </div>
        <asp:HiddenField ID="OriginalValue" runat="server" />
        <asp:Panel ID="ParentCategoryPanel" runat="server" CssClass="field" Visible="false">
            <asp:Label runat="server" AssociatedControlID="ParentCategorySelect">Tender category</asp:Label>
            <asp:DropDownList ID="ParentCategorySelect" runat="server" />
        </asp:Panel>
        <asp:Panel ID="FinancialYearPanel" runat="server" Visible="false">
            <div class="row">
                <div class="field"><label for="FinancialYearStartText">Financial year starts</label><asp:TextBox ID="FinancialYearStartText" runat="server" TextMode="Date" /></div>
                <div class="field"><label for="FinancialYearEndText">Financial year ends</label><asp:TextBox ID="FinancialYearEndText" runat="server" TextMode="Date" /></div>
            </div>
        </asp:Panel>
        <div class="row">
            <div class="field">
                <asp:Label ID="ValueLabel" runat="server" AssociatedControlID="ValueText">Name</asp:Label>
                <asp:TextBox ID="ValueText" runat="server" MaxLength="150" />
                <asp:RequiredFieldValidator runat="server" ControlToValidate="ValueText" ValidationGroup="Catalog" CssClass="validation" ErrorMessage="A name is required." />
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="SortOrderText">Display order</asp:Label>
                <asp:TextBox ID="SortOrderText" runat="server" Text="0" TextMode="Number" />
            </div>
        </div>
        <div class="field"><asp:CheckBox ID="IsActiveCheck" runat="server" Text="Active" Checked="true" /></div>
        <div class="admin-actions">
            <asp:Button ID="SaveButton" runat="server" Text="Add item" ValidationGroup="Catalog" OnClick="SaveButton_Click" />
            <asp:Button ID="CancelButton" runat="server" Text="Cancel edit" CausesValidation="false" CssClass="button secondary" OnClick="CancelButton_Click" Visible="false" />
        </div>
    </section>
    <section class="card">
        <h2>Catalog entries</h2>
        <p class="muted">Existing business records are retained. Entries referenced by tenders or documents cannot be deleted.</p>
        <div class="table-wrap">
            <asp:GridView ID="CatalogGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                OnRowCommand="CatalogGrid_RowCommand" EmptyDataText="No entries are configured.">
                <Columns>
                    <asp:BoundField DataField="CatalogValue" HeaderText="Name" />
                    <asp:BoundField DataField="ParentValue" HeaderText="Category" />
                    <asp:BoundField DataField="FinancialYearRange" HeaderText="Date range" />
                    <asp:BoundField DataField="SortOrder" HeaderText="Display order" />
                    <asp:CheckBoxField DataField="IsActive" HeaderText="Active" />
                    <asp:BoundField DataField="UsageCount" HeaderText="References" />
                    <asp:TemplateField HeaderText="Actions"><ItemTemplate>
                        <asp:LinkButton runat="server" Text="Edit" CommandName="EditCatalogItem" CommandArgument='<%# Eval("CatalogKey") %>' CausesValidation="false" />
                        <asp:LinkButton runat="server" Text="Delete" CommandName="DeleteCatalogItem" CommandArgument='<%# Eval("CatalogKey") %>' CausesValidation="false" OnClientClick="return confirm('Delete this unused catalog entry?');" />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
