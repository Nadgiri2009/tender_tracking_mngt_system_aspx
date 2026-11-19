<%@ Page Title="Terms &amp; Conditions" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Terms.aspx.cs" Inherits="DepartmentModule.Pages.Terms" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Terms &amp; Conditions</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div>
            <h1>Terms &amp; Conditions</h1>
            <div class="muted">General portal participation guidance for bidders using the Solapur Municipal Corporation tender portal.</div>
        </div>
        <div class="actions">
            <asp:HyperLink ID="HomeLink" runat="server" NavigateUrl="~/Pages/Home.aspx" CssClass="button secondary">Home</asp:HyperLink>
            <asp:HyperLink ID="TendersLink" runat="server" NavigateUrl="~/Pages/Tenders.aspx" CssClass="button secondary">Browse tenders</asp:HyperLink>
        </div>
    </div>
    <div class="message">This is a general checklist, not a substitute for the official tender documents. Requirements vary by tender; the tender notice, schedules, corrigenda, and applicable rules take precedence. Confirm this wording with the Corporation's procurement/legal authority before treating it as an official policy.</div>
    <section class="card">
        <h2>Portal terms</h2>
        <ol class="terms-list">
                <li><h3>Tender notice and amendments</h3><p class="muted">Read the complete tender notice, schedules, specifications, and every addendum before preparing a bid. The tender-specific documents and later official amendments govern if this general summary differs from them.</p></li>
                <li><h3>Eligibility and registration</h3><p class="muted">Bidders must maintain accurate portal registration details and meet the eligibility, licensing, and qualification criteria stated in the relevant tender. Registration on the portal does not guarantee eligibility or award.</p></li>
                <li><h3>Preparation and submission</h3><p class="muted">Submit complete, legible, correctly signed information through the method and formats stated in the tender. Upload technical and financial information in the specified sections; do not include price information in a technical submission where a separate-envelope process applies.</p></li>
                <li><h3>Deadlines and bid validity</h3><p class="muted">Plan for upload and submission completion before the published closing time. A submission is subject to the time recorded by the portal. Bid validity, extensions, and any permitted withdrawal or modification are governed by the tender notice.</p></li>
                <li><h3>Fees and bid security</h3><p class="muted">Pay tender fees, earnest money deposit, or other security only where required, in the amount, form, and manner specified in the tender. Any exemption must be supported by valid documents and is subject to the stated eligibility rules.</p></li>
                <li><h3>Evaluation and clarification</h3><p class="muted">Bids are examined against the published eligibility, technical, and commercial criteria. The Corporation may seek clarifications where permitted by the tender and applicable rules; a clarification must not be treated as permission to change a bid unless expressly allowed.</p></li>
                <li><h3>Integrity and accuracy</h3><p class="muted">Information and documents submitted must be genuine, current, and complete. Bidders must avoid collusion, misrepresentation, unauthorized access, and any attempt to influence the process outside the official channels. Consequences are governed by the tender and applicable rules.</p></li>
                <li><h3>Award and contract</h3><p class="muted">An online notice or evaluation result does not by itself create a contract. The successful bidder must complete the acceptance, agreement, performance security, insurance, and other post-award requirements specified in the tender.</p></li>
                <li><h3>Portal use and account security</h3><p class="muted">Keep login credentials and signing credentials secure, use supported file formats, and check that each upload is readable and associated with the correct tender. Notify the portal administrator through the official contact channel if an account may be compromised.</p></li>
                <li><h3>Applicable documents and interpretation</h3><p class="muted">This page is a general portal guide, not an approved substitute for a tender document or legal advice. The current tender notice, schedules, corrigenda, and applicable Government of Maharashtra / Solapur Municipal Corporation rules prevail. Verify requirements for each tender before submission.</p></li>
        </ol>
    </section>
    <section class="card">
        <h2>Required document checklist</h2>
        <p class="muted">Check the tender notice for the final document list, required format, validity period, and whether each item applies.</p>
        <div class="table-wrap">
            <table>
                <thead><tr><th>S. No.</th><th>Document</th><th>What to prepare</th><th>Applicability</th></tr></thead>
                <tbody>
                    <tr><td>1</td><td>Tender-specific forms and acknowledgement</td><td>Completed tender schedules, declarations, and acknowledgement of the notice and applicable addenda.</td><td>As specified in the tender</td></tr>
                    <tr><td>2</td><td>Bidder constitution and registration proof</td><td>Incorporation, partnership, proprietorship, society, or other entity-registration documents establishing the bidder's legal status.</td><td>As applicable to the bidder</td></tr>
                    <tr><td>3</td><td>Authorized signatory proof</td><td>Power of attorney, board resolution, authorization letter, or equivalent evidence that the signatory may bind the bidder.</td><td>If submitted by a representative</td></tr>
                    <tr><td>4</td><td>PAN and tax registration</td><td>PAN and GST registration or exemption evidence, as required for the bidder and tender.</td><td>As specified in the tender</td></tr>
                    <tr><td>5</td><td>Eligibility and integrity declarations</td><td>Non-blacklisting, conflict-of-interest, integrity, and other declarations in the prescribed format.</td><td>As specified in the tender</td></tr>
                    <tr><td>6</td><td>Experience and performance evidence</td><td>Work orders, completion certificates, client references, or other evidence of similar work where qualification criteria require it.</td><td>For experience-based criteria</td></tr>
                    <tr><td>7</td><td>Financial qualification evidence</td><td>Audited statements, turnover certificates, solvency evidence, or other financial capacity documents where required.</td><td>For financial criteria in the tender</td></tr>
                    <tr><td>8</td><td>Tender fee and bid security evidence</td><td>Proof of tender-fee / EMD payment or the documents supporting a permitted exemption.</td><td>Only when required by the tender</td></tr>
                    <tr><td>9</td><td>Technical proposal and relevant licences</td><td>Technical response, specifications, method statements, product details, and required licences or registrations.</td><td>As specified in the tender</td></tr>
                    <tr><td>10</td><td>Price bid / BOQ</td><td>The completed price schedule in the prescribed format and submission section.</td><td>As specified; keep separate where required</td></tr>
                    <tr><td>11</td><td>Tender-specific permits and certificates</td><td>Any sector, safety, labour, environmental, quality, or other certificate expressly required for the work.</td><td>Only where listed in the tender</td></tr>
                </tbody>
            </table>
        </div>
    </section>
</asp:Content>
