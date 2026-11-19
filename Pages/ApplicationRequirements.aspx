<%@ Page Title="Application Requirements" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="ApplicationRequirements.aspx.cs" Inherits="DepartmentModule.Pages.ApplicationRequirements" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Application Requirements</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div>
            <h1>Application document requirements</h1>
            <div class="muted"><asp:Literal ID="TenderNumber" runat="server" /> · <asp:Literal ID="TenderTitle" runat="server" /></div>
        </div>
        <asp:HyperLink ID="WorkflowLink" runat="server" CssClass="button secondary">Return to tender workflow</asp:HyperLink>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <h2>Add a required document</h2>
        <p class="help">Requirements can be changed only while the tender is a draft and before a contractor creates an application.</p>
        <div class="row">
            <div class="field">
                <label for="DocumentTypeText">Document name</label>
                <asp:TextBox ID="DocumentTypeText" runat="server" MaxLength="160" />
            </div>
            <div class="field">
                <label for="MaxFileSizeText">Maximum file size (bytes)</label>
                <asp:TextBox ID="MaxFileSizeText" runat="server" TextMode="Number" min="1" max="10000000" />
            </div>
        </div>
        <div class="field">
            <label for="DescriptionText">Description (optional)</label>
            <asp:TextBox ID="DescriptionText" runat="server" TextMode="MultiLine" Rows="2" MaxLength="1000" />
        </div>
        <div class="row">
            <div class="field">
                <label for="AllowedExtensionsText">Allowed extensions (comma-separated)</label>
                <asp:TextBox ID="AllowedExtensionsText" runat="server" MaxLength="500" />
                <span class="help">Leave blank to allow PDF, Word, Excel, JPEG, and PNG files.</span>
            </div>
            <div class="field">
                <asp:CheckBox ID="MandatoryCheck" runat="server" Text="Contractor must provide this document" Checked="true" />
            </div>
        </div>
        <asp:Button ID="AddRequirementButton" runat="server" Text="Add requirement" OnClick="AddRequirementButton_Click" />
    </section>
    <section class="card">
        <h2>Configured requirements</h2>
        <div class="table-wrap">
            <asp:GridView ID="RequirementsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                DataKeyNames="DocumentType" EmptyDataText="No application document requirements have been configured."
                OnRowCommand="RequirementsGrid_RowCommand">
                <Columns>
                    <asp:BoundField DataField="DocumentType" HeaderText="Document" />
                    <asp:TemplateField HeaderText="Description"><ItemTemplate>
                        <asp:TextBox ID="EditDescriptionText" runat="server" Text='<%# Eval("Description") %>' MaxLength="1000" />
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Mandatory"><ItemTemplate>
                        <asp:CheckBox ID="EditMandatoryCheck" runat="server" Checked='<%# Convert.ToBoolean(Eval("IsMandatory")) %>' />
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Allowed extensions"><ItemTemplate>
                        <asp:TextBox ID="EditExtensionsText" runat="server" Text='<%# Eval("AllowedExtensions") %>' MaxLength="500" />
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Maximum bytes"><ItemTemplate>
                        <asp:TextBox ID="EditMaxFileSizeText" runat="server" Text='<%# Eval("MaxFileSizeBytes") %>' TextMode="Number" min="1" max="10000000" />
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Actions"><ItemTemplate>
                        <asp:Button ID="UpdateRequirementButton" runat="server" Text="Save" CommandName="UpdateRequirement"
                            CommandArgument='<%# Container.DataItemIndex %>' />
                        <asp:Button ID="DeleteRequirementButton" runat="server" Text="Remove" CssClass="button secondary"
                            CommandName="DeleteRequirement" CommandArgument='<%# Container.DataItemIndex %>'
                            OnClientClick="return confirm('Remove this application document requirement?');" />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
