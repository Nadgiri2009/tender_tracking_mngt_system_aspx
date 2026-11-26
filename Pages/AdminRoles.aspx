<%@ Page Title="Manage Roles" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="AdminRoles.aspx.cs" Inherits="DepartmentModule.Pages.AdminRoles" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Manage Roles</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading role-page-heading">
        <div>
            <div class="role-page-eyebrow">MASTER MANAGEMENT</div>
            <h1>Roles and permissions</h1>
            <div class="muted">Control role access and review permission assignments in one place.</div>
        </div>
        <a class="button secondary" href="Admin.aspx">Back to administration</a>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card role-editor-card" id="role-editor">
        <asp:HiddenField ID="RoleIdHidden" runat="server" />
        <div class="role-section-heading">
            <div>
                <span class="role-section-kicker">ROLE SETUP</span>
                <h2><asp:Literal ID="FormHeading" runat="server">Create custom role</asp:Literal></h2>
            </div>
            <p class="muted">Core workflow role names and workflow-linked records are protected. JE administrators can edit assignments and manage custom roles.</p>
        </div>
        <div class="row">
            <div class="field"><asp:Label runat="server" AssociatedControlID="RoleNameText">Role name</asp:Label><asp:TextBox ID="RoleNameText" runat="server" MaxLength="100" /><asp:RequiredFieldValidator runat="server" ControlToValidate="RoleNameText" ValidationGroup="Role" CssClass="validation" ErrorMessage="Role name is required." /></div>
            <div class="field"><asp:Label runat="server" AssociatedControlID="RoleDescriptionText">Description</asp:Label><asp:TextBox ID="RoleDescriptionText" runat="server" MaxLength="400" TextMode="MultiLine" Rows="3" /></div>
        </div>
        <div class="role-permission-panel">
            <div class="role-subheading">
                <h3>Permission grants</h3>
                <span class="muted">Select only the capabilities this role needs.</span>
            </div>
            <asp:CheckBoxList ID="PermissionsList" runat="server" RepeatLayout="Table"
                RepeatDirection="Vertical" RepeatColumns="2" CssClass="role-permissions" />
        </div>
        <div class="admin-actions">
            <asp:Button ID="SaveRoleButton" runat="server" Text="Save role" ValidationGroup="Role" OnClick="SaveRoleButton_Click" />
            <asp:Button ID="CancelEditButton" runat="server" Text="Cancel edit" CausesValidation="false" CssClass="button secondary" OnClick="CancelEditButton_Click" Visible="false" />
        </div>
    </section>
    <section class="card role-catalog-card" id="role-catalog">
        <div class="role-section-heading">
            <div><span class="role-section-kicker">ACCESS PROFILES</span><h2>Role catalog</h2></div>
            <p class="muted">Review protection, assigned users, and the number of permissions for each role.</p>
        </div>
        <div class="table-wrap">
            <asp:GridView ID="RolesGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="role-data-grid" OnRowCommand="RolesGrid_RowCommand" EmptyDataText="No roles found.">
                <Columns>
                    <asp:BoundField DataField="RoleName" HeaderText="Role" />
                    <asp:BoundField DataField="Description" HeaderText="Description" />
                    <asp:BoundField DataField="PermissionCount" HeaderText="Permissions" />
                    <asp:BoundField DataField="AssignedUsers" HeaderText="Assigned users" />
                    <asp:CheckBoxField DataField="IsActive" HeaderText="Active" />
                    <asp:CheckBoxField DataField="IsSystemRole" HeaderText="Protected" />
                    <asp:TemplateField HeaderText="Actions"><ItemTemplate>
                        <asp:LinkButton runat="server" CssClass="button secondary role-action-link" Text="Edit" CommandName="EditRole" CommandArgument='<%# Eval("RoleId") %>' CausesValidation="false" />
                        <asp:LinkButton runat="server" CssClass="button secondary role-action-link" Text='<%# Convert.ToBoolean(Eval("IsActive")) ? "Deactivate" : "Activate" %>' CommandName="ToggleRole" CommandArgument='<%# Eval("RoleId") %>' CausesValidation="false" Visible='<%# !Convert.ToBoolean(Eval("IsSystemRole")) %>' OnClientClick="return confirm('Change this role status?');" />
                        <asp:LinkButton runat="server" CssClass="button secondary role-action-link danger" Text="Delete" CommandName="DeleteRole" CommandArgument='<%# Eval("RoleId") %>' CausesValidation="false" Visible='<%# !Convert.ToBoolean(Eval("IsSystemRole")) && Convert.ToInt32(Eval("AssignedUsers")) == 0 %>' OnClientClick="return confirm('Delete this unassigned custom role?');" />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card role-catalog-card" id="permission-catalog">
        <div class="role-section-heading">
            <div><span class="role-section-kicker">CAPABILITY DEFINITIONS</span><h2>Permission catalog</h2></div>
            <p class="muted">Keys cannot be renamed. Remove assignments before deactivating or deleting a permission.</p>
        </div>
        <asp:HiddenField ID="PermissionIdHidden" runat="server" />
        <div class="row">
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="PermissionKeyText">Permission key</asp:Label>
                <asp:TextBox ID="PermissionKeyText" runat="server" MaxLength="120" />
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="PermissionDescriptionText">Description</asp:Label>
                <asp:TextBox ID="PermissionDescriptionText" runat="server" MaxLength="400" />
            </div>
        </div>
        <div class="admin-actions">
            <asp:Button ID="SavePermissionButton" runat="server" Text="Add permission" OnClick="SavePermissionButton_Click" />
            <asp:Button ID="CancelPermissionButton" runat="server" Text="Cancel edit" CausesValidation="false" CssClass="button secondary" OnClick="CancelPermissionButton_Click" Visible="false" />
        </div>
        <div class="table-wrap">
            <asp:GridView ID="PermissionsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="role-data-grid"
                OnRowCommand="PermissionsGrid_RowCommand" EmptyDataText="No permissions found.">
                <Columns>
                    <asp:BoundField DataField="PermissionKey" HeaderText="Permission key" />
                    <asp:BoundField DataField="Description" HeaderText="Description" />
                    <asp:BoundField DataField="AssignedRoles" HeaderText="Assigned roles" />
                    <asp:CheckBoxField DataField="IsActive" HeaderText="Active" />
                    <asp:TemplateField HeaderText="Actions"><ItemTemplate>
                        <asp:LinkButton runat="server" CssClass="button secondary role-action-link" Text="Edit description" CommandName="EditPermission" CommandArgument='<%# Eval("PermissionId") %>' CausesValidation="false" />
                        <asp:LinkButton runat="server" CssClass="button secondary role-action-link" Text='<%# Convert.ToBoolean(Eval("IsActive")) ? "Deactivate" : "Activate" %>' CommandName="TogglePermission" CommandArgument='<%# Eval("PermissionId") %>' CausesValidation="false" OnClientClick="return confirm('Change this permission status?');" />
                        <asp:LinkButton runat="server" CssClass="button secondary role-action-link danger" Text="Delete" CommandName="DeletePermission" CommandArgument='<%# Eval("PermissionId") %>' CausesValidation="false" Visible='<%# !Convert.ToBoolean(Eval("IsActive")) && Convert.ToInt32(Eval("AssignedRoles")) == 0 %>' OnClientClick="return confirm('Delete this unused inactive permission?');" />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
