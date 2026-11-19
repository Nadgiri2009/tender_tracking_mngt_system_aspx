<%@ Page Title="My Profile" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Profile.aspx.cs" Inherits="DepartmentModule.Pages.Profile" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">My Profile</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading"><div><h1>My Profile</h1><div class="muted">Update the name and email saved to your user account.</div></div></div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <div class="field">
            <asp:Label ID="FullNameLabel" runat="server" AssociatedControlID="FullNameText">Full name</asp:Label>
            <asp:TextBox ID="FullNameText" runat="server" MaxLength="150" />
            <asp:RequiredFieldValidator ID="FullNameRequired" runat="server" ControlToValidate="FullNameText" ValidationGroup="Profile" CssClass="validation" Display="Dynamic" ErrorMessage="Enter your full name." />
            <asp:RegularExpressionValidator ID="FullNameLength" runat="server" ControlToValidate="FullNameText" ValidationGroup="Profile" CssClass="validation" Display="Dynamic" ValidationExpression="^[\s\S]{2,150}$" ErrorMessage="Full name must be between 2 and 150 characters." />
        </div>
        <div class="field">
            <asp:Label ID="EmailLabel" runat="server" AssociatedControlID="EmailText">Email</asp:Label>
            <asp:TextBox ID="EmailText" runat="server" TextMode="Email" MaxLength="150" />
            <asp:RequiredFieldValidator ID="EmailRequired" runat="server" ControlToValidate="EmailText" ValidationGroup="Profile" CssClass="validation" Display="Dynamic" ErrorMessage="Enter your email address." />
            <asp:RegularExpressionValidator ID="EmailFormat" runat="server" ControlToValidate="EmailText" ValidationGroup="Profile" CssClass="validation" Display="Dynamic" ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" ErrorMessage="Enter a valid email address." />
        </div>
        <div class="field">
            <asp:Label ID="MobileLabel" runat="server" AssociatedControlID="MobileText">Sign-in mobile</asp:Label>
            <asp:TextBox ID="MobileText" runat="server" ReadOnly="true" />
            <span class="field-help">Mobile changes require administrator verification and cannot be changed here.</span>
        </div>
        <div class="field">
            <asp:Label ID="RoleLabel" runat="server" AssociatedControlID="RoleText">Role</asp:Label>
            <asp:TextBox ID="RoleText" runat="server" ReadOnly="true" />
        </div>
        <asp:Button ID="SaveButton" runat="server" Text="Save profile" CssClass="button" ValidationGroup="Profile" OnClick="SaveButton_Click" />
    </section>
</asp:Content>
